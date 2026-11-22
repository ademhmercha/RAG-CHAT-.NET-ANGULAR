using DocMind.Domain.Common;

namespace DocMind.Domain.Documents;

public sealed class Document : Entity
{
    public const int FileNameMaxLength = 260;
    public const int ErrorMessageMaxLength = 2000;

    private Document()
    {
        FileName = string.Empty;
        ContentType = string.Empty;
        StorageKey = string.Empty;
    }

    private Document(
        Guid id,
        Guid userId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        DateTimeOffset now)
        : base(id, now)
    {
        UserId = userId;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        Status = DocumentStatus.Pending;
        UpdatedAt = now;
    }

    public Guid UserId { get; private set; }

    public string FileName { get; private set; }

    public string ContentType { get; private set; }

    public long SizeBytes { get; private set; }

    public string StorageKey { get; private set; }

    public DocumentStatus Status { get; private set; }

    public string? ErrorMessage { get; private set; }

    public int? PageCount { get; private set; }

    public int ChunkCount { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Document Create(
        Guid userId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageKey,
        DateTimeOffset now,
        Guid? id = null)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeBytes);

        return new Document(id ?? Guid.CreateVersion7(), userId, fileName.Trim(), contentType, sizeBytes, storageKey, now);
    }

    public Result MarkProcessing(DateTimeOffset now)
    {
        if (Status == DocumentStatus.Ready)
        {
            return Result.Failure(DocumentErrors.InvalidTransition(Status, DocumentStatus.Processing));
        }

        Status = DocumentStatus.Processing;
        ErrorMessage = null;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result MarkReady(int pageCount, int chunkCount, DateTimeOffset now)
    {
        if (Status != DocumentStatus.Processing)
        {
            return Result.Failure(DocumentErrors.InvalidTransition(Status, DocumentStatus.Ready));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(pageCount);
        ArgumentOutOfRangeException.ThrowIfNegative(chunkCount);

        Status = DocumentStatus.Ready;
        PageCount = pageCount;
        ChunkCount = chunkCount;
        ErrorMessage = null;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result MarkFailed(string errorMessage, DateTimeOffset now)
    {
        if (Status == DocumentStatus.Ready)
        {
            return Result.Failure(DocumentErrors.InvalidTransition(Status, DocumentStatus.Failed));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        Status = DocumentStatus.Failed;
        ErrorMessage = errorMessage.Length > ErrorMessageMaxLength
            ? errorMessage[..ErrorMessageMaxLength]
            : errorMessage;
        UpdatedAt = now;
        return Result.Success();
    }
}
