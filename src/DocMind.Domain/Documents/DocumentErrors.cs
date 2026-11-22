using DocMind.Domain.Common;

namespace DocMind.Domain.Documents;

public static class DocumentErrors
{
    public static Error NotFound(Guid id) =>
        Error.NotFound("documents.not_found", $"Document '{id}' was not found.");

    public static Error NotReady(Guid id) =>
        Error.Conflict("documents.not_ready", $"Document '{id}' has not finished processing yet.");

    public static Error InvalidTransition(DocumentStatus from, DocumentStatus to) =>
        Error.Conflict("documents.invalid_transition", $"A document cannot move from {from} to {to}.");
}
