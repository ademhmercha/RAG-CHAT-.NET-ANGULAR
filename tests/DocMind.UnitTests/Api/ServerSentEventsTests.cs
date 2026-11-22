using System.Text;
using System.Text.Json;
using DocMind.Api.Common;
using DocMind.Api.Features.Conversations;
using DocMind.Application.Features.Chat;
using DocMind.Domain.Common;
using DocMind.Domain.Conversations;

namespace DocMind.UnitTests.Api;

public sealed class ServerSentEventsTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Format_WritesTheEventNameAndSingleLineData()
    {
        var bytes = ServerSentEventsResult.Format(new SseMessage("token", "{\"text\":\"hi\"}"));

        Encoding.UTF8.GetString(bytes).Should().Be("event: token\ndata: {\"text\":\"hi\"}\n\n");
    }

    [Fact]
    public void Format_SplitsMultiLineDataSoTheStreamCannotBeInjectedInto()
    {
        var bytes = ServerSentEventsResult.Format(new SseMessage("token", "line one\r\nevent: forged\nline three"));

        Encoding.UTF8.GetString(bytes).Should().Be("event: token\ndata: line one\ndata: event: forged\ndata: line three\n\n");
    }

    [Fact]
    public void Map_SerializesTokenAndToolEvents()
    {
        ChatSseMapper.Map(new TokenChatEvent("a \"quoted\" word"), Json, "t").Should()
            .Be(new SseMessage("token", "{\"text\":\"a \\u0022quoted\\u0022 word\"}"));
        ChatSseMapper.Map(new ToolChatEvent("search_documents", "running"), Json, "t").Should()
            .Be(new SseMessage("tool", "{\"name\":\"search_documents\",\"status\":\"running\"}"));
    }

    [Fact]
    public void Map_SerializesCitationsWithTheContractFields()
    {
        var id = Guid.NewGuid();

        var message = ChatSseMapper.Map(new CitationChatEvent(new Citation(id, "report.pdf", 3, "excerpt")), Json, "t");

        message.Event.Should().Be("citation");
        var data = JsonDocument.Parse(message.Data).RootElement;
        data.GetProperty("documentId").GetGuid().Should().Be(id);
        data.GetProperty("documentName").GetString().Should().Be("report.pdf");
        data.GetProperty("page").GetInt32().Should().Be(3);
        data.GetProperty("excerpt").GetString().Should().Be("excerpt");
    }

    [Fact]
    public void Map_SerializesTheDoneEventWithUsage()
    {
        var id = Guid.NewGuid();

        var message = ChatSseMapper.Map(new DoneChatEvent(id, new TokenUsage(10, 5)), Json, "t");

        var data = JsonDocument.Parse(message.Data).RootElement;
        message.Event.Should().Be("done");
        data.GetProperty("messageId").GetGuid().Should().Be(id);
        data.GetProperty("usage").GetProperty("totalTokens").GetInt32().Should().Be(15);
    }

    [Fact]
    public void Map_SerializesErrorsAsProblemDetails()
    {
        var message = ChatSseMapper.Map(new ErrorChatEvent(ChatErrors.ProviderUnavailable), Json, "trace-123");

        var data = JsonDocument.Parse(message.Data).RootElement;
        message.Event.Should().Be("error");
        data.GetProperty("status").GetInt32().Should().Be(500);
        data.GetProperty("code").GetString().Should().Be("chat.provider_unavailable");
        data.GetProperty("traceId").GetString().Should().Be("trace-123");
        data.GetProperty("type").GetString().Should().StartWith("https://www.rfc-editor.org/rfc/");
    }

    [Fact]
    public void Map_MapsDomainErrorTypesToProblemStatuses()
    {
        var message = ChatSseMapper.Map(new ErrorChatEvent(Error.NotFound("x.missing", "Gone.")), Json, "t");

        JsonDocument.Parse(message.Data).RootElement.GetProperty("status").GetInt32().Should().Be(404);
    }
}
