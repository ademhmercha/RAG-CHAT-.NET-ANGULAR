using DocMind.Application.Features.Chat;
using DocMind.Application.Features.Conversations;

namespace DocMind.UnitTests.Application;

public sealed class ChatValidatorTests
{
    private readonly SendMessageRequestValidator _send = new();

    [Fact]
    public void SendMessage_AcceptsContentWithOrWithoutDocuments()
    {
        _send.Validate(new SendMessageRequest("hello")).IsValid.Should().BeTrue();
        _send.Validate(new SendMessageRequest("hello", [Guid.NewGuid()])).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SendMessage_RejectsBlankContent(string content)
    {
        _send.Validate(new SendMessageRequest(content)).Errors.Should().Contain(e => e.PropertyName == "Content");
    }

    [Fact]
    public void SendMessage_RejectsContentOverTheLimit()
    {
        var request = new SendMessageRequest(new string('a', SendMessageRequest.MaxContentLength + 1));

        _send.Validate(request).IsValid.Should().BeFalse();
        _send.Validate(request with { Content = new string('a', SendMessageRequest.MaxContentLength) }).IsValid.Should().BeTrue();
    }

    [Fact]
    public void SendMessage_RejectsTooManyOrEmptyDocumentIdentifiers()
    {
        var tooMany = Enumerable.Range(0, SendMessageRequest.MaxDocumentIds + 1).Select(_ => Guid.NewGuid()).ToList();

        _send.Validate(new SendMessageRequest("hi", tooMany)).IsValid.Should().BeFalse();
        _send.Validate(new SendMessageRequest("hi", [Guid.Empty])).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Conversation_TitlesAreBoundedAndRenameRequiresOne()
    {
        new CreateConversationRequestValidator().Validate(new CreateConversationRequest(null)).IsValid.Should().BeTrue();
        new CreateConversationRequestValidator().Validate(new CreateConversationRequest(new string('t', 201))).IsValid.Should().BeFalse();
        new RenameConversationRequestValidator().Validate(new RenameConversationRequest(" ")).IsValid.Should().BeFalse();
        new RenameConversationRequestValidator().Validate(new RenameConversationRequest("ok")).IsValid.Should().BeTrue();
    }
}
