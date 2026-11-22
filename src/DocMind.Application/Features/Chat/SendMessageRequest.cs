using FluentValidation;

namespace DocMind.Application.Features.Chat;

public sealed record SendMessageRequest(string Content, IReadOnlyList<Guid>? DocumentIds = null)
{
    public const int MaxContentLength = 8000;
    public const int MaxDocumentIds = 20;
}

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(SendMessageRequest.MaxContentLength);

        RuleFor(x => x.DocumentIds)
            .Must(ids => ids is null || ids.Count <= SendMessageRequest.MaxDocumentIds)
            .WithMessage($"At most {SendMessageRequest.MaxDocumentIds} documents can be selected.")
            .Must(ids => ids is null || ids.All(id => id != Guid.Empty))
            .WithMessage("Document identifiers must be valid.");
    }
}
