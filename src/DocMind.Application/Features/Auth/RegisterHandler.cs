using DocMind.Application.Abstractions;
using DocMind.Domain.Common;

namespace DocMind.Application.Features.Auth;

public sealed class RegisterHandler(IIdentityService identity)
{
    public Task<Result<UserDto>> HandleAsync(RegisterRequest request, CancellationToken cancellationToken) =>
        identity.RegisterAsync(request.Email.Trim(), request.Password, request.DisplayName.Trim(), cancellationToken);
}
