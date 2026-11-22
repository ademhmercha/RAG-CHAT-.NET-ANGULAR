using DocMind.Domain.Common;

namespace DocMind.Domain.Conversations;

public sealed class Message : Entity
{
    private readonly List<Citation> _citations = [];

    private Message()
    {
        Content = string.Empty;
    }

    internal Message(
        Guid conversationId,
        MessageRole role,
        string content,
        IEnumerable<Citation>? citations,
        TokenUsage? usage,
        DateTimeOffset now)
        : base(Guid.CreateVersion7(), now)
    {
        ConversationId = conversationId;
        Role = role;
        Content = content;
        Usage = usage;

        if (citations is not null)
        {
            _citations.AddRange(citations);
        }
    }

    public Guid ConversationId { get; private set; }

    public MessageRole Role { get; private set; }

    public string Content { get; private set; }

    public IReadOnlyList<Citation> Citations => _citations;

    public TokenUsage? Usage { get; private set; }
}
