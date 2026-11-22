using DocMind.Application.Features.Auth;

namespace DocMind.Api.Features.Auth;

public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User)
{
    public static AuthResponse From(AuthResult result) =>
        new(result.AccessToken.Value, result.AccessToken.ExpiresAt, result.User);
}
