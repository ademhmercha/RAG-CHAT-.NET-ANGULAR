using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DocMind.Infrastructure.Persistence;

internal sealed class DatabaseHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    private const string ProbeSql = "SELECT count(*) FROM pg_extension WHERE extname = 'vector'";

    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            await dbContext.Database.OpenConnectionAsync(timeout.Token);

            try
            {
                await using var command = dbContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = ProbeSql;
                var result = await command.ExecuteScalarAsync(timeout.Token);

                return Convert.ToInt32(result, CultureInfo.InvariantCulture) == 1
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("The pgvector extension is not installed.");
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("The database is unreachable.", exception);
        }
    }
}
