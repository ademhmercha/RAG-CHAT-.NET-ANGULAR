using DocMind.Domain.Common;

namespace DocMind.Domain.Conversations;

public static class ConversationErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("conversations.not_found", $"Conversation '{id}' was not found.");
}
