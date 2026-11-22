using DocMind.Domain.Conversations;

namespace DocMind.Application.Features.Conversations;

public sealed record ConversationSummaryDto(Guid Id, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record MessageDto(
    Guid Id,
    MessageRole Role,
    string Content,
    IReadOnlyList<Citation> Citations,
    TokenUsage? Usage,
    DateTimeOffset CreatedAt);

public sealed record ConversationDto(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<MessageDto> Messages);

internal static class ConversationMappings
{
    public static ConversationSummaryDto ToSummary(this Conversation conversation) =>
        new(conversation.Id, conversation.Title, conversation.CreatedAt, conversation.UpdatedAt);

    public static MessageDto ToDto(this Message message) =>
        new(message.Id, message.Role, message.Content, message.Citations.ToList(), message.Usage, message.CreatedAt);

    public static ConversationDto ToDto(this Conversation conversation) =>
        new(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            conversation.Messages.Select(ToDto).ToList());
}
