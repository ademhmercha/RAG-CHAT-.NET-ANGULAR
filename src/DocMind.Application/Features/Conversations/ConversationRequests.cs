using DocMind.Domain.Conversations;
using FluentValidation;

namespace DocMind.Application.Features.Conversations;

public sealed record CreateConversationRequest(string? Title);

public sealed record RenameConversationRequest(string Title);

public sealed class CreateConversationRequestValidator : AbstractValidator<CreateConversationRequest>
{
    public CreateConversationRequestValidator()
    {
        RuleFor(x => x.Title).MaximumLength(Conversation.TitleMaxLength);
    }
}

public sealed class RenameConversationRequestValidator : AbstractValidator<RenameConversationRequest>
{
    public RenameConversationRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Conversation.TitleMaxLength);
    }
}
