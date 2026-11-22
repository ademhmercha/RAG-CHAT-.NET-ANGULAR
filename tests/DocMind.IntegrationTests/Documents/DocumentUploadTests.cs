using System.Net;
using DocMind.IntegrationTests.Infrastructure;
using DocMind.TestSupport;

namespace DocMind.IntegrationTests.Documents;

[Collection(PostgresFixture.CollectionName)]
public sealed class DocumentUploadTests(PostgresFixture postgres) : DocumentApiTestBase(postgres)
{
    [Fact]
    public async Task Upload_AcceptsAPdfAndReturnsAPendingDocument()
    {
        var session = await Client.RegisterAndLoginAsync();
        var bytes = TestDocuments.Pdf("Quarterly report");

        var response = await UploadAsync(session, "report.pdf", bytes, TestDocuments.PdfContentType);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().Should().Be($"/api/v1/documents/{id}");
        body.GetProperty("fileName").GetString().Should().Be("report.pdf");
        body.GetProperty("contentType").GetString().Should().Be(TestDocuments.PdfContentType);
        body.GetProperty("sizeBytes").GetInt64().Should().Be(bytes.Length);
        body.GetProperty("status").GetString().Should().Be("Pending");
        body.GetProperty("chunkCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Upload_StoresTheFileUnderAnOpaqueKeyAndQueuesIngestion()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(session, "../../evil name.pdf", "content");

        var document = await LoadDocumentAsync(id);

        document.FileName.Should().Be("evil name.pdf");
        document.StorageKey.Should().Be($"documents/{session.UserId:N}/{id:N}/original.pdf");
        File.Exists(Path.Combine(StorageRoot, document.StorageKey.Replace('/', Path.DirectorySeparatorChar))).Should().BeTrue();
    }

    [Fact]
    public async Task Upload_AcceptsADocx()
    {
        var session = await Client.RegisterAndLoginAsync();

        var response = await UploadAsync(session, "notes.docx", TestDocuments.Docx("Meeting notes"), TestDocuments.DocxContentType);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await response.ReadJsonAsync()).GetProperty("contentType").GetString().Should().Be(TestDocuments.DocxContentType);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("image.png")]
    [InlineData("noextension")]
    public async Task Upload_RejectsUnsupportedExtensions(string fileName)
    {
        var session = await Client.RegisterAndLoginAsync();

        var response = await UploadAsync(session, fileName, "plain text"u8.ToArray());

        response.ShouldBeProblem(HttpStatusCode.UnsupportedMediaType);
        (await response.ReadJsonAsync()).GetProperty("code").GetString().Should().Be("documents.unsupported_type");
    }

    [Fact]
    public async Task Upload_RejectsContentThatDoesNotMatchTheExtension()
    {
        var session = await Client.RegisterAndLoginAsync();

        var disguisedText = await UploadAsync(session, "fake.pdf", "just some text, not a pdf"u8.ToArray());
        var pdfAsDocx = await UploadAsync(session, "wrong.docx", TestDocuments.Pdf("hello"));

        disguisedText.ShouldBeProblem(HttpStatusCode.UnsupportedMediaType);
        pdfAsDocx.ShouldBeProblem(HttpStatusCode.UnsupportedMediaType);
        (await disguisedText.ReadJsonAsync()).GetProperty("code").GetString().Should().Be("documents.content_mismatch");
    }

    [Fact]
    public async Task Upload_RejectsEmptyFiles()
    {
        var session = await Client.RegisterAndLoginAsync();

        var response = await UploadAsync(session, "empty.pdf", []);

        response.ShouldBeProblem(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("code").GetString().Should().Be("documents.empty_file");
    }

    [Fact]
    public async Task Upload_RejectsFilesOverTheConfiguredLimit()
    {
        var session = await Client.RegisterAndLoginAsync();
        var oversized = new byte[(1024 * 1024) + 1];
        "%PDF-1.4"u8.CopyTo(oversized);

        var response = await UploadAsync(session, "huge.pdf", oversized, TestDocuments.PdfContentType);

        response.ShouldBeProblem(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Upload_RejectsRequestsFarOverTheLimitBeforeReadingThem()
    {
        var session = await Client.RegisterAndLoginAsync();
        var huge = new byte[3 * 1024 * 1024];
        "%PDF-1.4"u8.CopyTo(huge);

        var response = await UploadAsync(session, "huge.pdf", huge, TestDocuments.PdfContentType);

        response.ShouldBeProblem(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Upload_RequiresAFilePart()
    {
        var session = await Client.RegisterAndLoginAsync();
        using var form = new MultipartFormDataContent { { new StringContent("value"), "other" } };

        var response = await Client.SendAsync(session, HttpMethod.Post, "/api/v1/documents", form);

        response.ShouldBeProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_EnforcesThePerUserQuota()
    {
        var session = await Client.RegisterAndLoginAsync();
        for (var i = 0; i < 6; i++)
        {
            await UploadPdfAsync(session, $"doc{i}.pdf", $"content {i}");
        }

        var response = await UploadAsync(session, "one-too-many.pdf", TestDocuments.Pdf("extra"), TestDocuments.PdfContentType);

        response.ShouldBeProblem(HttpStatusCode.Conflict);
        (await response.ReadJsonAsync()).GetProperty("code").GetString().Should().Be("documents.quota_exceeded");

        var other = await Client.RegisterAndLoginAsync();
        (await UploadAsync(other, "mine.pdf", TestDocuments.Pdf("ok"), TestDocuments.PdfContentType)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Upload_RequiresAuthentication()
    {
        var response = await Client.PostAsync("/api/v1/documents", FileContent("a.pdf", TestDocuments.Pdf("x")), TestContext.Current.CancellationToken);

        response.ShouldBeProblem(HttpStatusCode.Unauthorized);
    }
}
