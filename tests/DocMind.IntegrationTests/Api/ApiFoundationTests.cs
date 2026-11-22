using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocMind.IntegrationTests.Infrastructure;

namespace DocMind.IntegrationTests.Api;

[Collection(PostgresFixture.CollectionName)]
public sealed class ApiFoundationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private DocMindApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = new DocMindApiFactory(await postgres.CreateDatabaseAsync());
        _client = _factory.CreateApiClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Live_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("Healthy");
    }

    [Fact]
    public async Task Ready_ReturnsHealthyWhenTheDatabaseIsReachable()
    {
        var response = await _client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("Healthy");
    }

    [Fact]
    public async Task UnknownRoute_ReturnsAProblemDetailsDocumentToAuthenticatedUsers()
    {
        var session = await _client.RegisterAndLoginAsync();

        var response = await _client.GetAsync(session, "/api/v1/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("status").GetInt32().Should().Be(404);
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UnknownRoute_DoesNotRevealItsAbsenceToAnonymousCallers()
    {
        var response = await _client.GetAsync("/api/v1/does-not-exist", TestContext.Current.CancellationToken);

        response.ShouldBeProblem(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnhandledException_ReturnsAnOpaqueProblemDetailsDocument()
    {
        var session = await _client.RegisterAndLoginAsync();

        var response = await _client.GetAsync(session, FaultInjectionStartupFilter.FaultPath);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.Should().NotContain(FaultInjectionStartupFilter.FaultMessage);
        body.Should().NotContain("InvalidOperationException");

        var problem = JsonDocument.Parse(body).RootElement;
        problem.GetProperty("status").GetInt32().Should().Be(500);
        problem.GetProperty("title").GetString().Should().Be("An unexpected error occurred.");
        problem.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OpenApiDocument_DescribesTheApi()
    {
        var response = await _client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        document.TryGetProperty("openapi", out _).Should().BeTrue();
        document.GetProperty("info").GetProperty("title").GetString().Should().Be("DocMind API");
    }

    [Fact]
    public async Task Scalar_ServesTheReferenceUi()
    {
        var response = await _client.GetAsync("/scalar/v1", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
    }
}
