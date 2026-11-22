using System.Net;
using DocMind.IntegrationTests.Infrastructure;
using DocMind.TestSupport;

namespace DocMind.IntegrationTests.Security;

[Collection(PostgresFixture.CollectionName)]
public sealed class RateLimitingTests(PostgresFixture postgres) : DocumentApiTestBase(postgres)
{
    protected override IReadOnlyDictionary<string, string?>? Settings
    {
        get
        {
            var settings = new Dictionary<string, string?>(base.Settings!, StringComparer.Ordinal)
            {
                ["RateLimiting:Enabled"] = "true",
                ["RateLimiting:Global:PermitLimit"] = "1000",
                ["RateLimiting:Auth:PermitLimit"] = "4",
                ["RateLimiting:Auth:WindowSeconds"] = "60",
                ["RateLimiting:Chat:PermitLimit"] = "2",
                ["RateLimiting:Chat:WindowSeconds"] = "60",
                ["RateLimiting:Upload:PermitLimit"] = "2",
                ["RateLimiting:Upload:WindowSeconds"] = "60",
            };
            return settings;
        }
    }

    [Fact]
    public async Task Auth_IsStrictlyLimitedPerClientAddress()
    {
        var email = AuthClientExtensions.UniqueEmail();
        (await Client.RegisterAsync(email)).EnsureSuccessStatusCode();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            (await Client.LoginAsync(email, "Wr0ngPassword!")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var limited = await Client.LoginAsync(email);

        limited.ShouldBeProblem(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter!.Delta!.Value.TotalSeconds.Should().BeGreaterThan(0);
        (await limited.ReadJsonAsync()).GetProperty("code").GetString().Should().Be("rate_limit.exceeded");
    }

    [Fact]
    public async Task Auth_LimitCoversRegistrationRefreshAndLogoutToo()
    {
        for (var i = 0; i < 4; i++)
        {
            await Client.RefreshAsync("unknown");
        }

        (await Client.RegisterAsync(AuthClientExtensions.UniqueEmail())).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await Client.LogoutAsync(null)).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Auth_DoesNotLimitTheCurrentUserEndpoint()
    {
        var session = await Client.RegisterAndLoginAsync();

        for (var i = 0; i < 10; i++)
        {
            (await Client.GetAsync(session, "/api/v1/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Chat_IsLimitedPerUserNotPerAddress()
    {
        var heavy = await Client.RegisterAndLoginAsync();
        var light = await Client.RegisterAndLoginAsync();
        var heavyConversation = await Client.CreateConversationAsync(heavy);
        var lightConversation = await Client.CreateConversationAsync(light);

        for (var i = 0; i < 2; i++)
        {
            using var accepted = await Client.SendChatAsync(heavy, heavyConversation, $"message {i}");
            accepted.StatusCode.Should().Be(HttpStatusCode.OK);
            await SseReader.ReadAsync(accepted);
        }

        using var limited = await Client.SendChatAsync(heavy, heavyConversation, "one too many");
        using var unaffected = await Client.SendChatAsync(light, lightConversation, "my first message");

        limited.ShouldBeProblem(HttpStatusCode.TooManyRequests);
        unaffected.StatusCode.Should().Be(HttpStatusCode.OK);
        Chat.Calls.Should().HaveCount(3);
    }

    [Fact]
    public async Task Upload_IsLimitedPerUser()
    {
        var session = await Client.RegisterAndLoginAsync();
        var other = await Client.RegisterAndLoginAsync();
        var pdf = TestDocuments.Pdf("limited");

        (await UploadAsync(session, "a.pdf", pdf, TestDocuments.PdfContentType)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await UploadAsync(session, "b.pdf", pdf, TestDocuments.PdfContentType)).StatusCode.Should().Be(HttpStatusCode.Created);

        (await UploadAsync(session, "c.pdf", pdf, TestDocuments.PdfContentType)).ShouldBeProblem(HttpStatusCode.TooManyRequests);
        (await UploadAsync(other, "d.pdf", pdf, TestDocuments.PdfContentType)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task HealthProbesAreNeverRateLimited()
    {
        for (var i = 0; i < 30; i++)
        {
            (await Client.GetAsync("/health/live", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task RejectedResponsesStillCarrySecurityHeaders()
    {
        for (var i = 0; i < 4; i++)
        {
            await Client.RefreshAsync("unknown");
        }

        var limited = await Client.RefreshAsync("unknown");

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
    }
}

[Collection(PostgresFixture.CollectionName)]
public sealed class RateLimitingWindowTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    protected override IReadOnlyDictionary<string, string?>? Settings => new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["RateLimiting:Enabled"] = "true",
        ["RateLimiting:Auth:PermitLimit"] = "2",
        ["RateLimiting:Auth:WindowSeconds"] = "5",
    };

    [Fact]
    public async Task Auth_AllowsRequestsAgainWhenTheWindowRollsOver()
    {
        await Client.RefreshAsync("x");
        await Client.RefreshAsync("x");
        (await Client.RefreshAsync("x")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        await Task.Delay(TimeSpan.FromMilliseconds(5500), TestContext.Current.CancellationToken);

        (await Client.RefreshAsync("x")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

[Collection(PostgresFixture.CollectionName)]
public sealed class RateLimitingGlobalTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    protected override IReadOnlyDictionary<string, string?>? Settings => new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["RateLimiting:Enabled"] = "true",
        ["RateLimiting:Global:PermitLimit"] = "5",
        ["RateLimiting:Global:WindowSeconds"] = "60",
    };

    [Fact]
    public async Task GlobalLimit_AppliesToEveryApiRequestButNotToProbes()
    {
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 8; i++)
        {
            statuses.Add((await Client.GetAsync("/api/v1/conversations", TestContext.Current.CancellationToken)).StatusCode);
        }

        statuses.Take(5).Should().OnlyContain(s => s == HttpStatusCode.Unauthorized);
        statuses.Skip(5).Should().OnlyContain(s => s == HttpStatusCode.TooManyRequests);
        (await Client.GetAsync("/health/ready", TestContext.Current.CancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

[Collection(PostgresFixture.CollectionName)]
public sealed class RateLimitingDisabledTests(PostgresFixture postgres) : ApiTestBase(postgres)
{
    protected override IReadOnlyDictionary<string, string?>? Settings => new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["RateLimiting:Enabled"] = "false",
        ["RateLimiting:Auth:PermitLimit"] = "1",
    };

    [Fact]
    public async Task WhenDisabled_NothingIsLimited()
    {
        for (var i = 0; i < 10; i++)
        {
            (await Client.RefreshAsync("x")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
