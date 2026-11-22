using System.Runtime.CompilerServices;
using System.Text.Json;
using DocMind.Api.Common;
using DocMind.Application.Features.Chat;
using Microsoft.AspNetCore.Mvc;

namespace DocMind.Api.Features.Conversations;

internal static class ChatSseMapper
{
    public const string TokenEvent = "token";
    public const string ToolEvent = "tool";
    public const string CitationEvent = "citation";
    public const string DoneEvent = "done";
    public const string ErrorEvent = "error";

    public static async IAsyncEnumerable<SseMessage> MapAsync(
        IAsyncEnumerable<ChatEvent> events,
        JsonSerializerOptions json,
        string traceId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var chatEvent in events.WithCancellation(cancellationToken))
        {
            yield return Map(chatEvent, json, traceId);
        }
    }

    public static SseMessage Map(ChatEvent chatEvent, JsonSerializerOptions json, string traceId) => chatEvent switch
    {
        TokenChatEvent token => new SseMessage(TokenEvent, JsonSerializer.Serialize(new { text = token.Text }, json)),
        ToolChatEvent tool => new SseMessage(ToolEvent, JsonSerializer.Serialize(new { name = tool.Name, status = tool.Status }, json)),
        CitationChatEvent citation => new SseMessage(CitationEvent, JsonSerializer.Serialize(
            new
            {
                documentId = citation.Citation.DocumentId,
                documentName = citation.Citation.DocumentName,
                page = citation.Citation.Page,
                excerpt = citation.Citation.Excerpt,
            },
            json)),
        DoneChatEvent done => new SseMessage(DoneEvent, JsonSerializer.Serialize(
            new
            {
                messageId = done.MessageId,
                usage = new
                {
                    inputTokens = done.Usage.InputTokens,
                    outputTokens = done.Usage.OutputTokens,
                    totalTokens = done.Usage.TotalTokens,
                },
            },
            json)),
        ErrorChatEvent error => ErrorMessage(error.Error.Description, error.Error.Code, ResultExtensions.StatusFor(error.Error.Type), json, traceId),
        _ => throw new InvalidOperationException($"Unsupported chat event '{chatEvent.GetType().Name}'."),
    };

    public static SseMessage Fault(JsonSerializerOptions json, string traceId) =>
        ErrorMessage(
            "The server failed to process the request.",
            "server.unexpected_error",
            StatusCodes.Status500InternalServerError,
            json,
            traceId);

    private static SseMessage ErrorMessage(string detail, string code, int status, JsonSerializerOptions json, string traceId)
    {
        var problem = new ProblemDetails
        {
            Type = ProblemTypes.ForStatus(status),
            Title = status >= StatusCodes.Status500InternalServerError ? "Internal Server Error" : "Request failed",
            Status = status,
            Detail = detail,
            Extensions =
            {
                [ResultExtensions.ErrorCodeKey] = code,
                [ProblemDetailsCustomizer.TraceIdKey] = traceId,
            },
        };

        return new SseMessage(ErrorEvent, JsonSerializer.Serialize(problem, json));
    }
}
