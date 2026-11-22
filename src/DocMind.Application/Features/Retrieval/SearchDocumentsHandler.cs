using DocMind.Application.Abstractions;
using DocMind.Application.Ai;
using DocMind.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace DocMind.Application.Features.Retrieval;

public sealed record SearchDocumentsQuery(string Query, IReadOnlyCollection<Guid>? DocumentIds = null, int? TopK = null);

public sealed class SearchDocumentsHandler(
    IAppDbContext db,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    IChunkSearcher searcher,
    IOptions<RagSettings> rag,
    IOptions<EmbeddingOptions> embeddingOptions)
{
    public async Task<bool> HasSearchableDocumentsAsync(Guid userId, IReadOnlyCollection<Guid>? documentIds, CancellationToken cancellationToken)
    {
        var query = db.Documents.AsNoTracking().Where(d => d.UserId == userId && d.Status == DocumentStatus.Ready);

        if (documentIds is { Count: > 0 })
        {
            query = query.Where(d => documentIds.Contains(d.Id));
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RetrievedChunk>> HandleAsync(Guid userId, SearchDocumentsQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Query) || !await HasSearchableDocumentsAsync(userId, query.DocumentIds, cancellationToken))
        {
            return [];
        }

        var settings = rag.Value;
        var topK = Math.Clamp(query.TopK ?? settings.TopK, 1, 20);

        var generated = await embeddings.GenerateAsync([query.Query], cancellationToken: cancellationToken);
        var vector = generated.Single().Vector;

        if (vector.Length != embeddingOptions.Value.Dimensions)
        {
            throw new InvalidOperationException(
                $"The embedding model returned {vector.Length} dimensions but {embeddingOptions.Value.Dimensions} are configured (Embedding:Dimensions).");
        }

        return await searcher.SearchAsync(userId, vector, query.DocumentIds, topK, settings.MinScore, cancellationToken);
    }
}
