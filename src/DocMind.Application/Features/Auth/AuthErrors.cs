using DocMind.Domain.Common;

namespace DocMind.Application.Features.Auth;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("auth.invalid_credentials", "The email or password is incorrect.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("auth.invalid_refresh_token", "The refresh token is missing, expired or revoked.");

    public static readonly Error EmailTaken =
        Error.Conflict("auth.email_taken", "An account with this email already exists.");

    public static readonly Error UserNotFound =
        Error.NotFound("auth.user_not_found", "The user does not exist.");

    public static Error InvalidRegistration(string description) =>
        Error.Validation("auth.invalid_registration", description);
}
