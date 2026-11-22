using DocMind.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocMind.Application.Features.Ingestion;

public sealed class IngestionQueueProcessor(
    IMessageQueue queue,
    IServiceScopeFactory scopes,
    IOptions<IngestionSettings> settings,
    ILogger<IngestionQueueProcessor> logger)
{
    private const string PoisonFailureMessage = "Processing failed repeatedly and was abandoned.";
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    public async Task<int> ProcessBatchAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        var options = settings.Value;
        var messages = await queue.ReceiveAsync(options.MaxConcurrency, wait, cancellationToken);

        await Parallel.ForEachAsync(
            messages,
            new ParallelOptions { MaxDegreeOfParallelism = options.MaxConcurrency, CancellationToken = cancellationToken },
            async (message, token) => await ProcessAsync(message, token));

        return messages.Count;
    }

    private static TimeSpan BackoffFor(QueueMessage message, int baseMilliseconds)
    {
        var factor = Math.Pow(2, Math.Max(0, message.DeliveryCount - 1));
        var delay = TimeSpan.FromMilliseconds(baseMilliseconds * factor);
        return delay > MaxBackoff ? MaxBackoff : delay;
    }

    private async Task ProcessAsync(QueueMessage message, CancellationToken cancellationToken)
    {
        var parsed = IngestionMessage.TryParse(message.Body);
        if (parsed is null)
        {
            logger.LogWarning("Dropping malformed ingestion message {MessageId}.", message.Id);
            await queue.CompleteAsync(message, cancellationToken);
            return;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IngestDocumentHandler>();
            await handler.HandleAsync(parsed.DocumentId, cancellationToken);
            await queue.CompleteAsync(message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await queue.AbandonAsync(message, TimeSpan.Zero, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Processing message {MessageId} for document {DocumentId} failed (delivery {Delivery}).", message.Id, parsed.DocumentId, message.DeliveryCount);
            await HandleFailureAsync(message, parsed, cancellationToken);
        }
    }

    private async Task HandleFailureAsync(QueueMessage message, IngestionMessage parsed, CancellationToken cancellationToken)
    {
        var options = settings.Value;

        if (message.DeliveryCount < options.MaxDeliveries)
        {
            await queue.AbandonAsync(message, BackoffFor(message, options.RetryBaseDelayMilliseconds), cancellationToken);
            return;
        }

        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IngestDocumentHandler>();
            await handler.FailAsync(parsed.DocumentId, PoisonFailureMessage, cancellationToken);
            await queue.CompleteAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Marking document {DocumentId} as failed after repeated errors did not succeed.", parsed.DocumentId);
            await queue.AbandonAsync(message, BackoffFor(message, options.RetryBaseDelayMilliseconds), cancellationToken);
        }
    }
}
