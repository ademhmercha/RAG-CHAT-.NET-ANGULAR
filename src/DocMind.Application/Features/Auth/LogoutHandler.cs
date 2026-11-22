using DocMind.Application.Abstractions;

namespace DocMind.Application.Features.Auth;

public sealed class LogoutHandler(IRefreshTokenService refreshTokens)
{
    public async Task HandleAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await refreshTokens.RevokeAsync(refreshToken, cancellationToken);
        }
    }
}
