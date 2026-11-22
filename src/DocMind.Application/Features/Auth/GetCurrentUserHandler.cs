using DocMind.Application.Abstractions;
using DocMind.Domain.Common;

namespace DocMind.Application.Features.Auth;

public sealed class GetCurrentUserHandler(IIdentityService identity)
{
    public async Task<Result<UserDto>> HandleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await identity.FindByIdAsync(userId, cancellationToken);
        return user is null ? AuthErrors.UserNotFound : user;
    }
}
