using DocMind.Application.Features.Chat;
using Microsoft.Extensions.AI;

namespace DocMind.UnitTests.Application;

public sealed class ChatStreamProcessorTests
{
    private static ChatResponseUpdate Text(string text) => new(ChatRole.Assistant, text);

    private static ChatResponseUpdate Usage(long? input, long? output) => new()
    {
        Contents = [new UsageContent(new UsageDetails { InputTokenCount = input, OutputTokenCount = output })],
    };

    [Fact]
    public void Process_AccumulatesTextAndEmitsTokenEvents()
    {
        var processor = new ChatStreamProcessor();

        var first = processor.Process(Text("Hello "));
        var second = processor.Process(Text("world"));

        first.Should().Equal(new TokenChatEvent("Hello "));
        second.Should().Equal(new TokenChatEvent("world"));
        processor.Text.Should().Be("Hello world");
        processor.HasText.Should().BeTrue();
    }

    [Fact]
    public void Process_IgnoresEmptyTextUpdates()
    {
        var processor = new ChatStreamProcessor();

        processor.Process(Text(string.Empty)).Should().BeEmpty();

        processor.HasText.Should().BeFalse();
    }

    [Fact]
    public void Process_SumsUsageAcrossToolRounds()
    {
        var processor = new ChatStreamProcessor();

        processor.Process(Usage(10, 4));
        processor.Process(Usage(30, null));
        processor.Process(Usage(null, 6));

        processor.Usage.Should().Be(new DocMind.Domain.Conversations.TokenUsage(40, 10));
    }

    [Fact]
    public void Usage_IsUnknownUntilTheProviderReportsIt()
    {
        new ChatStreamProcessor().Usage.Should().BeNull();
    }

    [Fact]
    public void Process_PairsToolResultsWithTheirCalls()
    {
        var processor = new ChatStreamProcessor();

        var started = processor.Process(new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("c1", "search_documents")]));
        var finished = processor.Process(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("c1", "ok")]));

        started.Should().Equal(new ToolChatEvent("search_documents", ToolStatus.Running));
        finished.Should().Equal(new ToolChatEvent("search_documents", ToolStatus.Completed));
    }

    [Fact]
    public void Process_ReportsFailedToolsAndUnknownCalls()
    {
        var processor = new ChatStreamProcessor();
        processor.Process(new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("c1", "summarize_document")]));

        var failed = processor.Process(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("c1", null) { Exception = new InvalidOperationException() }]));
        var orphan = processor.Process(new ChatResponseUpdate(ChatRole.Tool, [new FunctionResultContent("unknown", "x")]));

        failed.Should().Equal(new ToolChatEvent("summarize_document", ToolStatus.Failed));
        orphan.Should().Equal(new ToolChatEvent("tool", ToolStatus.Completed));
    }
}
