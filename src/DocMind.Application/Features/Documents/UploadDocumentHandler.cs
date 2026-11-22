using DocMind.Application.Abstractions;
using DocMind.Application.Features.Ingestion;
using DocMind.Domain.Common;
using DocMind.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocMind.Application.Features.Documents;

public sealed record UploadDocumentCommand(string FileName, long Length, Stream Content);

public sealed class UploadDocumentHandler(
    IAppDbContext db,
    IFileStorage storage,
    IMessageQueue queue,
    IDocumentStatusNotifier notifier,
    TimeProvider time,
    IOptions<DocumentSettings> settings,
    ILogger<UploadDocumentHandler> logger)
{
    public async Task<Result<DocumentDto>> HandleAsync(Guid userId, UploadDocumentCommand command, CancellationToken cancellationToken)
    {
        var options = settings.Value;
        var contentType = DocumentFormats.ContentTypeForExtension(command.FileName);

        var rejection = await ValidateAsync(userId, command, contentType, options, cancellationToken);
        if (rejection is not null)
        {
            return rejection;
        }

        var now = time.GetUtcNow();
        var fileName = DocumentFormats.SanitizeFileName(command.FileName, Document.FileNameMaxLength);
        var documentId = Guid.CreateVersion7();
        var storageKey = $"documents/{userId:N}/{documentId:N}/original{Path.GetExtension(fileName).ToLowerInvariant()}";
        var document = Document.Create(userId, fileName, contentType!, command.Length, storageKey, now, documentId);

        await storage.SaveAsync(storageKey, command.Content, contentType!, cancellationToken);

        try
        {
            db.Documents.Add(document);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await TryDeleteFileAsync(storageKey);
            throw;
        }

        await PublishAsync(document, cancellationToken);
        await notifier.NotifyAsync(
            new DocumentStatusChanged(document.Id, userId, document.Status, null, document.UpdatedAt),
            cancellationToken);

        return document.ToDto();
    }

    private async Task<Error?> ValidateAsync(
        Guid userId,
        UploadDocumentCommand command,
        string? contentType,
        DocumentSettings options,
        CancellationToken cancellationToken)
    {
        if (contentType is null)
        {
            return DocumentUploadErrors.UnsupportedType;
        }

        if (command.Length <= 0)
        {
            return DocumentUploadErrors.EmptyFile;
        }

        if (command.Length > options.MaxSizeBytes)
        {
            return DocumentUploadErrors.TooLarge(options.MaxSizeBytes);
        }

        if (!await HasExpectedSignatureAsync(command.Content, contentType, cancellationToken))
        {
            return DocumentUploadErrors.ContentMismatch;
        }

        var owned = await db.Documents.CountAsync(d => d.UserId == userId, cancellationToken);
        return owned >= options.MaxDocumentsPerUser ? DocumentUploadErrors.QuotaExceeded(options.MaxDocumentsPerUser) : null;
    }

    private static async Task<bool> HasExpectedSignatureAsync(Stream content, string contentType, CancellationToken cancellationToken)
    {
        var header = new byte[DocumentFormats.SignatureLength];
        var read = await content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        content.Seek(0, SeekOrigin.Begin);

        return DocumentFormats.MatchesSignature(contentType, header.AsSpan(0, read));
    }

    private async Task PublishAsync(Document document, CancellationToken cancellationToken)
    {
        try
        {
            await queue.PublishAsync(new IngestionMessage(document.Id, document.UserId).Serialize(), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Publishing the ingestion message for document {DocumentId} failed; the recovery sweep will pick it up.", document.Id);
        }
    }

    private async Task TryDeleteFileAsync(string storageKey)
    {
        try
        {
            await storage.DeleteAsync(storageKey, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Removing the orphaned file {StorageKey} failed.", storageKey);
        }
    }
}
