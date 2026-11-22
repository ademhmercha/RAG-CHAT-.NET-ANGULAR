using DocMind.Application.Abstractions;
using DocMind.Application.Features.Auth;
using DocMind.Domain.Common;
using DocMind.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DocMind.Infrastructure.Auth;

internal sealed class IdentityService(UserManager<ApplicationUser> users, TimeProvider time) : IIdentityService
{
    private static readonly PasswordHasher<ApplicationUser> DummyHasher = new();
    private static readonly string DummyHash = DummyHasher.HashPassword(new ApplicationUser(), "not-a-real-password");

    public async Task<Result<UserDto>> RegisterAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            DisplayName = displayName,
            CreatedAt = time.GetUtcNow(),
        };

        var result = await users.CreateAsync(user, password);
        if (result.Succeeded)
        {
            return ToDto(user);
        }

        if (result.Errors.Any(e => e.Code is nameof(IdentityErrorDescriber.DuplicateUserName) or nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            return AuthErrors.EmailTaken;
        }

        return AuthErrors.InvalidRegistration(string.Join(' ', result.Errors.Select(e => e.Description)));
    }

    public async Task<Result<UserDto>> VerifyCredentialsAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            DummyHasher.VerifyHashedPassword(new ApplicationUser(), DummyHash, password);
            return AuthErrors.InvalidCredentials;
        }

        if (await users.IsLockedOutAsync(user))
        {
            return AuthErrors.InvalidCredentials;
        }

        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            return AuthErrors.InvalidCredentials;
        }

        await users.ResetAccessFailedCountAsync(user);
        return ToDto(user);
    }

    public async Task<UserDto?> FindByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null ? null : ToDto(user);
    }

    private static UserDto ToDto(ApplicationUser user) => new(user.Id, user.Email ?? string.Empty, user.DisplayName);
}
