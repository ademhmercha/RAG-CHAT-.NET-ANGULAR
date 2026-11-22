using System.Threading.Channels;
using DocMind.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace DocMind.Infrastructure.Messaging;

internal sealed class ChannelMessageQueue(TimeProvider time, ILogger<ChannelMessageQueue> logger) : IMessageQueue, IDisposable
{
    private readonly Channel<QueueMessage> _channel = Channel.CreateUnbounded<QueueMessage>();
    private readonly CancellationTokenSource _shutdown = new();

    public async Task PublishAsync(string body, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid().ToString("N");
        await _channel.Writer.WriteAsync(new QueueMessage(id, body, 1, id), cancellationToken);
    }

    public async Task<IReadOnlyList<QueueMessage>> ReceiveAsync(int maxMessages, TimeSpan waitTime, CancellationToken cancellationToken)
    {
        if (!_channel.Reader.TryRead(out var first))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(waitTime);

            try
            {
                if (!await _channel.Reader.WaitToReadAsync(timeout.Token) || !_channel.Reader.TryRead(out first))
                {
                    return [];
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return [];
            }
        }

        var messages = new List<QueueMessage> { first };
        while (messages.Count < maxMessages && _channel.Reader.TryRead(out var next))
        {
            messages.Add(next);
        }

        return messages;
    }

    public Task CompleteAsync(QueueMessage message, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task AbandonAsync(QueueMessage message, TimeSpan delay, CancellationToken cancellationToken)
    {
        _ = RequeueAsync(message with { DeliveryCount = message.DeliveryCount + 1 }, delay);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
    }

    private async Task RequeueAsync(QueueMessage message, TimeSpan delay)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, time, _shutdown.Token);
            }

            await _channel.Writer.WriteAsync(message, _shutdown.Token);
        }
        catch (OperationCanceledException exception)
        {
            logger.LogDebug(exception, "Requeue of message {MessageId} cancelled by shutdown.", message.Id);
        }
    }
}
