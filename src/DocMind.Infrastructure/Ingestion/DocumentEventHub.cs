using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using DocMind.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using DocMind.Infrastructure.Persistence;
using Npgsql;

namespace DocMind.Infrastructure.Ingestion;

internal sealed class DocumentEventHub(string connectionString, ILogger<DocumentEventHub> logger)
    : BackgroundService, IDocumentEventSubscriber
{
    private const int SubscriberBuffer = 100;
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(3);
    private static readonly int KeepAliveSeconds = 30;

    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();
    private readonly TaskCompletionSource _listening = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<IDocumentSubscription> SubscribeAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await _listening.Task.WaitAsync(ReadyTimeout, cancellationToken);
        }
        catch (TimeoutException exception)
        {
            logger.LogWarning(exception, "Subscribing before the notification listener is ready; early events may be missed.");
        }

        var channel = Channel.CreateBounded<DocumentStatusChanged>(
            new BoundedChannelOptions(SubscriberBuffer) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

        var id = Guid.NewGuid();
        _subscribers[id] = new Subscriber(userId, channel.Writer);

        return new Subscription(channel.Reader, () => _subscribers.TryRemove(id, out _));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ListenAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The document notification listener failed; reconnecting.");
            }

            await Task.Delay(ReconnectDelay, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        var builder = new NpgsqlConnectionStringBuilder(DbContextOptionsBuilderExtensions.WithoutGssProbing(connectionString)) { Pooling = false, KeepAlive = KeepAliveSeconds };

        await using var connection = new NpgsqlConnection(builder.ConnectionString);
        connection.Notification += (_, args) => Dispatch(args.Payload);

        await connection.OpenAsync(cancellationToken);

        await using (var listen = new NpgsqlCommand($"LISTEN {PostgresDocumentStatusNotifier.ChannelName}", connection))
        {
            await listen.ExecuteNonQueryAsync(cancellationToken);
        }

        _listening.TrySetResult();

        while (!cancellationToken.IsCancellationRequested)
        {
            await connection.WaitAsync(cancellationToken);
        }
    }

    private void Dispatch(string payload)
    {
        DocumentStatusChanged? change;

        try
        {
            change = JsonSerializer.Deserialize<DocumentStatusChanged>(payload, PostgresDocumentStatusNotifier.JsonOptions);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Ignoring a malformed document notification.");
            return;
        }

        if (change is null)
        {
            return;
        }

        foreach (var subscriber in _subscribers.Values.Where(s => s.UserId == change.UserId))
        {
            subscriber.Writer.TryWrite(change);
        }
    }

    private sealed record Subscriber(Guid UserId, ChannelWriter<DocumentStatusChanged> Writer);

    private sealed class Subscription(ChannelReader<DocumentStatusChanged> reader, Action onDispose) : IDocumentSubscription
    {
        public async IAsyncEnumerable<DocumentStatusChanged> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var change in reader.ReadAllAsync(cancellationToken))
            {
                yield return change;
            }
        }

        public ValueTask DisposeAsync()
        {
            onDispose();
            return ValueTask.CompletedTask;
        }
    }
}
