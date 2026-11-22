using System.Runtime.CompilerServices;
using System.Text.Json;
using DocMind.Api.Common;
using DocMind.Api.Features.Conversations;
using DocMind.Application.Abstractions;

namespace DocMind.Api.Features.Documents;

internal static class DocumentSseMapper
{
    public const string StatusEvent = "status";

    public static async IAsyncEnumerable<SseMessage> MapAsync(
        IDocumentSubscription subscription,
        JsonSerializerOptions json,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using (subscription)
        {
            await foreach (var change in subscription.ReadAllAsync(cancellationToken))
            {
                yield return Map(change, json);
            }
        }
    }

    public static SseMessage Map(DocumentStatusChanged change, JsonSerializerOptions json) =>
        new(
            StatusEvent,
            JsonSerializer.Serialize(
                new
                {
                    documentId = change.DocumentId,
                    status = change.Status,
                    errorMessage = change.ErrorMessage,
                    updatedAt = change.UpdatedAt,
                },
                json));
}

internal static class ChatSseMapperFault
{
    public static SseMessage Create(JsonSerializerOptions json, string traceId) => ChatSseMapper.Fault(json, traceId);
}
