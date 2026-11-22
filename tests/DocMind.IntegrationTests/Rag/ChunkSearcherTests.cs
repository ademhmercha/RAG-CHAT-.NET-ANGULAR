using DocMind.Application.Abstractions;
using DocMind.Domain.Documents;
using DocMind.Infrastructure.Persistence;
using DocMind.Infrastructure.Retrieval;
using DocMind.IntegrationTests.Infrastructure;
using Pgvector;

namespace DocMind.IntegrationTests.Rag;

[Collection(PostgresFixture.CollectionName)]
public sealed class ChunkSearcherTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static ReadOnlyMemory<float> Query(params (int Index, float Value)[] components)
    {
        var vector = new float[TestDatabase.Dimensions];
        foreach (var (index, value) in components)
        {
            vector[index] = value;
        }

        return vector;
    }

    private static Vector Embedding(params (int Index, float Value)[] components) => new(Query(components));

    private async Task<(string ConnectionString, Guid UserId)> ArrangeAsync(Action<AppDbContext, Guid> seed)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = await TestDatabase.CreateMigratedContextAsync(connectionString);
        var user = TestDatabase.NewUser();
        context.Users.Add(user);
        seed(context, user.Id);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (connectionString, user.Id);
    }

    private static Document NewDocument(Guid userId, string name, DocumentStatus status = DocumentStatus.Ready)
    {
        var document = Document.Create(userId, name, "application/pdf", 10, $"k/{Guid.NewGuid():N}", Now);
        if (status != DocumentStatus.Pending)
        {
            document.MarkProcessing(Now);
        }

        if (status == DocumentStatus.Ready)
        {
            document.MarkReady(1, 1, Now);
        }

        return document;
    }

    private static async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        string connectionString,
        Guid userId,
        ReadOnlyMemory<float> query,
        IReadOnlyCollection<Guid>? documents = null,
        int topK = 5,
        double minScore = 0.0)
    {
        await using var context = TestDatabase.CreateContext(connectionString);
        return await new PgVectorChunkSearcher(context).SearchAsync(userId, query, documents, topK, minScore, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Search_RanksChunksByCosineSimilarityAndReportsScores()
    {
        var (connection, userId) = await ArrangeAsync((db, user) =>
        {
            var document = NewDocument(user, "doc.pdf");
            db.Documents.Add(document);
            db.DocumentChunks.AddRange(
                DocumentChunk.Create(document.Id, user, 0, 1, "orthogonal", Embedding((0, 1f)), Now),
                DocumentChunk.Create(document.Id, user, 1, 2, "close", Embedding((1, 1f), (2, 0.2f)), Now),
                DocumentChunk.Create(document.Id, user, 2, 3, "exact", Embedding((1, 1f)), Now));
        });

        var results = await SearchAsync(connection, userId, Query((1, 1f)));

        results.Select(r => r.Content).Should().Equal("exact", "close", "orthogonal");
        results[0].Score.Should().BeApproximately(1.0, 1e-6);
        results[1].Score.Should().BeApproximately(1 / Math.Sqrt(1.04), 1e-5);
        results[2].Score.Should().BeApproximately(0.0, 1e-6);
        results.Select(r => r.Page).Should().Equal(3, 2, 1);
        results.Should().OnlyContain(r => r.DocumentName == "doc.pdf");
    }

    [Fact]
    public async Task Search_AppliesTopKAndTheMinimumScore()
    {
        var (connection, userId) = await ArrangeAsync((db, user) =>
        {
            var document = NewDocument(user, "doc.pdf");
            db.Documents.Add(document);
            for (var i = 0; i < 6; i++)
            {
                db.DocumentChunks.Add(DocumentChunk.Create(document.Id, user, i, 1, $"chunk{i}", Embedding((1, 1f), (3, i * 0.1f)), Now));
            }
        });

        (await SearchAsync(connection, userId, Query((1, 1f)), topK: 3)).Should().HaveCount(3);
        (await SearchAsync(connection, userId, Query((1, 1f)), minScore: 0.99)).Select(r => r.Content).Should().Equal("chunk0", "chunk1");
        (await SearchAsync(connection, userId, Query((5, 1f)), minScore: 0.5)).Should().BeEmpty();
    }

    [Fact]
    public async Task Search_NeverReturnsChunksOfOtherUsers()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        Guid me;
        await using (var context = await TestDatabase.CreateMigratedContextAsync(connectionString))
        {
            var mine = TestDatabase.NewUser();
            var theirs = TestDatabase.NewUser();
            context.Users.AddRange(mine, theirs);
            var myDoc = NewDocument(mine.Id, "mine.pdf");
            var theirDoc = NewDocument(theirs.Id, "theirs.pdf");
            context.Documents.AddRange(myDoc, theirDoc);
            context.DocumentChunks.AddRange(
                DocumentChunk.Create(myDoc.Id, mine.Id, 0, 1, "mine", Embedding((1, 0.5f), (2, 0.5f)), Now),
                DocumentChunk.Create(theirDoc.Id, theirs.Id, 0, 1, "theirs exact", Embedding((1, 1f)), Now));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            me = mine.Id;
        }

        var results = await SearchAsync(connectionString, me, Query((1, 1f)));

        results.Select(r => r.Content).Should().Equal("mine");
    }

    [Fact]
    public async Task Search_CanBeRestrictedToSpecificDocuments()
    {
        Guid second = Guid.Empty;
        var (connection, userId) = await ArrangeAsync((db, user) =>
        {
            var first = NewDocument(user, "first.pdf");
            var other = NewDocument(user, "second.pdf");
            second = other.Id;
            db.Documents.AddRange(first, other);
            db.DocumentChunks.AddRange(
                DocumentChunk.Create(first.Id, user, 0, 1, "from first", Embedding((1, 1f)), Now),
                DocumentChunk.Create(other.Id, user, 0, 1, "from second", Embedding((1, 0.9f), (2, 0.1f)), Now));
        });

        var results = await SearchAsync(connection, userId, Query((1, 1f)), [second]);

        results.Select(r => r.Content).Should().Equal("from second");
    }

    [Fact]
    public async Task Search_SkipsDocumentsThatAreNotReady()
    {
        var (connection, userId) = await ArrangeAsync((db, user) =>
        {
            var ready = NewDocument(user, "ready.pdf");
            var processing = NewDocument(user, "processing.pdf", DocumentStatus.Processing);
            db.Documents.AddRange(ready, processing);
            db.DocumentChunks.AddRange(
                DocumentChunk.Create(ready.Id, user, 0, 1, "ready chunk", Embedding((1, 0.8f), (2, 0.2f)), Now),
                DocumentChunk.Create(processing.Id, user, 0, 1, "half processed chunk", Embedding((1, 1f)), Now));
        });

        var results = await SearchAsync(connection, userId, Query((1, 1f)));

        results.Select(r => r.Content).Should().Equal("ready chunk");
    }

    [Fact]
    public async Task Search_StillFillsTopKWhenMostChunksBelongToOtherUsers()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        Guid me;
        await using (var context = await TestDatabase.CreateMigratedContextAsync(connectionString))
        {
            var mine = TestDatabase.NewUser();
            var noisy = TestDatabase.NewUser();
            context.Users.AddRange(mine, noisy);
            var myDoc = NewDocument(mine.Id, "mine.pdf");
            var noisyDoc = NewDocument(noisy.Id, "noise.pdf");
            context.Documents.AddRange(myDoc, noisyDoc);

            for (var i = 0; i < 120; i++)
            {
                context.DocumentChunks.Add(DocumentChunk.Create(noisyDoc.Id, noisy.Id, i, 1, $"noise{i}", Embedding((1, 1f), (3, i * 0.001f)), Now));
            }

            for (var i = 0; i < 5; i++)
            {
                context.DocumentChunks.Add(DocumentChunk.Create(myDoc.Id, mine.Id, i, 1, $"mine{i}", Embedding((1, 0.2f), (4, 1f)), Now));
            }

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            me = mine.Id;
        }

        var results = await SearchAsync(connectionString, me, Query((1, 1f)), topK: 5);

        results.Should().HaveCount(5);
        results.Should().OnlyContain(r => r.Content.StartsWith("mine", StringComparison.Ordinal));
    }
}
