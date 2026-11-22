using System.Text;
using Microsoft.AspNetCore.Http.Features;

namespace DocMind.Api.Common;

internal readonly record struct SseMessage(string Event, string Data);

internal sealed class SseOptions
{
    public const string SectionName = "Sse";

    public int HeartbeatMilliseconds { get; init; } = 15000;
}

internal sealed class ServerSentEventsResult(
    IAsyncEnumerable<SseMessage> messages,
    Func<Exception, SseMessage> onFault,
    TimeSpan heartbeat) : IResult
{
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(10);
    private static readonly byte[] KeepAlive = Encoding.UTF8.GetBytes(": keep-alive\n\n");

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var response = httpContext.Response;
        var cancellationToken = httpContext.RequestAborted;

        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache, no-store, no-transform";
        response.Headers["X-Accel-Buffering"] = "no";
        httpContext.Features.GetRequiredFeature<IHttpResponseBodyFeature>().DisableBuffering();

        try
        {
            await response.StartAsync(cancellationToken);
            await PumpAsync(response, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException || (exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            // The client disconnected; there is nothing left to write.
        }
    }

    internal static byte[] Format(SseMessage message)
    {
        var builder = new StringBuilder();
        builder.Append("event: ").Append(message.Event).Append('\n');

        foreach (var line in message.Data.Split('\n'))
        {
            builder.Append("data: ").Append(line.TrimEnd('\r')).Append('\n');
        }

        builder.Append('\n');
        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private async Task PumpAsync(HttpResponse response, CancellationToken cancellationToken)
    {
        using var producer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var enumerator = messages.GetAsyncEnumerator(producer.Token);
        var next = MoveNextAsync(enumerator);

        try
        {
            while (true)
            {
                using var tick = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var delay = Task.Delay(heartbeat, tick.Token);
                var completed = await Task.WhenAny(next, delay);

                if (completed == delay)
                {
                    await WriteAsync(response, KeepAlive, cancellationToken);
                    continue;
                }

                await tick.CancelAsync();

                bool hasNext;
                try
                {
                    hasNext = await next;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    await WriteAsync(response, Format(onFault(exception)), cancellationToken);
                    return;
                }

                if (!hasNext)
                {
                    return;
                }

                await WriteAsync(response, Format(enumerator.Current), cancellationToken);
                next = MoveNextAsync(enumerator);
            }
        }
        finally
        {
            await producer.CancelAsync();
            await DrainAsync(enumerator, next);
        }
    }

    private static async Task DrainAsync(IAsyncEnumerator<SseMessage> enumerator, Task<bool> pending)
    {
        try
        {
            await pending.WaitAsync(DrainTimeout);
            await enumerator.DisposeAsync();
        }
#pragma warning disable RCS1075
        catch (Exception)
        {
            // The producer was cancelled, failed or did not stop in time; nothing more can be delivered.
        }
#pragma warning restore RCS1075
    }

    private static Task<bool> MoveNextAsync(IAsyncEnumerator<SseMessage> enumerator) =>
        enumerator.MoveNextAsync().AsTask();

    private static async Task WriteAsync(HttpResponse response, byte[] payload, CancellationToken cancellationToken)
    {
        await response.Body.WriteAsync(payload, cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
