using DocMind.Domain.Conversations;

namespace DocMind.UnitTests.Domain;

public sealed class ConversationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_FallsBackToTheDefaultTitle(string? title)
    {
        var conversation = Conversation.Create(UserId, title, Now);

        conversation.Title.Should().Be(Conversation.DefaultTitle);
    }

    [Fact]
    public void Create_TrimsAndTruncatesTheTitle()
    {
        var conversation = Conversation.Create(UserId, $"  {new string('a', 500)}  ", Now);

        conversation.Title.Should().HaveLength(Conversation.TitleMaxLength);
    }

    [Fact]
    public void Create_RejectsAnEmptyUser()
    {
        var act = () => Conversation.Create(Guid.Empty, "title", Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rename_UpdatesTitleAndTimestamp()
    {
        var conversation = Conversation.Create(UserId, "old", Now);

        conversation.Rename("new title", Now.AddMinutes(2));

        conversation.Title.Should().Be("new title");
        conversation.UpdatedAt.Should().Be(Now.AddMinutes(2));
        conversation.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void AddMessage_AppendsTheMessageWithCitationsAndUsage()
    {
        var conversation = Conversation.Create(UserId, null, Now);
        var citation = new Citation(Guid.NewGuid(), "report.pdf", 4, "excerpt");

        var message = conversation.AddMessage(
            MessageRole.Assistant,
            "answer",
            [citation],
            new TokenUsage(10, 5),
            Now.AddSeconds(30));

        conversation.Messages.Should().ContainSingle().Which.Should().BeSameAs(message);
        message.ConversationId.Should().Be(conversation.Id);
        message.Role.Should().Be(MessageRole.Assistant);
        message.Citations.Should().ContainSingle().Which.Should().Be(citation);
        message.Usage!.TotalTokens.Should().Be(15);
        conversation.UpdatedAt.Should().Be(Now.AddSeconds(30));
    }

    [Fact]
    public void AddMessage_RejectsBlankContent()
    {
        var conversation = Conversation.Create(UserId, null, Now);

        var act = () => conversation.AddMessage(MessageRole.User, " ", null, null, Now);

        act.Should().Throw<ArgumentException>();
    }
}
