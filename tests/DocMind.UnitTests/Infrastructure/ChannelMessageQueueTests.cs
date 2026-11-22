using DocMind.Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DocMind.UnitTests.Infrastructure;

public sealed class ChannelMessageQueueTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly ChannelMessageQueue _queue;

    public ChannelMessageQueueTests()
    {
        _queue = new ChannelMessageQueue(_time, NullLogger<ChannelMessageQueue>.Instance);
    }

    public void Dispose() => _queue.Dispose();

    [Fact]
    public async Task Receive_ReturnsPublishedMessagesInOrderUpToTheLimit()
    {
        foreach (var body in new[] { "a", "b", "c" })
        {
            await _queue.PublishAsync(body, TestContext.Current.CancellationToken);
        }

        var first = await _queue.ReceiveAsync(2, TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        var second = await _queue.ReceiveAsync(2, TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

        first.Select(m => m.Body).Should().Equal("a", "b");
        second.Select(m => m.Body).Should().Equal("c");
        first.Should().OnlyContain(m => m.DeliveryCount == 1);
    }

    [Fact]
    public async Task Receive_ReturnsNothingWhenTheWaitElapses()
    {
        var messages = await _queue.ReceiveAsync(5, TimeSpan.FromMilliseconds(30), TestContext.Current.CancellationToken);

        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Receive_WakesUpWhenAMessageArrives()
    {
        var pending = _queue.ReceiveAsync(1, TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await _queue.PublishAsync("late", TestContext.Current.CancellationToken);

        (await pending).Should().ContainSingle().Which.Body.Should().Be("late");
    }

    [Fact]
    public async Task Receive_HonoursCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var pending = _queue.ReceiveAsync(1, TimeSpan.FromSeconds(30), cancellation.Token);

        await cancellation.CancelAsync();

        await pending.Invoking(p => p).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Abandon_RedeliversAfterTheDelayWithAnIncrementedCount()
    {
        await _queue.PublishAsync("retry me", TestContext.Current.CancellationToken);
        var message = (await _queue.ReceiveAsync(1, TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken)).Single();

        await _queue.AbandonAsync(message, TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        (await _queue.ReceiveAsync(1, TimeSpan.FromMilliseconds(30), TestContext.Current.CancellationToken)).Should().BeEmpty();

        _time.Advance(TimeSpan.FromSeconds(31));

        var redelivered = (await _queue.ReceiveAsync(1, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Single();
        redelivered.Id.Should().Be(message.Id);
        redelivered.DeliveryCount.Should().Be(2);
    }

    [Fact]
    public async Task Abandon_WithoutDelayRedeliversImmediately()
    {
        await _queue.PublishAsync("now", TestContext.Current.CancellationToken);
        var message = (await _queue.ReceiveAsync(1, TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken)).Single();

        await _queue.AbandonAsync(message, TimeSpan.Zero, TestContext.Current.CancellationToken);

        (await _queue.ReceiveAsync(1, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken)).Single().DeliveryCount.Should().Be(2);
    }
}
