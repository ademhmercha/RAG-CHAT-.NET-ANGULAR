using DocMind.Application.Abstractions;
using DocMind.Domain.Common;
using DocMind.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace DocMind.Application.Features.Documents;

public sealed class DeleteDocumentHandler(IAppDbContext db, IFileStorage storage, HybridCache cache, ILogger<DeleteDocumentHandler> logger)
{
    public async Task<Result> HandleAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var storageKey = await db.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId && d.UserId == userId)
            .Select(d => d.StorageKey)
            .SingleOrDefaultAsync(cancellationToken);

        if (storageKey is null)
        {
            return Result.Failure(DocumentErrors.NotFound(documentId));
        }

        var deleted = await db.Documents
            .Where(d => d.Id == documentId && d.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            return Result.Failure(DocumentErrors.NotFound(documentId));
        }

        await cache.RemoveByTagAsync(SummarizeDocumentHandler.TagFor(documentId), cancellationToken);

        try
        {
            await storage.DeleteAsync(storageKey, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "The file {StorageKey} of deleted document {DocumentId} could not be removed.", storageKey, documentId);
        }

        return Result.Success();
    }
}
