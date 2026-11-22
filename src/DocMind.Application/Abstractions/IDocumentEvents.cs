using DocMind.Domain.Documents;

namespace DocMind.Application.Abstractions;

public sealed record DocumentStatusChanged(
    Guid DocumentId,
    Guid UserId,
    DocumentStatus Status,
    string? ErrorMessage,
    DateTimeOffset UpdatedAt);

public interface IDocumentStatusNotifier
{
    Task NotifyAsync(DocumentStatusChanged change, CancellationToken cancellationToken);
}

public interface IDocumentSubscription : IAsyncDisposable
{
    IAsyncEnumerable<DocumentStatusChanged> ReadAllAsync(CancellationToken cancellationToken);
}

public interface IDocumentEventSubscriber
{
    Task<IDocumentSubscription> SubscribeAsync(Guid userId, CancellationToken cancellationToken);
}
