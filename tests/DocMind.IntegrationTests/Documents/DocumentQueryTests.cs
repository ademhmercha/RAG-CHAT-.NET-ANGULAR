using System.Net;
using DocMind.IntegrationTests.Infrastructure;
using DocMind.TestSupport;

namespace DocMind.IntegrationTests.Documents;

[Collection(PostgresFixture.CollectionName)]
public sealed class DocumentQueryTests(PostgresFixture postgres) : DocumentApiTestBase(postgres)
{
    [Fact]
    public async Task List_PagesDocumentsNewestFirst()
    {
        var session = await Client.RegisterAndLoginAsync();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add(await UploadPdfAsync(session, $"doc{i}.pdf", $"content {i}"));
            Time.Advance(TimeSpan.FromMinutes(1));
        }

        var first = await (await Client.GetAsync(session, "/api/v1/documents?page=1&pageSize=2")).ReadJsonAsync();
        var second = await (await Client.GetAsync(session, "/api/v1/documents?page=2&pageSize=2")).ReadJsonAsync();

        first.GetProperty("items").EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Should().Equal(ids[2], ids[1]);
        second.GetProperty("items").EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Should().Equal(ids[0]);
        first.GetProperty("totalCount").GetInt64().Should().Be(3);
        first.GetProperty("totalPages").GetInt32().Should().Be(2);
        first.GetProperty("page").GetInt32().Should().Be(1);
        first.GetProperty("pageSize").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task List_UsesSensibleDefaultsWithoutParameters()
    {
        var session = await Client.RegisterAndLoginAsync();
        await UploadPdfAsync(session, "only.pdf", "content");

        var body = await (await Client.GetAsync(session, "/api/v1/documents")).ReadJsonAsync();

        body.GetProperty("page").GetInt32().Should().Be(1);
        body.GetProperty("pageSize").GetInt32().Should().Be(20);
        body.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task List_FiltersByStatusCaseInsensitively()
    {
        var session = await Client.RegisterAndLoginAsync();
        var ready = await UploadPdfAsync(session, "ready.pdf", TestDocuments.Sentences("ready", 2));
        await DrainQueueAsync();
        var pending = await UploadPdfAsync(session, "pending.pdf", "waiting");

        var readyOnly = await (await Client.GetAsync(session, "/api/v1/documents?status=ready")).ReadJsonAsync();
        var pendingOnly = await (await Client.GetAsync(session, "/api/v1/documents?status=Pending")).ReadJsonAsync();
        var failedOnly = await (await Client.GetAsync(session, "/api/v1/documents?status=FAILED")).ReadJsonAsync();

        readyOnly.GetProperty("items").EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Should().Equal(ready);
        pendingOnly.GetProperty("items").EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Should().Equal(pending);
        failedOnly.GetProperty("totalCount").GetInt64().Should().Be(0);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("page=-3")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("status=archived")]
    public async Task List_RejectsInvalidParameters(string query)
    {
        var session = await Client.RegisterAndLoginAsync();

        var response = await Client.GetAsync(session, $"/api/v1/documents?{query}");

        response.ShouldBeProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_OnlyShowsMyDocuments()
    {
        var me = await Client.RegisterAndLoginAsync();
        var other = await Client.RegisterAndLoginAsync();
        var mine = await UploadPdfAsync(me, "mine.pdf", "mine");
        await UploadPdfAsync(other, "theirs.pdf", "theirs");

        var body = await (await Client.GetAsync(me, "/api/v1/documents")).ReadJsonAsync();

        body.GetProperty("items").EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Should().Equal(mine);
        body.GetProperty("totalCount").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task Get_ReturnsTheDocumentOr404()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(session, "one.pdf", "content");

        var found = await Client.GetAsync(session, $"/api/v1/documents/{id}");
        var missing = await Client.GetAsync(session, $"/api/v1/documents/{Guid.NewGuid()}");

        found.StatusCode.Should().Be(HttpStatusCode.OK);
        (await found.ReadJsonAsync()).GetProperty("fileName").GetString().Should().Be("one.pdf");
        missing.ShouldBeProblem(HttpStatusCode.NotFound);
        (await missing.ReadJsonAsync()).GetProperty("code").GetString().Should().Be("documents.not_found");
    }

    [Fact]
    public async Task Documents_AreInvisibleToOtherUsers()
    {
        var owner = await Client.RegisterAndLoginAsync();
        var intruder = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(owner, "secret.pdf", TestDocuments.Sentences("secret", 2));
        await DrainQueueAsync();

        var get = await Client.GetAsync(intruder, $"/api/v1/documents/{id}");
        var delete = await Client.SendAsync(intruder, HttpMethod.Delete, $"/api/v1/documents/{id}");

        get.ShouldBeProblem(HttpStatusCode.NotFound);
        delete.ShouldBeProblem(HttpStatusCode.NotFound);
        (await GetDocumentAsync(owner, id)).GetProperty("status").GetString().Should().Be("Ready");
        (await CountChunksAsync(id)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Delete_RemovesTheDocumentItsChunksAndItsFile()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await UploadPdfAsync(session, "bye.pdf", TestDocuments.Sentences("farewell", 2));
        await DrainQueueAsync();
        var document = await LoadDocumentAsync(id);
        var path = Path.Combine(StorageRoot, document.StorageKey.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue();

        var response = await Client.SendAsync(session, HttpMethod.Delete, $"/api/v1/documents/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Client.GetAsync(session, $"/api/v1/documents/{id}")).ShouldBeProblem(HttpStatusCode.NotFound);
        (await CountChunksAsync(id)).Should().Be(0);
        File.Exists(path).Should().BeFalse();
        (await Client.SendAsync(session, HttpMethod.Delete, $"/api/v1/documents/{id}")).ShouldBeProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        var id = Guid.NewGuid();

        foreach (var (method, uri) in new[] { (HttpMethod.Get, "/api/v1/documents"), (HttpMethod.Get, $"/api/v1/documents/{id}"), (HttpMethod.Delete, $"/api/v1/documents/{id}"), (HttpMethod.Get, "/api/v1/documents/events") })
        {
            using var request = new HttpRequestMessage(method, uri);
            var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

            response.ShouldBeProblem(HttpStatusCode.Unauthorized);
        }
    }
}
