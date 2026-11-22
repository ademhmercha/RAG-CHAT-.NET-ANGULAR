using Amazon.SQS;
using Amazon.SQS.Model;
using DocMind.Application.Abstractions;
using DocMind.Infrastructure.Aws;
using DocMind.Infrastructure.Messaging;
using Microsoft.Extensions.Options;

namespace DocMind.UnitTests.Aws;

public sealed class SqsMessageQueueTests
{
    private const string QueueUrl = "https://sqs.eu-west-1.amazonaws.com/123456789012/docmind-ingestion";

    private readonly IAmazonSQS _sqs = Substitute.For<IAmazonSQS>();

    private SqsMessageQueue CreateQueue(int visibilitySeconds = 600) => new(
        _sqs,
        Options.Create(new QueueOptions
        {
            Provider = QueueProviders.Sqs,
            Sqs = new SqsQueueOptions { QueueUrl = QueueUrl, Region = "eu-west-1", VisibilityTimeoutSeconds = visibilitySeconds },
        }));

    [Fact]
    public async Task Publish_SendsTheBodyToTheConfiguredQueue()
    {
        SendMessageRequest? captured = null;
        _sqs.SendMessageAsync(Arg.Do<SendMessageRequest>(r => captured = r), Arg.Any<CancellationToken>()).Returns(new SendMessageResponse());

        await CreateQueue().PublishAsync("{\"documentId\":1}", TestContext.Current.CancellationToken);

        captured!.QueueUrl.Should().Be(QueueUrl);
        captured.MessageBody.Should().Be("{\"documentId\":1}");
    }

    [Fact]
    public async Task Receive_LongPollsAndMapsMessagesWithTheirDeliveryCount()
    {
        ReceiveMessageRequest? captured = null;
        _sqs.ReceiveMessageAsync(Arg.Do<ReceiveMessageRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new ReceiveMessageResponse
            {
                Messages =
                [
                    new Message { MessageId = "m1", Body = "one", ReceiptHandle = "r1", Attributes = new Dictionary<string, string>(StringComparer.Ordinal) { ["ApproximateReceiveCount"] = "3" } },
                    new Message { MessageId = "m2", Body = "two", ReceiptHandle = "r2", Attributes = new Dictionary<string, string>(StringComparer.Ordinal) },
                ],
            });

        var messages = await CreateQueue(900).ReceiveAsync(4, TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        captured!.QueueUrl.Should().Be(QueueUrl);
        captured.MaxNumberOfMessages.Should().Be(4);
        captured.WaitTimeSeconds.Should().Be(5);
        captured.VisibilityTimeout.Should().Be(900);
        captured.MessageSystemAttributeNames.Should().Contain("ApproximateReceiveCount");
        messages.Should().Equal(new QueueMessage("m1", "one", 3, "r1"), new QueueMessage("m2", "two", 1, "r2"));
    }

    [Theory]
    [InlineData(50, 120, 10, 20)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(3, 0.2, 3, 1)]
    public async Task Receive_ClampsBatchSizeAndWaitTimeToSqsLimits(int requested, double waitSeconds, int expectedBatch, int expectedWait)
    {
        ReceiveMessageRequest? captured = null;
        _sqs.ReceiveMessageAsync(Arg.Do<ReceiveMessageRequest>(r => captured = r), Arg.Any<CancellationToken>()).Returns(new ReceiveMessageResponse());

        await CreateQueue().ReceiveAsync(requested, TimeSpan.FromSeconds(waitSeconds), TestContext.Current.CancellationToken);

        captured!.MaxNumberOfMessages.Should().Be(expectedBatch);
        captured.WaitTimeSeconds.Should().Be(expectedWait);
    }

    [Fact]
    public async Task Receive_ReturnsAnEmptyListWhenNothingArrives()
    {
        _sqs.ReceiveMessageAsync(Arg.Any<ReceiveMessageRequest>(), Arg.Any<CancellationToken>()).Returns(new ReceiveMessageResponse { Messages = null });

        (await CreateQueue().ReceiveAsync(1, TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task Complete_DeletesTheMessageByReceiptHandle()
    {
        DeleteMessageRequest? captured = null;
        _sqs.DeleteMessageAsync(Arg.Do<DeleteMessageRequest>(r => captured = r), Arg.Any<CancellationToken>()).Returns(new DeleteMessageResponse());

        await CreateQueue().CompleteAsync(new QueueMessage("m", "b", 1, "receipt-1"), TestContext.Current.CancellationToken);

        captured!.QueueUrl.Should().Be(QueueUrl);
        captured.ReceiptHandle.Should().Be("receipt-1");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2.2, 3)]
    [InlineData(900000, 43200)]
    public async Task Abandon_MakesTheMessageVisibleAgainAfterTheDelay(double delaySeconds, int expectedVisibility)
    {
        ChangeMessageVisibilityRequest? captured = null;
        _sqs.ChangeMessageVisibilityAsync(Arg.Do<ChangeMessageVisibilityRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new ChangeMessageVisibilityResponse());

        await CreateQueue().AbandonAsync(new QueueMessage("m", "b", 2, "receipt-2"), TimeSpan.FromSeconds(delaySeconds), TestContext.Current.CancellationToken);

        captured!.ReceiptHandle.Should().Be("receipt-2");
        captured.VisibilityTimeout.Should().Be(expectedVisibility);
    }
}
