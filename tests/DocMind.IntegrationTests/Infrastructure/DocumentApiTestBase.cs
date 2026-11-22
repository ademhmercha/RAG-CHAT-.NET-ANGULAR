using System.Net.Http.Headers;
using DocMind.Application.Features.Ingestion;
using DocMind.Domain.Documents;
using Microsoft.Extensions.DependencyInjection;

namespace DocMind.IntegrationTests.Infrastructure;

public abstract class DocumentApiTestBase(PostgresFixture postgres) : ChatApiTestBase(postgres)
{
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), $"docmind-it-{Guid.NewGuid():N}");

    protected string StorageRoot => _storageRoot;

    protected override IReadOnlyDictionary<string, string?>? Settings => new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["Storage:Local:RootPath"] = _storageRoot,
        ["Ingestion:RetryBaseDelayMilliseconds"] = "1",
        ["Ingestion:MaxAttempts"] = "3",
        ["Ingestion:PendingGraceSeconds"] = "15",
        ["Ingestion:LeaseMinutes"] = "10",
        ["Sse:HeartbeatMilliseconds"] = "200",
        ["Documents:MaxSizeBytes"] = "1048576",
        ["Documents:MaxDocumentsPerUser"] = "6",
    };

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    protected static MultipartFormDataContent FileContent(string fileName, byte[] bytes, string? contentType = null)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType ?? "application/octet-stream");

        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    protected Task<HttpResponseMessage> UploadAsync(TestSession session, string fileName, byte[] bytes, string? contentType = null) =>
        Client.SendAsync(session, HttpMethod.Post, "/api/v1/documents", FileContent(fileName, bytes, contentType));

    protected async Task<Guid> UploadPdfAsync(TestSession session, string fileName, params string[] pages)
    {
        var response = await UploadAsync(session, fileName, TestSupport.TestDocuments.Pdf(pages), TestSupport.TestDocuments.PdfContentType);
        response.EnsureSuccessStatusCode();
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    protected async Task<int> DrainQueueAsync()
    {
        var processor = Factory.Services.GetRequiredService<IngestionQueueProcessor>();
        var total = 0;

        while (true)
        {
            var processed = await processor.ProcessBatchAsync(TimeSpan.FromMilliseconds(150), TestContext.Current.CancellationToken);
            if (processed == 0)
            {
                return total;
            }

            total += processed;
        }
    }

    protected async Task<System.Text.Json.JsonElement> GetDocumentAsync(TestSession session, Guid id)
    {
        var response = await Client.GetAsync(session, $"/api/v1/documents/{id}");
        response.EnsureSuccessStatusCode();
        return await response.ReadJsonAsync();
    }

    protected async Task<Document> LoadDocumentAsync(Guid id)
    {
        await using var db = TestDatabase.CreateContext(ConnectionString);
        return await db.Documents.AsNoTracking().SingleAsync(d => d.Id == id, TestContext.Current.CancellationToken);
    }

    protected async Task<int> CountChunksAsync(Guid documentId)
    {
        await using var db = TestDatabase.CreateContext(ConnectionString);
        return await db.DocumentChunks.CountAsync(c => c.DocumentId == documentId, TestContext.Current.CancellationToken);
    }
}
