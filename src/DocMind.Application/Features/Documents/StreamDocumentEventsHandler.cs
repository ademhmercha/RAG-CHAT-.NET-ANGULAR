using DocMind.Application.Abstractions;

namespace DocMind.Application.Features.Documents;

public sealed class StreamDocumentEventsHandler(IDocumentEventSubscriber subscriber)
{
    public Task<IDocumentSubscription> HandleAsync(Guid userId, CancellationToken cancellationToken) =>
        subscriber.SubscribeAsync(userId, cancellationToken);
}
