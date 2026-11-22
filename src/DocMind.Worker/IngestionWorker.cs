using DocMind.Application.Features.Ingestion;
using Microsoft.Extensions.Options;

namespace DocMind.Worker;

internal sealed class IngestionWorker(
    IngestionQueueProcessor processor,
    IOptions<IngestionSettings> settings,
    ILogger<IngestionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Ingestion worker started.");
        var wait = TimeSpan.FromSeconds(settings.Value.PollWaitSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await processor.ProcessBatchAsync(wait, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Polling the ingestion queue failed; retrying shortly.");
                await Task.Delay(ErrorBackoff, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }

        logger.LogInformation("Ingestion worker stopped.");
    }
}
