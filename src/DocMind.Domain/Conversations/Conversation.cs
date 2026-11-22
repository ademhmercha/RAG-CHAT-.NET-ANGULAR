using DocMind.Domain.Common;

namespace DocMind.Domain.Conversations;

public sealed class Conversation : Entity
{
    public const int TitleMaxLength = 200;
    public const string DefaultTitle = "New conversation";

    private readonly List<Message> _messages = [];

    private Conversation()
    {
        Title = DefaultTitle;
    }

    private Conversation(Guid userId, string title, DateTimeOffset now)
        : base(Guid.CreateVersion7(), now)
    {
        UserId = userId;
        Title = title;
        UpdatedAt = now;
    }

    public Guid UserId { get; private set; }

    public string Title { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<Message> Messages => _messages;

    public static Conversation Create(Guid userId, string? title, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        return new Conversation(userId, NormalizeTitle(title), now);
    }

    public void Rename(string title, DateTimeOffset now)
    {
        Title = NormalizeTitle(title);
        UpdatedAt = now;
    }

    public Message AddMessage(
        MessageRole role,
        string content,
        IEnumerable<Citation>? citations,
        TokenUsage? usage,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var message = new Message(Id, role, content, citations, usage, now);
        _messages.Add(message);
        UpdatedAt = now;
        return message;
    }

    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return DefaultTitle;
        }

        var trimmed = title.Trim();
        return trimmed.Length > TitleMaxLength ? trimmed[..TitleMaxLength] : trimmed;
    }
}
