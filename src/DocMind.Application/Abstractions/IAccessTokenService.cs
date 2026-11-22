using DocMind.Application.Features.Auth;

namespace DocMind.Application.Abstractions;

public interface IAccessTokenService
{
    AccessToken Create(UserDto user);
}
