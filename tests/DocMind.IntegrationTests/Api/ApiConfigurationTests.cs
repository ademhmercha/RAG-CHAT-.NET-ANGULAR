using System.Net;
using DocMind.IntegrationTests.Infrastructure;

namespace DocMind.IntegrationTests.Api;

public sealed class ApiConfigurationTests
{
    private const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=docmind;Username=postgres;Timeout=2;Pooling=false";

    [Fact]
    public async Task Ready_ReportsUnavailableWhenTheDatabaseIsDown_WhileLiveStaysHealthy()
    {
        await using var factory = new DocMindApiFactory(UnreachableDatabase, applyMigrations: false);
        using var client = factory.CreateClient();

        var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        live.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ApiDocumentation_IsNotExposedWhenDisabled()
    {
        await using var factory = new DocMindApiFactory(UnreachableDatabase, applyMigrations: false, documentationEnabled: false);
        using var client = factory.CreateClient();

        var openApi = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        var scalar = await client.GetAsync("/scalar/v1", TestContext.Current.CancellationToken);

        openApi.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
        scalar.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
    }
}
