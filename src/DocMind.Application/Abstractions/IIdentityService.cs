using DocMind.Application.Features.Auth;
using DocMind.Domain.Common;

namespace DocMind.Application.Abstractions;

public interface IIdentityService
{
    Task<Result<UserDto>> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken);

    Task<Result<UserDto>> VerifyCredentialsAsync(string email, string password, CancellationToken cancellationToken);

    Task<UserDto?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);
}
