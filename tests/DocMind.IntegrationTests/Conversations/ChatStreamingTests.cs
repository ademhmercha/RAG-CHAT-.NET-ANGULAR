using System.Net;
using System.Runtime.CompilerServices;
using DocMind.Domain.Conversations;
using DocMind.IntegrationTests.Infrastructure;
using Microsoft.Extensions.AI;

namespace DocMind.IntegrationTests.Conversations;

[Collection(PostgresFixture.CollectionName)]
public sealed class ChatStreamingTests(PostgresFixture postgres) : ChatApiTestBase(postgres)
{
    protected override IReadOnlyDictionary<string, string?>? Settings => new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["Chat:HistoryMessages"] = "4",
        ["Sse:HeartbeatMilliseconds"] = "100",
    };

    [Fact]
    public async Task SendMessage_StreamsTokensThenDoneAndPersistsBothMessages()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session);

        using var response = await Client.SendChatAsync(session, id, "What is RAG?");
        var transcript = await SseReader.ReadAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/event-stream");
        response.Headers.CacheControl!.NoCache.Should().BeTrue();
        transcript.Events.Select(e => e.Name).Distinct(StringComparer.Ordinal).Should().Equal("token", "done");
        transcript.Events[^1].Name.Should().Be("done");
        transcript.Text.Should().Be("Echo: What is RAG? ");

        var done = transcript.Events[^1].Json;
        done.GetProperty("usage").GetProperty("inputTokens").GetInt32().Should().Be(12);
        done.GetProperty("usage").GetProperty("outputTokens").GetInt32().Should().Be(7);

        var messages = await LoadMessagesAsync(id);
        messages.Select(m => m.Role).Should().Equal(MessageRole.User, MessageRole.Assistant);
        messages[0].Content.Should().Be("What is RAG?");
        messages[1].Content.Should().Be(transcript.Text);
        messages[1].Id.Should().Be(done.GetProperty("messageId").GetGuid());
        messages[1].Usage.Should().Be(new TokenUsage(12, 7));
    }

    [Fact]
    public async Task SendMessage_NamesTheConversationAfterTheFirstQuestionOnly()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session);

        using var first = await Client.SendChatAsync(session, id, "Summarise the   onboarding\npolicy please");
        await SseReader.ReadAsync(first);
        using var second = await Client.SendChatAsync(session, id, "And the leave policy?");
        await SseReader.ReadAsync(second);

        var detail = await (await Client.GetAsync(session, $"/api/v1/conversations/{id}")).ReadJsonAsync();
        detail.GetProperty("title").GetString().Should().Be("Summarise the onboarding policy please");
    }

    [Fact]
    public async Task SendMessage_SendsTheSystemPromptAndRecentHistoryToTheModel()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session);

        foreach (var question in new[] { "one", "two", "three", "four" })
        {
            using var response = await Client.SendChatAsync(session, id, question);
            await SseReader.ReadAsync(response);
        }

        var lastCall = Chat.Calls[^1];
        lastCall.Messages[0].Role.Should().Be(ChatRole.System);
        lastCall.Messages.Skip(1).Select(m => m.Text).Should().Equal("two", "Echo: two ", "three", "Echo: three ", "four");
        lastCall.Messages[^1].Role.Should().Be(ChatRole.User);
        lastCall.Options!.Temperature.Should().BeApproximately(0.2f, 0.001f);
    }

    [Fact]
    public async Task SendMessage_EstimatesUsageWhenTheProviderReportsNone()
    {
        Chat.Behavior = (_, _, _) => FakeChatClient.Sequence(FakeChatClient.Token("a short answer"));
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session);

        using var response = await Client.SendChatAsync(session, id, "question");
        var transcript = await SseReader.ReadAsync(response);

        var usage = transcript.Named("done").Single().Json.GetProperty("usage");
        usage.GetProperty("inputTokens").GetInt32().Should().BeGreaterThan(0);
        usage.GetProperty("outputTokens").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task SendMessage_ForwardsToolActivityAsToolEvents()
    {
        Chat.Behavior = (_, _, _) => FakeChatClient.Sequence(
            new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "search_documents")]),
            new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("call-1", "found")]),
            new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-2", "summarize_document")]),
            new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("call-2", null) { Exception = new InvalidOperationException("boom") }]),
            FakeChatClient.Token("done"));
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session);

        using var response = await Client.SendChatAsync(session, id, "use your tools");
        var transcript = await SseReader.ReadAsync(response);

        transcript.Named("tool")
            .Select(e => $"{e.Json.GetProperty("name").GetString()}:{e.Json.GetProperty("status").GetString()}")
            .Should().Equal("search_documents:running", "search_documents:completed", "summarize_document:running", "summarize_document:failed");
    }

    [Fact]
    public async Task SendMessage_ReportsProviderFailuresAsAnErrorEventWithoutSavingAnAnswer()
    {
        Chat.Behavior = (_, _, _) => Throwing();
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session);

        using var response = await Client.SendChatAsync(session, id, "will fail");
        var transcript = await SseReader.ReadAsync(response);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var error = transcript.Events.Should().ContainSingle(e => e.Name == "error").Subject.Json;
        error.GetProperty("code").GetString().Should().Be("chat.provider_unavailable");
        error.GetProperty("status").GetInt32().Should().Be(500);
        error.GetProperty("traceId").GetString().Should().NotBeNullOrWhiteSpace();
        error.GetProperty("detail").GetString().Should().NotContain("provider exploded");
        transcript.Events.Should().NotContain(e => e.Name == "done");

        (await LoadMessagesAsync(id)).Select(m => m.Role).Should().Equal(MessageRole.User);
    }

    [Fact]
    public async Task SendMessage_ReportsAnEmptyAnswerAsAnError()
    {
        Chat.Behavior = (_, _, _) => FakeChatClient.Sequence(FakeChatClient.Usage(5, 0));
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session);

        using var response = await Client.SendChatAsync(session, id, "say nothing");
        var transcript = await SseReader.ReadAsync(response);

        transcript.Events.Should().ContainSingle(e => e.Name == "error")
            .Which.Json.GetProperty("code").GetString().Should().Be("chat.empty_response");
    }

    [Fact]
    public async Task SendMessage_SendsKeepAliveCommentsWhileTheModelIsSilent()
    {
        Chat.Behavior = (_, _, ct) => Slow(ct);
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session);

        using var response = await Client.SendChatAsync(session, id, "take your time");
        var transcript = await SseReader.ReadAsync(response);

        transcript.Comments.Should().Contain(": keep-alive");
        transcript.Text.Should().Be("late answer");
    }

    [Fact]
    public async Task SendMessage_KeepsThePartialAnswerWhenTheClientDisconnects()
    {
        Chat.Behavior = (_, _, ct) => PartialThenHang(ct);
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var response = await Client.SendChatAsync(session, id, "interrupt me", null, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
        var stream = await response.Content.ReadAsStreamAsync(cancellation.Token);
        var reader = new StreamReader(stream);
        string? line;
        do
        {
            line = await reader.ReadLineAsync(cancellation.Token);
        }
        while (line is not null && !line.StartsWith("data:", StringComparison.Ordinal));

        await cancellation.CancelAsync();
        reader.Dispose();
        response.Dispose();

        var saved = await WaitForAsync(async () => (await LoadMessagesAsync(id)).Count == 2);
        saved.Should().BeTrue("the partial answer should be stored once the client is gone");
        (await LoadMessagesAsync(id))[1].Content.Should().Be("partial ");
    }

    [Fact]
    public async Task SendMessage_RejectsInvalidRequests()
    {
        var session = await Client.RegisterAndLoginAsync();
        var id = await Client.CreateConversationAsync(session);

        var empty = await Client.SendChatAsync(session, id, "   ");
        var tooLong = await Client.SendChatAsync(session, id, new string('x', 8001));
        var missing = await Client.SendChatAsync(session, Guid.NewGuid(), "hello");
        var tooManyDocuments = await Client.SendChatAsync(session, id, "hello", Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()));

        empty.ShouldBeProblem(HttpStatusCode.BadRequest);
        tooLong.ShouldBeProblem(HttpStatusCode.BadRequest);
        missing.ShouldBeProblem(HttpStatusCode.NotFound);
        tooManyDocuments.ShouldBeProblem(HttpStatusCode.BadRequest);
        Chat.Calls.Should().BeEmpty();
    }

    private async Task<List<Message>> LoadMessagesAsync(Guid conversationId)
    {
        await using var db = TestDatabase.CreateContext(ConnectionString);
        return await db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => EF.Property<long>(m, "Sequence"))
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<bool> WaitForAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }

        return false;
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> Throwing()
    {
        await Task.Yield();
        throw new InvalidOperationException("provider exploded");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> Slow([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Delay(450, cancellationToken);
        yield return FakeChatClient.Token("late answer");
    }

    private static async IAsyncEnumerable<ChatResponseUpdate> PartialThenHang([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return FakeChatClient.Token("partial ");
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }
}
