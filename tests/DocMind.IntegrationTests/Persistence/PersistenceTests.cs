using DocMind.Domain.Conversations;
using DocMind.Domain.Documents;
using DocMind.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace DocMind.IntegrationTests.Persistence;

[Collection(PostgresFixture.CollectionName)]
public sealed class PersistenceTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    private async Task<string> NewDatabaseAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = await TestDatabase.CreateMigratedContextAsync(connectionString);
        return connectionString;
    }

    [Fact]
    public async Task CosineSearch_ReturnsTheClosestChunkOfTheRequestedUserOnly()
    {
        var connectionString = await NewDatabaseAsync();
        var owner = TestDatabase.NewUser("owner");
        var stranger = TestDatabase.NewUser("stranger");

        await using (var context = TestDatabase.CreateContext(connectionString))
        {
            context.Users.AddRange(owner, stranger);

            var ownerDocument = Document.Create(owner.Id, "owner.pdf", "application/pdf", 10, "k1", Now);
            var strangerDocument = Document.Create(stranger.Id, "stranger.pdf", "application/pdf", 10, "k2", Now);
            context.Documents.AddRange(ownerDocument, strangerDocument);

            context.DocumentChunks.AddRange(
                DocumentChunk.Create(ownerDocument.Id, owner.Id, 0, 1, "far", TestDatabase.OneHot(0), Now),
                DocumentChunk.Create(ownerDocument.Id, owner.Id, 1, 2, "near", TestDatabase.OneHot(1), Now),
                DocumentChunk.Create(strangerDocument.Id, stranger.Id, 0, 1, "exact but foreign", TestDatabase.OneHot(1), Now));

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var readContext = TestDatabase.CreateContext(connectionString);
        var queryValues = new float[TestDatabase.Dimensions];
        queryValues[0] = 0.1f;
        queryValues[1] = 0.9f;
        var query = new Vector(queryValues);

        var results = await readContext.DocumentChunks
            .Where(c => c.UserId == owner.Id)
            .OrderBy(c => c.Embedding.CosineDistance(query))
            .Take(2)
            .Select(c => new { c.Content, c.PageNumber, Distance = c.Embedding.CosineDistance(query) })
            .ToListAsync(TestContext.Current.CancellationToken);

        results.Select(r => r.Content).Should().Equal("near", "far");
        results[0].PageNumber.Should().Be(2);
        results[0].Distance.Should().BeLessThan(results[1].Distance);
    }

    [Fact]
    public async Task Conversation_RoundTripsMessagesCitationsAndUsage()
    {
        var connectionString = await NewDatabaseAsync();
        var user = TestDatabase.NewUser();
        var documentId = Guid.NewGuid();
        Guid conversationId;

        await using (var context = TestDatabase.CreateContext(connectionString))
        {
            context.Users.Add(user);
            var conversation = Conversation.Create(user.Id, "Quarterly report", Now);
            conversation.AddMessage(MessageRole.User, "What changed?", null, null, Now.AddSeconds(1));
            conversation.AddMessage(
                MessageRole.Assistant,
                "Revenue grew.",
                [new Citation(documentId, "q3.pdf", 7, "Revenue grew by 12%")],
                new TokenUsage(120, 30),
                Now.AddSeconds(2));
            context.Conversations.Add(conversation);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            conversationId = conversation.Id;
        }

        await using var readContext = TestDatabase.CreateContext(connectionString);
        var loaded = await readContext.Conversations
            .Include(c => c.Messages)
            .SingleAsync(c => c.Id == conversationId, TestContext.Current.CancellationToken);

        var messages = loaded.Messages.OrderBy(m => m.CreatedAt).ToList();
        loaded.Title.Should().Be("Quarterly report");
        messages.Should().HaveCount(2);
        messages[0].Role.Should().Be(MessageRole.User);
        messages[0].Citations.Should().BeEmpty();
        messages[0].Usage.Should().BeNull();
        messages[1].Role.Should().Be(MessageRole.Assistant);
        messages[1].Citations.Should().ContainSingle().Which.Should().Be(new Citation(documentId, "q3.pdf", 7, "Revenue grew by 12%"));
        messages[1].Usage.Should().Be(new TokenUsage(120, 30));
    }

    [Fact]
    public async Task DocumentStatus_IsStoredAsReadableText()
    {
        var connectionString = await NewDatabaseAsync();
        var user = TestDatabase.NewUser();

        await using (var context = TestDatabase.CreateContext(connectionString))
        {
            context.Users.Add(user);
            var document = Document.Create(user.Id, "a.pdf", "application/pdf", 1, "k", Now);
            document.MarkProcessing(Now);
            document.MarkFailed("boom", Now);
            context.Documents.Add(document);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var readContext = TestDatabase.CreateContext(connectionString);
        var stored = await readContext.Database
            .SqlQueryRaw<string>("SELECT \"Status\" AS \"Value\" FROM \"Documents\"")
            .SingleAsync(TestContext.Current.CancellationToken);

        stored.Should().Be("Failed");
    }

    [Fact]
    public async Task DeletingADocument_CascadesToItsChunks()
    {
        var connectionString = await NewDatabaseAsync();
        var user = TestDatabase.NewUser();
        Guid documentId;

        await using (var context = TestDatabase.CreateContext(connectionString))
        {
            context.Users.Add(user);
            var document = Document.Create(user.Id, "a.pdf", "application/pdf", 1, "k", Now);
            context.Documents.Add(document);
            context.DocumentChunks.Add(DocumentChunk.Create(document.Id, user.Id, 0, 1, "text", TestDatabase.OneHot(3), Now));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            documentId = document.Id;
        }

        await using (var deleteContext = TestDatabase.CreateContext(connectionString))
        {
            await deleteContext.Documents.Where(d => d.Id == documentId).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }

        await using var readContext = TestDatabase.CreateContext(connectionString);
        (await readContext.DocumentChunks.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task DeletingAUser_RemovesAllOfTheirData()
    {
        var connectionString = await NewDatabaseAsync();
        var user = TestDatabase.NewUser();

        await using (var context = TestDatabase.CreateContext(connectionString))
        {
            context.Users.Add(user);
            var document = Document.Create(user.Id, "a.pdf", "application/pdf", 1, "k", Now);
            context.Documents.Add(document);
            context.DocumentChunks.Add(DocumentChunk.Create(document.Id, user.Id, 0, 1, "text", TestDatabase.OneHot(3), Now));
            var conversation = Conversation.Create(user.Id, null, Now);
            conversation.AddMessage(MessageRole.User, "hello", null, null, Now);
            context.Conversations.Add(conversation);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var deleteContext = TestDatabase.CreateContext(connectionString))
        {
            await deleteContext.Users.Where(u => u.Id == user.Id).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }

        await using var readContext = TestDatabase.CreateContext(connectionString);
        (await readContext.Documents.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        (await readContext.DocumentChunks.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        (await readContext.Conversations.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        (await readContext.Messages.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }
}
