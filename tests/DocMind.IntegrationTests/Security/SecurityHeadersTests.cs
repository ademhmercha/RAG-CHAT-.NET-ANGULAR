using System.Net;
using DocMind.IntegrationTests.Infrastructure;

namespace DocMind.IntegrationTests.Security;

[Collection(PostgresFixture.CollectionName)]
public sealed class SecurityHeadersTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/api/v1/auth/me")]
    [InlineData("/does-not-exist")]
    public async Task EveryResponseCarriesTheHardeningHeaders(string path)
    {
        var response = await Client.GetAsync(path, TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("X-Frame-Options").Should().Equal("DENY");
        response.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle().Which.Should().Contain("default-src 'none'").And.Contain("frame-ancestors 'none'");
        response.Headers.GetValues("Permissions-Policy").Should().ContainSingle();
    }

    [Fact]
    public async Task TheDocumentationUiIsNotBlockedByTheApiContentSecurityPolicy()
    {
        var response = await Client.GetAsync("/scalar/v1", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Content-Security-Policy").Should().BeFalse();
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
    }

    [Fact]
    public async Task ErrorResponsesAreNotCacheableByDefaultProxies()
    {
        var session = await Client.RegisterAndLoginAsync();
        var login = await Client.LoginAsync(session.Email);

        login.Headers.CacheControl!.NoStore.Should().BeTrue();
    }
}
