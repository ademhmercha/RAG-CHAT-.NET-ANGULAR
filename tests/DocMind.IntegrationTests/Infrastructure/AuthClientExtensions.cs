using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace DocMind.IntegrationTests.Infrastructure;

public sealed record TestSession(string Email, Guid UserId, string AccessToken, string RefreshCookie);

public static class AuthClientExtensions
{
    public const string Password = "Str0ngPassw0rd";
    public const string RefreshCookieName = "docmind_refresh";

    public static string UniqueEmail(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@docmind.test";

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, string email, string password = Password, string displayName = "Test User") =>
        client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, TestContext.Current.CancellationToken);

    public static async Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string? refreshCookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        if (refreshCookie is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshCookie}");
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<HttpResponseMessage> LogoutAsync(this HttpClient client, string? refreshCookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        if (refreshCookie is not null)
        {
            request.Headers.Add("Cookie", $"{RefreshCookieName}={refreshCookie}");
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static async Task<TestSession> RegisterAndLoginAsync(this HttpClient client, string? email = null, string displayName = "Test User")
    {
        email ??= UniqueEmail();

        (await client.RegisterAsync(email, displayName: displayName)).EnsureSuccessStatusCode();
        var response = await client.LoginAsync(email);
        response.EnsureSuccessStatusCode();

        return await ToSessionAsync(response);
    }

    public static async Task<TestSession> ToSessionAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

        return new TestSession(
            body.GetProperty("user").GetProperty("email").GetString()!,
            body.GetProperty("user").GetProperty("id").GetGuid(),
            body.GetProperty("accessToken").GetString()!,
            ReadRefreshCookie(response)!);
    }

    public static string? ReadRefreshCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        var header = values.FirstOrDefault(v => v.StartsWith($"{RefreshCookieName}=", StringComparison.Ordinal));
        return header?.Split(';')[0][(RefreshCookieName.Length + 1)..];
    }

    public static string? RawRefreshCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith($"{RefreshCookieName}=", StringComparison.Ordinal))
            : null;

    public static HttpRequestMessage Authorized(this TestSession session, HttpMethod method, string uri, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return request;
    }

    public static Task<HttpResponseMessage> SendAsync(this HttpClient client, TestSession session, HttpMethod method, string uri, HttpContent? content = null)
    {
        var request = session.Authorized(method, uri, content);
        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> GetAsync(this HttpClient client, TestSession session, string uri) =>
        client.SendAsync(session, HttpMethod.Get, uri);

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, TestSession session, string uri, T body) =>
        client.SendAsync(session, HttpMethod.Post, uri, JsonContent.Create(body));

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);

    public static void ShouldBeProblem(this HttpResponseMessage response, HttpStatusCode status) =>
        response.Should().Match<HttpResponseMessage>(r =>
            r.StatusCode == status && r.Content.Headers.ContentType!.MediaType == "application/problem+json");
}
