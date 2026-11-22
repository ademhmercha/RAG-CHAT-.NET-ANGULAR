using DocMind.Domain.Common;
using Pgvector;

namespace DocMind.Domain.Documents;

public sealed class DocumentChunk : Entity
{
    private DocumentChunk()
    {
        Content = string.Empty;
        Embedding = new Vector(Array.Empty<float>());
    }

    private DocumentChunk(
        Guid documentId,
        Guid userId,
        int chunkIndex,
        int pageNumber,
        string content,
        Vector embedding,
        DateTimeOffset now)
        : base(Guid.CreateVersion7(), now)
    {
        DocumentId = documentId;
        UserId = userId;
        ChunkIndex = chunkIndex;
        PageNumber = pageNumber;
        Content = content;
        Embedding = embedding;
    }

    public Guid DocumentId { get; private set; }

    public Guid UserId { get; private set; }

    public int ChunkIndex { get; private set; }

    public int PageNumber { get; private set; }

    public string Content { get; private set; }

    public Vector Embedding { get; private set; }

    public static DocumentChunk Create(
        Guid documentId,
        Guid userId,
        int chunkIndex,
        int pageNumber,
        string content,
        Vector embedding,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(documentId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(userId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegative(chunkIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentNullException.ThrowIfNull(embedding);

        return new DocumentChunk(documentId, userId, chunkIndex, pageNumber, content, embedding, now);
    }
}
