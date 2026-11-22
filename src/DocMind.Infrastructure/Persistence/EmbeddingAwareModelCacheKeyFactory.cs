using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DocMind.Infrastructure.Persistence;

internal sealed class EmbeddingAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        (context.GetType(), (context as AppDbContext)?.EmbeddingDimensions, designTime);
}
