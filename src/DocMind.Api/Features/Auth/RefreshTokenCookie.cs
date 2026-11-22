using DocMind.Application.Features.Auth;

namespace DocMind.Api.Features.Auth;

internal static class RefreshTokenCookie
{
    public const string Path = "/api/v1/auth";

    public static void Append(HttpResponse response, IssuedRefreshToken token, JwtOptions options) =>
        response.Cookies.Append(options.RefreshCookieName, token.Value, CreateOptions(token.ExpiresAt));

    public static void Delete(HttpResponse response, JwtOptions options) =>
        response.Cookies.Delete(options.RefreshCookieName, CreateOptions(expires: null));

    public static string? Read(HttpRequest request, JwtOptions options) =>
        request.Cookies.TryGetValue(options.RefreshCookieName, out var value) ? value : null;

    private static CookieOptions CreateOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        IsEssential = true,
        Expires = expires,
    };
}
