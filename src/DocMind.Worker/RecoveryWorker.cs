using DocMind.Application.Features.Ingestion;
using Microsoft.Extensions.Options;

namespace DocMind.Worker;

internal sealed class RecoveryWorker(
    DocumentRecoverySweeper sweeper,
    IOptions<IngestionSettings> settings,
    TimeProvider time,
    ILogger<RecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(settings.Value.SweepIntervalSeconds), time);

        try
        {
            do
            {
                await SweepAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is shutting down.
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            await sweeper.SweepAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The document recovery sweep failed.");
        }
    }
}
