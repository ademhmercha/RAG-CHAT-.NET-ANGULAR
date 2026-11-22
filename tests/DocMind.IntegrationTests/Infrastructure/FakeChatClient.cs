using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace DocMind.IntegrationTests.Infrastructure;

public sealed record RecordedCall(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options);

public sealed class FakeChatClient : IChatClient
{
    private readonly Lock _gate = new();
    private readonly List<RecordedCall> _calls = [];

    public Func<IReadOnlyList<ChatMessage>, ChatOptions?, CancellationToken, IAsyncEnumerable<ChatResponseUpdate>>? Behavior { get; set; }

    public IReadOnlyList<RecordedCall> Calls
    {
        get
        {
            lock (_gate)
            {
                return [.. _calls];
            }
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _calls.Clear();
        }
    }

    public static ChatResponseUpdate Token(string text) => new(ChatRole.Assistant, text);

    public static ChatResponseUpdate Usage(long input, long output) => new()
    {
        Role = ChatRole.Assistant,
        Contents = [new UsageContent(new UsageDetails { InputTokenCount = input, OutputTokenCount = output })],
    };

    public static async IAsyncEnumerable<ChatResponseUpdate> Sequence(params ChatResponseUpdate[] updates)
    {
        foreach (var update in updates)
        {
            yield return update;
            await Task.Yield();
        }
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            updates.Add(update);
        }

        return updates.ToChatResponse();
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = messages.ToList();

        lock (_gate)
        {
            _calls.Add(new RecordedCall(snapshot, options));
        }

        return Behavior?.Invoke(snapshot, options, cancellationToken) ?? Echo(snapshot, cancellationToken);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> Echo(
        IReadOnlyList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var last = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;

        foreach (var word in $"Echo: {last}".Split(' '))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return Token(word + " ");
            await Task.Yield();
        }

        yield return Usage(12, 7);
    }
}
