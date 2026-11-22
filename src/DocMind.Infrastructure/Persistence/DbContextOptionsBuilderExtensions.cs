using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace DocMind.Infrastructure.Persistence;

public static class DbContextOptionsBuilderExtensions
{
    private const int MaxRetryCount = 3;

    public static string WithoutGssProbing(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!builder.ContainsKey("GSS Encryption Mode"))
        {
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        return builder.ConnectionString;
    }

    public static DbContextOptionsBuilder UseDocMindNpgsql(this DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(
                WithoutGssProbing(connectionString),
                npgsql =>
                {
                    npgsql.UseVector();
                    npgsql.EnableRetryOnFailure(MaxRetryCount);
                })
            .ReplaceService<IModelCacheKeyFactory, EmbeddingAwareModelCacheKeyFactory>();

    public static DbContextOptionsBuilder<TContext> UseDocMindNpgsql<TContext>(
        this DbContextOptionsBuilder<TContext> options,
        string connectionString)
        where TContext : DbContext
    {
        ((DbContextOptionsBuilder)options).UseDocMindNpgsql(connectionString);
        return options;
    }
}
