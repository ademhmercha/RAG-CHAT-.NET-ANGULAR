using System.Text.Json;
using DocMind.IntegrationTests.Infrastructure;
using Microsoft.Extensions.AI;

namespace DocMind.IntegrationTests.Tools;

[Collection(PostgresFixture.CollectionName)]
public sealed class ToolCallingTests(PostgresFixture postgres) : DocumentApiTestBase(postgres)
{
    private const string VacationText = "Employees receive twenty five vacation days per year and may carry over five days.";
    private const string SummaryText = "A short summary of the policy.";

    protected override bool UseFunctionInvocation => true;

    protected override IReadOnlyDictionary<string, string?>? Settings
    {
        get
        {
            var settings = new Dictionary<string, string?>(base.Settings!, StringComparer.Ordinal)
            {
                ["Summary:MaxCharacters"] = "1000",
                ["Summary:MaxSegments"] = "3",
            };
            return settings;
        }
    }

    private static bool IsSummarization(IReadOnlyList<ChatMessage> messages) =>
        messages[0].Text.StartsWith("You summarize documents", StringComparison.Ordinal);

    private static Func<IReadOnlyList<ChatMessage>, ChatOptions?, CancellationToken, IAsyncEnumerable<ChatResponseUpdate>> CallOnce(
        string toolName,
        Dictionary<string, object?> arguments,
        string finalAnswer = "Final answer.") =>
        (messages, _, _) =>
        {
            if (IsSummarization(messages))
            {
                return FakeChatClient.Sequence(FakeChatClient.Token(SummaryText));
            }

            var hasResult = messages.Any(m => m.Contents.OfType<FunctionResultContent>().Any());
            return hasResult
                ? FakeChatClient.Sequence(FakeChatClient.Token(finalAnswer))
                : FakeChatClient.Sequence(new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", toolName, arguments)]));
        };

    private async Task<Guid> ReadyDocumentAsync(TestSession session, string fileName, string text)
    {
        var id = await UploadPdfAsync(session, fileName, text);
        await DrainQueueAsync();
        return id;
    }

    private static string ToolResultOf(RecordedCall call) =>
        call.Messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().Single().Result switch
        {
            JsonElement element => element.GetRawText(),
            var other => other?.ToString() ?? string.Empty,
        };

    [Fact]
    public async Task Chat_ExposesTheTwoToolsWithoutAnyUserIdentifierParameter()
    {
        var session = await Client.RegisterAndLoginAsync();
        var conversation = await Client.CreateConversationAsync(session);

        using var response = await Client.SendChatAsync(session, conversation, "hello");
        await SseReader.ReadAsync(response);

        var tools = Chat.Calls[0].Options!.Tools!.OfType<AIFunction>().ToList();
        tools.Select(t => t.Name).Should().BeEquivalentTo("search_documents", "summarize_document");
        tools.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Description));
        tools.Single(t => t.Name == "search_documents").JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo("query", "maxResults");
        tools.Single(t => t.Name == "summarize_document").JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo("documentId");
    }

    [Fact]
    public async Task SearchTool_ReturnsPassagesOfTheCallersDocumentsAndReportsToolEvents()
    {
        var session = await Client.RegisterAndLoginAsync();
        var document = await ReadyDocumentAsync(session, "vacation.pdf", VacationText);
        Chat.Behavior = CallOnce("search_documents", new Dictionary<string, object?> { ["query"] = "vacation days" }, "Done [1].");
        var conversation = await Client.CreateConversationAsync(session);

        using var response = await Client.SendChatAsync(session, conversation, "Use the tool please");
        var transcript = await SseReader.ReadAsync(response);

        transcript.Named("tool")
            .Select(e => $"{e.Json.GetProperty("name").GetString()}:{e.Json.GetProperty("status").GetString()}")
            .Should().Equal("search_documents:running", "search_documents:completed", "search_documents:running", "search_documents:completed");
        transcript.Text.Should().Be("Done [1].");

        var toolCall = Chat.Calls.Single(c => c.Messages.Any(m => m.Contents.OfType<FunctionResultContent>().Any()));
        var result = JsonDocument.Parse(ToolResultOf(toolCall)).RootElement;
        var hit = result.EnumerateArray().Should().ContainSingle().Subject;
        hit.GetProperty("documentId").GetGuid().Should().Be(document);
        hit.GetProperty("documentName").GetString().Should().Be("vacation.pdf");
        hit.GetProperty("page").GetInt32().Should().Be(1);
        hit.GetProperty("text").GetString().Should().Contain("vacation days");
    }

    [Fact]
    public async Task SearchTool_NeverReturnsAnotherUsersPassages()
    {
        var owner = await Client.RegisterAndLoginAsync();
        var caller = await Client.RegisterAndLoginAsync();
        await ReadyDocumentAsync(owner, "secret.pdf", VacationText);
        Chat.Behavior = CallOnce("search_documents", new Dictionary<string, object?> { ["query"] = "vacation days employees receive" });
        var conversation = await Client.CreateConversationAsync(caller);

        using var response = await Client.SendChatAsync(caller, conversation, "Look it up");
        await SseReader.ReadAsync(response);

        var toolCall = Chat.Calls.Single(c => c.Messages.Any(m => m.Contents.OfType<FunctionResultContent>().Any()));
        JsonDocument.Parse(ToolResultOf(toolCall)).RootElement.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task SearchTool_HonoursTheDocumentsSelectedForTheConversation()
    {
        var session = await Client.RegisterAndLoginAsync();
        await ReadyDocumentAsync(session, "vacation.pdf", VacationText);
        var other = await ReadyDocumentAsync(session, "other.pdf", "Quarterly parking permits are issued by facilities.");
        Chat.Behavior = CallOnce("search_documents", new Dictionary<string, object?> { ["query"] = "vacation days employees receive" });
        var conversation = await Client.CreateConversationAsync(session);

        using var response = await Client.SendChatAsync(session, conversation, "Look it up", [other]);
        await SseReader.ReadAsync(response);

        var toolCall = Chat.Calls.Single(c => c.Messages.Any(m => m.Contents.OfType<FunctionResultContent>().Any()));
        ToolResultOf(toolCall).Should().NotContain("vacation days");
    }

    [Fact]
    public async Task SummarizeTool_ReturnsASummaryOfTheCallersDocument()
    {
        var session = await Client.RegisterAndLoginAsync();
        var document = await ReadyDocumentAsync(session, "policy.pdf", VacationText);
        Chat.Behavior = CallOnce("summarize_document", new Dictionary<string, object?> { ["documentId"] = document.ToString() });
        var conversation = await Client.CreateConversationAsync(session);

        using var response = await Client.SendChatAsync(session, conversation, "Summarize my policy");
        var transcript = await SseReader.ReadAsync(response);

        transcript.Named("tool").Skip(2).Select(e => e.Json.GetProperty("status").GetString()).Should().Equal("running", "completed");
        var toolCall = Chat.Calls.Single(c => c.Messages.Any(m => m.Contents.OfType<FunctionResultContent>().Any()));
        var result = JsonDocument.Parse(ToolResultOf(toolCall)).RootElement;
        result.GetProperty("summary").GetString().Should().Be(SummaryText);
        result.TryGetProperty("error", out _).Should().BeFalse("null values are omitted from tool results");
        Chat.Calls.Count(c => IsSummarization(c.Messages)).Should().Be(1);
    }

    [Fact]
    public async Task SummarizeTool_CachesSummariesUntilTheDocumentChanges()
    {
        var session = await Client.RegisterAndLoginAsync();
        var document = await ReadyDocumentAsync(session, "policy.pdf", VacationText);
        Chat.Behavior = CallOnce("summarize_document", new Dictionary<string, object?> { ["documentId"] = document.ToString() });

        for (var i = 0; i < 2; i++)
        {
            var conversation = await Client.CreateConversationAsync(session);
            using var response = await Client.SendChatAsync(session, conversation, "Summarize my policy");
            await SseReader.ReadAsync(response);
        }

        Chat.Calls.Count(c => IsSummarization(c.Messages)).Should().Be(1);
    }

    [Fact]
    public async Task SummarizeTool_SummarizesLongDocumentsInPartsThenCombinesThem()
    {
        var session = await Client.RegisterAndLoginAsync();
        var pages = Enumerable.Range(0, 8).Select(i => string.Join(' ', Enumerable.Repeat($"chapter{i} content about policies and procedures", 25))).ToArray();
        var id = await UploadPdfAsync(session, "long.pdf", pages);
        await DrainQueueAsync();
        Chat.Behavior = CallOnce("summarize_document", new Dictionary<string, object?> { ["documentId"] = id.ToString() });
        var conversation = await Client.CreateConversationAsync(session);

        using var response = await Client.SendChatAsync(session, conversation, "Summarize the long document");
        await SseReader.ReadAsync(response);

        var summarizations = Chat.Calls.Count(c => IsSummarization(c.Messages));
        summarizations.Should().BeInRange(3, 4, "up to three sampled parts plus the final combination");
    }

    [Fact]
    public async Task SummarizeTool_DoesNotRevealOrSummarizeAnotherUsersDocument()
    {
        var owner = await Client.RegisterAndLoginAsync();
        var caller = await Client.RegisterAndLoginAsync();
        var foreign = await ReadyDocumentAsync(owner, "secret.pdf", VacationText);
        Chat.Behavior = CallOnce("summarize_document", new Dictionary<string, object?> { ["documentId"] = foreign.ToString() });
        var conversation = await Client.CreateConversationAsync(caller);

        using var response = await Client.SendChatAsync(caller, conversation, "Summarize that document");
        await SseReader.ReadAsync(response);

        var toolCall = Chat.Calls.Single(c => c.Messages.Any(m => m.Contents.OfType<FunctionResultContent>().Any()));
        var result = JsonDocument.Parse(ToolResultOf(toolCall)).RootElement;
        result.TryGetProperty("summary", out _).Should().BeFalse();
        result.GetProperty("error").GetString().Should().Contain("was not found");
        Chat.Calls.Count(c => IsSummarization(c.Messages)).Should().Be(0);
    }

    [Fact]
    public async Task SummarizeTool_RefusesDocumentsOutsideTheSelectedScope()
    {
        var session = await Client.RegisterAndLoginAsync();
        var selected = await ReadyDocumentAsync(session, "selected.pdf", VacationText);
        var outside = await ReadyDocumentAsync(session, "outside.pdf", "Parking permits are issued by facilities.");
        Chat.Behavior = CallOnce("summarize_document", new Dictionary<string, object?> { ["documentId"] = outside.ToString() });
        var conversation = await Client.CreateConversationAsync(session);

        using var response = await Client.SendChatAsync(session, conversation, "Summarize", [selected]);
        await SseReader.ReadAsync(response);

        var toolCall = Chat.Calls.Single(c => c.Messages.Any(m => m.Contents.OfType<FunctionResultContent>().Any()));
        JsonDocument.Parse(ToolResultOf(toolCall)).RootElement.GetProperty("error").GetString().Should().Be("Document not found.");
    }

    [Fact]
    public async Task SummarizeTool_ReportsAFriendlyErrorWhenTheModelFails()
    {
        var session = await Client.RegisterAndLoginAsync();
        var document = await ReadyDocumentAsync(session, "policy.pdf", VacationText);
        Chat.Behavior = (messages, _, _) =>
        {
            if (IsSummarization(messages))
            {
                throw new HttpRequestException("model crashed with internal detail");
            }

            var hasResult = messages.Any(m => m.Contents.OfType<FunctionResultContent>().Any());
            return hasResult
                ? FakeChatClient.Sequence(FakeChatClient.Token("Sorry."))
                : FakeChatClient.Sequence(new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("c", "summarize_document", new Dictionary<string, object?> { ["documentId"] = document.ToString() })]));
        };
        var conversation = await Client.CreateConversationAsync(session);

        using var response = await Client.SendChatAsync(session, conversation, "Summarize");
        await SseReader.ReadAsync(response);

        var toolCall = Chat.Calls.Single(c => c.Messages.Any(m => m.Contents.OfType<FunctionResultContent>().Any()));
        ToolResultOf(toolCall).Should().Contain("could not be generated").And.NotContain("internal detail");
    }
}
