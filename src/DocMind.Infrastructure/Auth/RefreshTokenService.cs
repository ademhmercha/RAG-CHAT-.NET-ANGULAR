using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using DocMind.Application.Abstractions;
using DocMind.Application.Features.Auth;
using DocMind.Domain.Common;
using DocMind.Infrastructure.Identity;
using DocMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocMind.Infrastructure.Auth;

internal sealed class RefreshTokenService(
    AppDbContext db,
    IOptions<JwtOptions> options,
    TimeProvider time,
    ILogger<RefreshTokenService> logger) : IRefreshTokenService
{
    private const int TokenBytes = 48;

    private readonly JwtOptions _options = options.Value;

    public async Task<IssuedRefreshToken> IssueAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var absoluteExpiry = now.AddDays(_options.RefreshTokenAbsoluteDays);

        var (raw, entity) = CreateToken(userId, Guid.CreateVersion7(), now, absoluteExpiry);
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return new IssuedRefreshToken(raw, entity.ExpiresAt);
    }

    public async Task<Result<RotatedRefreshToken>> RotateAsync(string rawToken, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var current = await FindAsync(rawToken, cancellationToken);

        if (current is null)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        if (current.RevokedAt is not null)
        {
            await HandleReuseAsync(current, now, cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        if (current.ExpiresAt <= now || current.AbsoluteExpiresAt <= now)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var revoked = await db.RefreshTokens
                .Where(t => t.Id == current.Id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);

            if (revoked == 0)
            {
                return Result.Failure<RotatedRefreshToken>(AuthErrors.InvalidRefreshToken);
            }

            var (raw, next) = CreateToken(current.UserId, current.FamilyId, now, current.AbsoluteExpiresAt);
            db.RefreshTokens.Add(next);
            await db.SaveChangesAsync(cancellationToken);

            await db.RefreshTokens
                .Where(t => t.Id == current.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ReplacedByTokenId, next.Id), cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return Result.Success(new RotatedRefreshToken(current.UserId, new IssuedRefreshToken(raw, next.ExpiresAt)));
        });
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken)
    {
        var token = await FindAsync(rawToken, cancellationToken);
        if (token is not null)
        {
            await RevokeFamilyAsync(token.FamilyId, time.GetUtcNow(), cancellationToken);
        }
    }

    private static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))).ToLowerInvariant();

    private Task<RefreshToken?> FindAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        return db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
    }

    private (string Raw, RefreshToken Entity) CreateToken(Guid userId, Guid familyId, DateTimeOffset now, DateTimeOffset absoluteExpiry)
    {
        var raw = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        var expiresAt = Min(now.AddDays(_options.RefreshTokenDays), absoluteExpiry);

        return (raw, RefreshToken.Create(userId, familyId, Hash(raw), now, expiresAt, absoluteExpiry));
    }

    private async Task HandleReuseAsync(RefreshToken token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var leeway = TimeSpan.FromSeconds(_options.RefreshReuseLeewaySeconds);
        var rotatedJustNow = token.ReplacedByTokenId is not null && now - token.RevokedAt <= leeway;

        if (rotatedJustNow)
        {
            return;
        }

        logger.LogWarning("Revoked refresh token presented for user {UserId}; revoking token family {FamilyId}.", token.UserId, token.FamilyId);
        await RevokeFamilyAsync(token.FamilyId, now, cancellationToken);
    }

    private Task<int> RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), cancellationToken);

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a <= b ? a : b;
}
