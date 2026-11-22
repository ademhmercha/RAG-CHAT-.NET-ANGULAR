using DocMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocMind.Infrastructure.Maintenance;

internal sealed class ExpiredTokenPurgeService(IServiceScopeFactory scopes, TimeProvider time, ILogger<ExpiredTokenPurgeService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);

        try
        {
            do
            {
                await PurgeAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is shutting down.
        }
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var threshold = time.GetUtcNow() - GracePeriod;

            var removed = await db.RefreshTokens
                .Where(t => t.AbsoluteExpiresAt < threshold || t.ExpiresAt < threshold || (t.RevokedAt != null && t.RevokedAt < threshold))
                .ExecuteDeleteAsync(cancellationToken);

            if (removed > 0)
            {
                logger.LogInformation("Purged {Count} expired refresh token(s).", removed);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Purging expired refresh tokens failed.");
        }
    }
}
