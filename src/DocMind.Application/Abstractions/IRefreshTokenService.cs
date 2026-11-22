using DocMind.Application.Features.Auth;
using DocMind.Domain.Common;

namespace DocMind.Application.Abstractions;

public interface IRefreshTokenService
{
    Task<IssuedRefreshToken> IssueAsync(Guid userId, CancellationToken cancellationToken);

    Task<Result<RotatedRefreshToken>> RotateAsync(string rawToken, CancellationToken cancellationToken);

    Task RevokeAsync(string rawToken, CancellationToken cancellationToken);
}
