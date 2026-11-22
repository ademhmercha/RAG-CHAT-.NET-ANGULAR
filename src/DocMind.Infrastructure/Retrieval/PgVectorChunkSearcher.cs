using DocMind.Application.Abstractions;
using DocMind.Domain.Documents;
using DocMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace DocMind.Infrastructure.Retrieval;

internal sealed class PgVectorChunkSearcher(AppDbContext db) : IChunkSearcher
{
    private const int EfSearch = 100;

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        Guid userId,
        ReadOnlyMemory<float> queryEmbedding,
        IReadOnlyCollection<Guid>? documentIds,
        int topK,
        double minScore,
        CancellationToken cancellationToken)
    {
        var query = new Vector(queryEmbedding);
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            await db.Database.ExecuteSqlAsync(
                $"SELECT set_config('hnsw.iterative_scan', 'relaxed_order', true), set_config('hnsw.ef_search', {EfSearch.ToString(System.Globalization.CultureInfo.InvariantCulture)}, true)",
                cancellationToken);

            var chunks = db.DocumentChunks.AsNoTracking().Where(c => c.UserId == userId);

            if (documentIds is { Count: > 0 })
            {
                chunks = chunks.Where(c => documentIds.Contains(c.DocumentId));
            }

            var rows = await (
                from chunk in chunks
                join document in db.Documents.AsNoTracking() on chunk.DocumentId equals document.Id
                where document.UserId == userId && document.Status == DocumentStatus.Ready
                orderby chunk.Embedding.CosineDistance(query)
                select new
                {
                    chunk.DocumentId,
                    document.FileName,
                    chunk.PageNumber,
                    chunk.Content,
                    Distance = chunk.Embedding.CosineDistance(query),
                })
                .Take(topK)
                .ToListAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return (IReadOnlyList<RetrievedChunk>)rows
                .Select(row => new RetrievedChunk(row.DocumentId, row.FileName, row.PageNumber, row.Content, 1 - row.Distance))
                .Where(chunk => chunk.Score >= minScore)
                .ToList();
        });
    }
}
