namespace DocMind.Application.Abstractions;

public sealed record QueueMessage(string Id, string Body, int DeliveryCount, string Receipt);

public interface IMessageQueue
{
    Task PublishAsync(string body, CancellationToken cancellationToken);

    Task<IReadOnlyList<QueueMessage>> ReceiveAsync(int maxMessages, TimeSpan waitTime, CancellationToken cancellationToken);

    Task CompleteAsync(QueueMessage message, CancellationToken cancellationToken);

    Task AbandonAsync(QueueMessage message, TimeSpan delay, CancellationToken cancellationToken);
}
