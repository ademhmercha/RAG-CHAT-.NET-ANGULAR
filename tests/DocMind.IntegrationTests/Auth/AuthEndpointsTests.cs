using System.Net;
using DocMind.IntegrationTests.Infrastructure;
using Npgsql;

namespace DocMind.IntegrationTests.Auth;

[Collection(PostgresFixture.CollectionName)]
public sealed class AuthEndpointsTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    [Fact]
    public async Task Register_CreatesTheAccountAndReturnsTheUser()
    {
        var email = AuthClientExtensions.UniqueEmail();

        var response = await Client.RegisterAsync(email, displayName: "Ada");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.ReadJsonAsync();
        body.GetProperty("email").GetString().Should().Be(email);
        body.GetProperty("displayName").GetString().Should().Be("Ada");
        body.GetProperty("id").GetGuid().Should().NotBeEmpty();
        body.EnumerateObject().Select(p => p.Name).Should().NotContain(n => n.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Register_RejectsADuplicateEmailIgnoringCase()
    {
        var email = AuthClientExtensions.UniqueEmail();
        (await Client.RegisterAsync(email)).EnsureSuccessStatusCode();

        var response = await Client.RegisterAsync(email.ToUpperInvariant());

        response.ShouldBeProblem(HttpStatusCode.Conflict);
        (await response.ReadJsonAsync()).GetProperty("code").GetString().Should().Be("auth.email_taken");
    }

    [Fact]
    public async Task Register_ReturnsFieldLevelValidationErrors()
    {
        var response = await Client.RegisterAsync("not-an-email", "weak", "");

        response.ShouldBeProblem(HttpStatusCode.BadRequest);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        errors.TryGetProperty("email", out _).Should().BeTrue();
        errors.TryGetProperty("password", out _).Should().BeTrue();
        errors.TryGetProperty("displayName", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Register_RejectsMalformedJsonWithAProblemDocument()
    {
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/v1/auth/register", content, TestContext.Current.CancellationToken);

        response.ShouldBeProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ReturnsTheAccessTokenInTheBodyAndTheRefreshTokenInAHardenedCookie()
    {
        var email = AuthClientExtensions.UniqueEmail();
        (await Client.RegisterAsync(email)).EnsureSuccessStatusCode();

        var response = await Client.LoginAsync(email);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();

        var body = await response.ReadJsonAsync();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("expiresAt").GetDateTimeOffset().Should().Be(Time.GetUtcNow().AddMinutes(15));
        body.GetProperty("user").GetProperty("email").GetString().Should().Be(email);
        body.EnumerateObject().Select(p => p.Name).Should().NotContain("refreshToken");

        var cookie = AuthClientExtensions.RawRefreshCookieHeader(response)!;
        cookie.Should().ContainEquivalentOf("httponly");
        cookie.Should().ContainEquivalentOf("secure");
        cookie.Should().ContainEquivalentOf("samesite=strict");
        cookie.Should().ContainEquivalentOf("path=/api/v1/auth");
        cookie.Should().ContainEquivalentOf("expires=");
    }

    [Fact]
    public async Task Login_GivesTheSameAnswerForWrongPasswordAndUnknownAccount()
    {
        var email = AuthClientExtensions.UniqueEmail();
        (await Client.RegisterAsync(email)).EnsureSuccessStatusCode();

        var wrongPassword = await Client.LoginAsync(email, "Wr0ngPassword!");
        var unknown = await Client.LoginAsync(AuthClientExtensions.UniqueEmail());

        wrongPassword.ShouldBeProblem(HttpStatusCode.Unauthorized);
        unknown.ShouldBeProblem(HttpStatusCode.Unauthorized);
        var first = await wrongPassword.ReadJsonAsync();
        var second = await unknown.ReadJsonAsync();
        first.GetProperty("code").GetString().Should().Be("auth.invalid_credentials");
        second.GetProperty("detail").GetString().Should().Be(first.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Login_LocksTheAccountAfterRepeatedFailures()
    {
        var email = AuthClientExtensions.UniqueEmail();
        (await Client.RegisterAsync(email)).EnsureSuccessStatusCode();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await Client.LoginAsync(email, "Wr0ngPassword!")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var withCorrectPassword = await Client.LoginAsync(email);

        withCorrectPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await ExpireLockoutAsync(email);
        (await Client.LoginAsync(email)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task ExpireLockoutAsync(string email)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "UPDATE \"AspNetUsers\" SET \"LockoutEnd\" = now() - interval '1 minute' WHERE \"NormalizedEmail\" = @email",
            connection);
        command.Parameters.AddWithValue("email", email.ToUpperInvariant());
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Me_ReturnsTheAuthenticatedUser()
    {
        var session = await Client.RegisterAndLoginAsync(displayName: "Grace");

        var response = await Client.GetAsync(session, "/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("id").GetGuid().Should().Be(session.UserId);
        body.GetProperty("displayName").GetString().Should().Be("Grace");
    }

    [Fact]
    public async Task ProtectedEndpoints_RejectMissingTamperedAndExpiredTokens()
    {
        var session = await Client.RegisterAndLoginAsync();

        var anonymous = await Client.GetAsync("/api/v1/auth/me", TestContext.Current.CancellationToken);
        var tampered = await Client.GetAsync(session with { AccessToken = session.AccessToken[..^4] + "AAAA" }, "/api/v1/auth/me");

        anonymous.ShouldBeProblem(HttpStatusCode.Unauthorized);
        tampered.ShouldBeProblem(HttpStatusCode.Unauthorized);

        Time.Advance(TimeSpan.FromMinutes(16));
        var expired = await Client.GetAsync(session, "/api/v1/auth/me");
        expired.ShouldBeProblem(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_RotatesTheTokenAndIssuesAWorkingAccessToken()
    {
        var session = await Client.RegisterAndLoginAsync();
        Time.Advance(TimeSpan.FromMinutes(5));

        var response = await Client.RefreshAsync(session.RefreshCookie);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var renewed = await AuthClientExtensions.ToSessionAsync(response);
        renewed.RefreshCookie.Should().NotBe(session.RefreshCookie);
        renewed.AccessToken.Should().NotBe(session.AccessToken);
        (await Client.GetAsync(renewed, "/api/v1/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WithoutACookieIsRejectedAndClearsTheCookie()
    {
        var response = await Client.RefreshAsync(refreshCookie: null);

        response.ShouldBeProblem(HttpStatusCode.Unauthorized);
        (await response.ReadJsonAsync()).GetProperty("code").GetString().Should().Be("auth.invalid_refresh_token");
        AuthClientExtensions.RawRefreshCookieHeader(response).Should().ContainEquivalentOf("expires=Thu, 01 Jan 1970");
    }

    [Fact]
    public async Task Refresh_RejectsAnUnknownToken()
    {
        var response = await Client.RefreshAsync("totally-made-up-token");

        response.ShouldBeProblem(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ReusingARotatedTokenRevokesTheWholeSession()
    {
        var session = await Client.RegisterAndLoginAsync();
        var rotated = await AuthClientExtensions.ToSessionAsync(await Client.RefreshAsync(session.RefreshCookie));

        Time.Advance(TimeSpan.FromSeconds(30));
        var replay = await Client.RefreshAsync(session.RefreshCookie);

        replay.ShouldBeProblem(HttpStatusCode.Unauthorized);
        (await Client.RefreshAsync(rotated.RefreshCookie)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ConcurrentReuseInsideTheLeewayDoesNotKillTheSession()
    {
        var session = await Client.RegisterAndLoginAsync();
        var rotated = await AuthClientExtensions.ToSessionAsync(await Client.RefreshAsync(session.RefreshCookie));

        Time.Advance(TimeSpan.FromSeconds(2));
        var raceLoser = await Client.RefreshAsync(session.RefreshCookie);

        raceLoser.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.RefreshAsync(rotated.RefreshCookie)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_RejectsAnExpiredToken()
    {
        var session = await Client.RegisterAndLoginAsync();

        Time.Advance(TimeSpan.FromDays(8));

        (await Client.RefreshAsync(session.RefreshCookie)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_StopsAtTheAbsoluteSessionLifetime()
    {
        var session = await Client.RegisterAndLoginAsync();
        var current = session;

        for (var day = 0; day < 4; day++)
        {
            Time.Advance(TimeSpan.FromDays(6));
            var response = await Client.RefreshAsync(current.RefreshCookie);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            current = await AuthClientExtensions.ToSessionAsync(response);
        }

        Time.Advance(TimeSpan.FromDays(7));
        (await Client.RefreshAsync(current.RefreshCookie)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_RevokesTheSessionAndClearsTheCookie()
    {
        var session = await Client.RegisterAndLoginAsync();

        var response = await Client.LogoutAsync(session.RefreshCookie);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        AuthClientExtensions.RawRefreshCookieHeader(response).Should().ContainEquivalentOf("expires=Thu, 01 Jan 1970");
        (await Client.RefreshAsync(session.RefreshCookie)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithoutACookieStillSucceeds()
    {
        var response = await Client.LogoutAsync(refreshCookie: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AuthEndpoints_AreUnderTheVersionedRoute()
    {
        var session = await Client.RegisterAndLoginAsync();

        var response = await Client.SendAsync(session, HttpMethod.Post, "/api/auth/login");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
