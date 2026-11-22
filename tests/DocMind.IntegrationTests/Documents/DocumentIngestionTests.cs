using System.Net;
using DocMind.Application.Features.Ingestion;
using DocMind.Domain.Documents;
using DocMind.IntegrationTests.Infrastructure;
using DocMind.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace DocMind.IntegrationTests.Documents;

[Collection(PostgresFixture.CollectionName)]
public sealed class DocumentIngestionTests(PostgresFixture postgres) : DocumentApiTestBase(postgres)
{
    [Fact]
    public async Task Ingestion_ExtractsChunksEmbedsAndMarksTheDocumentReady()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(session, "handbook.pdf", TestDocuments.Sentences("onboarding", 3), TestDocuments.Sentences("benefits", 3));

        (await DrainQueueAsync()).Should().Be(1);

        var document = await GetDocumentAsync(session, id);
        document.GetProperty("status").GetString().Should().Be("Ready");
        document.GetProperty("pageCount").GetInt32().Should().Be(2);
        var chunkCount = document.GetProperty("chunkCount").GetInt32();
        chunkCount.Should().BeGreaterThanOrEqualTo(2);
        document.GetProperty("errorMessage").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        await using var db = TestDatabase.CreateContext(ConnectionString);
        var chunks = await db.DocumentChunks.Where(c => c.DocumentId == id).OrderBy(c => c.ChunkIndex).ToListAsync(TestContext.Current.CancellationToken);
        chunks.Should().HaveCount(chunkCount);
        chunks.Select(c => c.ChunkIndex).Should().Equal(Enumerable.Range(0, chunkCount));
        chunks.Select(c => c.PageNumber).Distinct().Should().Equal(1, 2);
        chunks.Should().OnlyContain(c => c.UserId == session.UserId && c.Embedding.ToArray().Length == TestDatabase.Dimensions);
        chunks.First(c => c.PageNumber == 1).Content.Should().Contain("onboarding");
        chunks.First(c => c.PageNumber == 2).Content.Should().Contain("benefits");
        Embeddings.Inputs.Should().HaveCount(chunkCount);
    }

    [Fact]
    public async Task Ingestion_ProcessesDocxFilesWithPageNumbers()
    {
        var session = await Client.RegisterAndLoginAsync();
        var response = await UploadAsync(session, "policy.docx", TestDocuments.Docx("First page policy text", "Second page appendix text"), TestDocuments.DocxContentType);
        var id = (await response.ReadJsonAsync()).GetProperty("id").GetGuid();

        await DrainQueueAsync();

        (await GetDocumentAsync(session, id)).GetProperty("status").GetString().Should().Be("Ready");
        await using var db = TestDatabase.CreateContext(ConnectionString);
        var pages = await db.DocumentChunks.Where(c => c.DocumentId == id).Select(c => c.PageNumber).Distinct().OrderBy(p => p).ToListAsync(TestContext.Current.CancellationToken);
        pages.Should().Equal(1, 2);
    }

    [Fact]
    public async Task Ingestion_BatchesEmbeddingCalls()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(session, "long.pdf", Enumerable.Range(0, 12).Select(i => TestDocuments.Sentences($"topic{i}", 30)).ToArray());

        await DrainQueueAsync();

        var chunks = await CountChunksAsync(id);
        chunks.Should().BeGreaterThan(16);
        Embeddings.Inputs.Should().HaveCount(chunks);
        Embeddings.BatchSizes.Should().HaveCountGreaterThan(1).And.OnlyContain(size => size <= 16);
    }

    [Fact]
    public async Task Ingestion_RetriesTransientEmbeddingFailuresWithBackoff()
    {
        var failures = 0;
        Embeddings.FailWith = _ => Interlocked.Increment(ref failures) <= 2 ? new HttpRequestException("model is loading") : null;
        var session = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(session, "retry.pdf", TestDocuments.Sentences("retries", 2));

        await DrainQueueAsync();

        (await GetDocumentAsync(session, id)).GetProperty("status").GetString().Should().Be("Ready");
        failures.Should().Be(3);
    }

    [Fact]
    public async Task Ingestion_FailsWithAClearMessageWhenTheEmbeddingProviderStaysDown()
    {
        Embeddings.FailWith = _ => new HttpRequestException("connection refused");
        var session = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(session, "down.pdf", TestDocuments.Sentences("outage", 2));

        await DrainQueueAsync();

        var document = await GetDocumentAsync(session, id);
        document.GetProperty("status").GetString().Should().Be("Failed");
        document.GetProperty("errorMessage").GetString().Should().Contain("dependent service").And.NotContain("connection refused");
        Embeddings.Inputs.Should().HaveCount(3);
        (await CountChunksAsync(id)).Should().Be(0);
    }

    [Fact]
    public async Task Ingestion_FailsWhenTheEmbeddingModelReturnsTheWrongDimension()
    {
        Embeddings.Dimensions = 512;
        var session = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(session, "dimension.pdf", TestDocuments.Sentences("size", 2));

        await DrainQueueAsync();

        var document = await GetDocumentAsync(session, id);
        document.GetProperty("status").GetString().Should().Be("Failed");
        document.GetProperty("errorMessage").GetString().Should().Be("An unexpected error occurred while processing the document.");
        Embeddings.Inputs.Should().HaveCount(1);
    }

    [Fact]
    public async Task Ingestion_FailsForDocumentsWithoutExtractableText()
    {
        var session = await Client.RegisterAndLoginAsync();
        var response = await UploadAsync(session, "scan.pdf", TestDocuments.BlankPdf(2), TestDocuments.PdfContentType);
        var id = (await response.ReadJsonAsync()).GetProperty("id").GetGuid();

        await DrainQueueAsync();

        var document = await GetDocumentAsync(session, id);
        document.GetProperty("status").GetString().Should().Be("Failed");
        document.GetProperty("errorMessage").GetString().Should().Contain("no extractable text");
        Embeddings.Inputs.Should().BeEmpty();
    }

    [Fact]
    public async Task Ingestion_FailsForCorruptFilesWithoutRetrying()
    {
        var session = await Client.RegisterAndLoginAsync();
        var response = await UploadAsync(session, "broken.pdf", TestDocuments.CorruptPdf(), TestDocuments.PdfContentType);
        var id = (await response.ReadJsonAsync()).GetProperty("id").GetGuid();

        (await DrainQueueAsync()).Should().Be(1);

        var document = await GetDocumentAsync(session, id);
        document.GetProperty("status").GetString().Should().Be("Failed");
        document.GetProperty("errorMessage").GetString().Should().Contain("corrupt");
    }

    [Fact]
    public async Task Ingestion_IsIdempotentForRedeliveredMessages()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(session, "once.pdf", TestDocuments.Sentences("idempotency", 2));
        await DrainQueueAsync();
        var embeddedOnce = Embeddings.Inputs.Count;

        await using var scope = Factory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<IngestDocumentHandler>();
        var outcome = await handler.HandleAsync(id, TestContext.Current.CancellationToken);

        outcome.Should().Be(IngestionOutcome.Skipped);
        Embeddings.Inputs.Should().HaveCount(embeddedOnce);
    }

    [Fact]
    public async Task Ingestion_IgnoresDocumentsThatWereDeletedBeforeProcessing()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(session, "gone.pdf", "content");
        (await Client.SendAsync(session, HttpMethod.Delete, $"/api/v1/documents/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await DrainQueueAsync();

        Embeddings.Inputs.Should().BeEmpty();
    }

    [Fact]
    public async Task Ingestion_DropsMalformedQueueMessages()
    {
        var queue = Factory.Services.GetRequiredService<DocMind.Application.Abstractions.IMessageQueue>();
        await queue.PublishAsync("this is not json", TestContext.Current.CancellationToken);

        (await DrainQueueAsync()).Should().Be(1);
        (await DrainQueueAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Recovery_ProcessesDocumentsWhoseMessageWasLost()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(session, "lost.pdf", TestDocuments.Sentences("recovery", 2));
        await DrainQueueAsync();
        await ResetToPendingAsync(id);

        var sweeper = Factory.Services.GetRequiredService<DocumentRecoverySweeper>();
        (await sweeper.SweepAsync(TestContext.Current.CancellationToken)).Should().Be(0);

        Time.Advance(TimeSpan.FromSeconds(20));
        (await sweeper.SweepAsync(TestContext.Current.CancellationToken)).Should().Be(1);

        (await GetDocumentAsync(session, id)).GetProperty("status").GetString().Should().Be("Ready");
        (await CountChunksAsync(id)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Recovery_ReclaimsDocumentsAbandonedByACrashedWorker()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(session, "crashed.pdf", TestDocuments.Sentences("crash", 2));
        await DrainQueueAsync();
        await ResetToPendingAsync(id, DocumentStatus.Processing);
        var sweeper = Factory.Services.GetRequiredService<DocumentRecoverySweeper>();

        Time.Advance(TimeSpan.FromMinutes(5));
        (await sweeper.SweepAsync(TestContext.Current.CancellationToken)).Should().Be(0);

        Time.Advance(TimeSpan.FromMinutes(6));
        (await sweeper.SweepAsync(TestContext.Current.CancellationToken)).Should().Be(1);

        (await GetDocumentAsync(session, id)).GetProperty("status").GetString().Should().Be("Ready");
    }

    [Fact]
    public async Task Reprocessing_ReplacesPreviousChunksInsteadOfDuplicatingThem()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(session, "again.pdf", TestDocuments.Sentences("replacement", 3));
        await DrainQueueAsync();
        var first = await CountChunksAsync(id);
        await ResetToPendingAsync(id);

        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IngestDocumentHandler>().HandleAsync(id, TestContext.Current.CancellationToken);

        (await CountChunksAsync(id)).Should().Be(first);
    }

    private async Task ResetToPendingAsync(Guid id, DocumentStatus status = DocumentStatus.Pending)
    {
        await using var db = TestDatabase.CreateContext(ConnectionString);
        var at = Time.GetUtcNow();
        await db.Documents.Where(d => d.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, status).SetProperty(d => d.UpdatedAt, at), TestContext.Current.CancellationToken);
    }
}
