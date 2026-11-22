namespace DocMind.Application.Features.Auth;

public sealed record UserDto(Guid Id, string Email, string DisplayName);

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public sealed record IssuedRefreshToken(string Value, DateTimeOffset ExpiresAt);

public sealed record RotatedRefreshToken(Guid UserId, IssuedRefreshToken Token);

public sealed record AuthResult(AccessToken AccessToken, UserDto User, IssuedRefreshToken RefreshToken);
