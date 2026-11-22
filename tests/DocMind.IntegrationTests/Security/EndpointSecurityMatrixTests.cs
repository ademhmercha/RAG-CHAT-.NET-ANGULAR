using System.Net;
using DocMind.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace DocMind.IntegrationTests.Security;

[Collection(PostgresFixture.CollectionName)]
public sealed class EndpointSecurityMatrixTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    private static readonly string[] AnonymousRoutes =
    [
        "POST api/v{version:apiVersion}/auth/register",
        "POST api/v{version:apiVersion}/auth/login",
        "POST api/v{version:apiVersion}/auth/refresh",
        "POST api/v{version:apiVersion}/auth/logout",
    ];

    private List<(string Method, RoutePattern Pattern, bool Anonymous)> ApiEndpoints() =>
        Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.TrimStart('/').StartsWith("api/", StringComparison.Ordinal) == true)
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? []).Select(method =>
                (method, e.RoutePattern, e.Metadata.GetMetadata<IAllowAnonymous>() is not null)))
            .ToList();

    private static string Materialize(RoutePattern pattern)
    {
        var path = "/" + string.Join('/', pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternParameterPart { Name: "version" } => "1",
            RoutePatternParameterPart => Guid.NewGuid().ToString(),
            _ => string.Empty,
        }))));

        return path;
    }

    [Fact]
    public void TheApiExposesTheExpectedEndpoints()
    {
        var endpoints = ApiEndpoints().Select(e => $"{e.Method} {e.Pattern.RawText!.TrimStart('/')}").ToList();

        endpoints.Should().HaveCount(16, "the API contract defines exactly 16 operations");
        endpoints.Should().Contain(
        [
            "POST api/v{version:apiVersion}/auth/login",
            "GET api/v{version:apiVersion}/documents/events",
            "POST api/v{version:apiVersion}/conversations/{id:guid}/messages",
        ]);
    }

    [Fact]
    public void OnlyTheCredentialEndpointsAreAnonymous()
    {
        var anonymous = ApiEndpoints().Where(e => e.Anonymous).Select(e => $"{e.Method} {e.Pattern.RawText!.TrimStart('/')}").ToList();

        anonymous.Should().BeEquivalentTo(AnonymousRoutes);
    }

    [Fact]
    public async Task EveryProtectedEndpointRejectsAnonymousCallers()
    {
        var protectedEndpoints = ApiEndpoints().Where(e => !e.Anonymous).ToList();
        protectedEndpoints.Should().NotBeEmpty();

        foreach (var (method, pattern, _) in protectedEndpoints)
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), Materialize(pattern));
            var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, $"{method} {pattern.RawText} must require authentication");
        }
    }

    [Fact]
    public async Task EveryProtectedEndpointRejectsForgedTokens()
    {
        var session = await Client.RegisterAndLoginAsync();
        var parts = session.AccessToken.Split('.');
        var noneHeader = Convert.ToBase64String("{\"alg\":\"none\",\"typ\":\"JWT\"}"u8).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var forged = new[]
        {
            $"{noneHeader}.{parts[1]}.",
            $"{parts[0]}.{parts[1]}.{new string('A', parts[2].Length)}",
            session.AccessToken[..^2] + (session.AccessToken[^1] == 'A' ? "BB" : "AA"),
        };

        foreach (var token in forged)
        {
            var response = await Client.GetAsync(session with { AccessToken = token }, "/api/v1/auth/me");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
