using DocMind.Application.Ai;
using DocMind.Infrastructure.Identity;
using DocMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;

namespace DocMind.IntegrationTests.Infrastructure;

internal static class TestDatabase
{
    public const int Dimensions = 768;

    public static AppDbContext CreateContext(string connectionString, int dimensions = Dimensions)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseDocMindNpgsql(connectionString)
            .Options;

        return new AppDbContext(options, Options.Create(new EmbeddingOptions { Dimensions = dimensions }));
    }

    public static async Task<AppDbContext> CreateMigratedContextAsync(string connectionString)
    {
        var context = CreateContext(connectionString);
        await context.Database.MigrateAsync();
        return context;
    }

    public static ApplicationUser NewUser(string name = "user") => new()
    {
        Id = Guid.CreateVersion7(),
        UserName = $"{name}-{Guid.NewGuid():N}@docmind.test",
        NormalizedUserName = $"{name}-{Guid.NewGuid():N}@DOCMIND.TEST".ToUpperInvariant(),
        Email = $"{name}@docmind.test",
        DisplayName = name,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    public static Vector OneHot(int position, int dimensions = Dimensions)
    {
        var values = new float[dimensions];
        values[position] = 1f;
        return new Vector(values);
    }
}
