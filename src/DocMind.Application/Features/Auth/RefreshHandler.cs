using DocMind.Application.Abstractions;
using DocMind.Domain.Common;

namespace DocMind.Application.Features.Auth;

public sealed class RefreshHandler(
    IIdentityService identity,
    IAccessTokenService accessTokens,
    IRefreshTokenService refreshTokens)
{
    public async Task<Result<AuthResult>> HandleAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var rotated = await refreshTokens.RotateAsync(refreshToken, cancellationToken);
        if (rotated.IsFailure)
        {
            return rotated.Error;
        }

        var user = await identity.FindByIdAsync(rotated.Value.UserId, cancellationToken);
        if (user is null)
        {
            await refreshTokens.RevokeAsync(rotated.Value.Token.Value, cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        return new AuthResult(accessTokens.Create(user), user, rotated.Value.Token);
    }
}
