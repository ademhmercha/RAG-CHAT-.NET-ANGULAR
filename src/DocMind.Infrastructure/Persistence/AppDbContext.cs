using DocMind.Application.Abstractions;
using DocMind.Application.Ai;
using DocMind.Domain.Conversations;
using DocMind.Domain.Documents;
using DocMind.Infrastructure.Identity;
using DocMind.Infrastructure.Persistence.Configurations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DocMind.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IOptions<EmbeddingOptions> embeddingOptions)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IAppDbContext
{
    public int EmbeddingDimensions => embeddingOptions.Value.Dimensions;

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("vector");

        builder.ApplyConfiguration(new ApplicationUserConfiguration());
        builder.ApplyConfiguration(new DocumentConfiguration());
        builder.ApplyConfiguration(new DocumentChunkConfiguration(EmbeddingDimensions));
        builder.ApplyConfiguration(new ConversationConfiguration());
        builder.ApplyConfiguration(new MessageConfiguration());
        builder.ApplyConfiguration(new RefreshTokenConfiguration());
    }
}
