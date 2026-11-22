using DocMind.Domain.Common;
using DocMind.Domain.Conversations;

namespace DocMind.Application.Features.Chat;

public abstract record ChatEvent;

public sealed record TokenChatEvent(string Text) : ChatEvent;

public sealed record ToolChatEvent(string Name, string Status) : ChatEvent;

public sealed record CitationChatEvent(Citation Citation) : ChatEvent;

public sealed record DoneChatEvent(Guid MessageId, TokenUsage Usage) : ChatEvent;

public sealed record ErrorChatEvent(Error Error) : ChatEvent;

public static class ToolStatus
{
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
}
