using System.Globalization;
using Amazon.SQS;
using Amazon.SQS.Model;
using DocMind.Application.Abstractions;
using DocMind.Infrastructure.Messaging;
using Microsoft.Extensions.Options;

namespace DocMind.Infrastructure.Aws;

internal sealed class SqsMessageQueue(IAmazonSQS sqs, IOptions<QueueOptions> options) : IMessageQueue
{
    private const string ReceiveCountAttribute = "ApproximateReceiveCount";
    private const int MaxVisibilitySeconds = 43200;

    private readonly SqsQueueOptions _options = options.Value.Sqs;

    public async Task PublishAsync(string body, CancellationToken cancellationToken) =>
        await sqs.SendMessageAsync(new SendMessageRequest { QueueUrl = _options.QueueUrl, MessageBody = body }, cancellationToken);

    public async Task<IReadOnlyList<QueueMessage>> ReceiveAsync(int maxMessages, TimeSpan waitTime, CancellationToken cancellationToken)
    {
        var response = await sqs.ReceiveMessageAsync(
            new ReceiveMessageRequest
            {
                QueueUrl = _options.QueueUrl,
                MaxNumberOfMessages = Math.Clamp(maxMessages, 1, SqsQueueOptions.MaxBatch),
                WaitTimeSeconds = Math.Clamp((int)Math.Ceiling(waitTime.TotalSeconds), 0, SqsQueueOptions.MaxLongPollSeconds),
                VisibilityTimeout = _options.VisibilityTimeoutSeconds,
                MessageSystemAttributeNames = [ReceiveCountAttribute],
            },
            cancellationToken);

        return (response.Messages ?? [])
            .Select(message => new QueueMessage(message.MessageId, message.Body, DeliveryCount(message), message.ReceiptHandle))
            .ToList();
    }

    public async Task CompleteAsync(QueueMessage message, CancellationToken cancellationToken) =>
        await sqs.DeleteMessageAsync(new DeleteMessageRequest { QueueUrl = _options.QueueUrl, ReceiptHandle = message.Receipt }, cancellationToken);

    public async Task AbandonAsync(QueueMessage message, TimeSpan delay, CancellationToken cancellationToken) =>
        await sqs.ChangeMessageVisibilityAsync(
            new ChangeMessageVisibilityRequest
            {
                QueueUrl = _options.QueueUrl,
                ReceiptHandle = message.Receipt,
                VisibilityTimeout = Math.Clamp((int)Math.Ceiling(delay.TotalSeconds), 0, MaxVisibilitySeconds),
            },
            cancellationToken);

    private static int DeliveryCount(Message message) =>
        message.Attributes is not null
        && message.Attributes.TryGetValue(ReceiveCountAttribute, out var value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? Math.Max(count, 1)
            : 1;
}
