namespace DocMind.Application.Abstractions;

public sealed record RetrievedChunk(Guid DocumentId, string DocumentName, int Page, string Content, double Score);

public interface IChunkSearcher
{
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        Guid userId,
        ReadOnlyMemory<float> queryEmbedding,
        IReadOnlyCollection<Guid>? documentIds,
        int topK,
        double minScore,
        CancellationToken cancellationToken);
}
