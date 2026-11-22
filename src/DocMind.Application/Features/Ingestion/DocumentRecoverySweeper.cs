using DocMind.Application.Abstractions;
using DocMind.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocMind.Application.Features.Ingestion;

public sealed class DocumentRecoverySweeper(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IOptions<IngestionSettings> settings,
    ILogger<DocumentRecoverySweeper> logger)
{
    private const int BatchSize = 50;

    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var options = settings.Value;

        var now = time.GetUtcNow();
        var pendingBefore = now - TimeSpan.FromSeconds(options.PendingGraceSeconds);
        var staleBefore = now - TimeSpan.FromMinutes(options.LeaseMinutes);

        var ids = await db.Documents
            .AsNoTracking()
            .Where(d => (d.Status == DocumentStatus.Pending && d.CreatedAt < pendingBefore)
                || (d.Status == DocumentStatus.Processing && d.UpdatedAt < staleBefore))
            .OrderBy(d => d.UpdatedAt)
            .Select(d => d.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return 0;
        }

        logger.LogInformation("Recovering {Count} document(s) that were not processed.", ids.Count);

        await Parallel.ForEachAsync(
            ids,
            new ParallelOptions { MaxDegreeOfParallelism = options.MaxConcurrency, CancellationToken = cancellationToken },
            async (id, token) => await RecoverAsync(id, token));

        return ids.Count;
    }

    private async Task RecoverAsync(Guid documentId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IngestDocumentHandler>();
            await handler.HandleAsync(documentId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Recovering document {DocumentId} failed.", documentId);
        }
    }
}
