using DocMind.Domain.Conversations;
using DocMind.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace DocMind.Application.Abstractions;

public interface IAppDbContext
{
    DatabaseFacade Database { get; }

    ChangeTracker ChangeTracker { get; }

    DbSet<Document> Documents { get; }

    DbSet<DocumentChunk> DocumentChunks { get; }

    DbSet<Conversation> Conversations { get; }

    DbSet<Message> Messages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
