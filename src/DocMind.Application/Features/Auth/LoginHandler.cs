using DocMind.Application.Abstractions;
using DocMind.Domain.Common;

namespace DocMind.Application.Features.Auth;

public sealed class LoginHandler(
    IIdentityService identity,
    IAccessTokenService accessTokens,
    IRefreshTokenService refreshTokens)
{
    public async Task<Result<AuthResult>> HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await identity.VerifyCredentialsAsync(request.Email.Trim(), request.Password, cancellationToken);
        if (user.IsFailure)
        {
            return user.Error;
        }

        var accessToken = accessTokens.Create(user.Value);
        var refreshToken = await refreshTokens.IssueAsync(user.Value.Id, cancellationToken);

        return new AuthResult(accessToken, user.Value, refreshToken);
    }
}
