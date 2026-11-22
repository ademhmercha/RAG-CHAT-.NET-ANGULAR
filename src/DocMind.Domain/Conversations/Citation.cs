namespace DocMind.Domain.Conversations;

public sealed record Citation(Guid DocumentId, string DocumentName, int Page, string Excerpt);
