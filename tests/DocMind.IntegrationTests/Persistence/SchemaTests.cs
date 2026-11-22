using DocMind.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DocMind.IntegrationTests.Persistence;

[Collection(PostgresFixture.CollectionName)]
public sealed class SchemaTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migrations_ApplyCleanlyAndMatchTheModel()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = await TestDatabase.CreateMigratedContextAsync(connectionString);

        (await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
        context.Database.HasPendingModelChanges().Should().BeFalse("every model change must ship with a migration");
    }

    [Fact]
    public async Task Migrations_AreIdempotent()
    {
        var connectionString = await NewMigratedDatabaseAsync();

        var act = async () =>
        {
            await using var second = await TestDatabase.CreateMigratedContextAsync(connectionString);
        };

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Schema_ContainsThePgvectorExtensionAndTheHnswIndex()
    {
        var connectionString = await NewMigratedDatabaseAsync();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var extension = await ScalarAsync(connection, "SELECT extname FROM pg_extension WHERE extname = 'vector'");
        var indexDefinition = await ScalarAsync(
            connection,
            "SELECT indexdef FROM pg_indexes WHERE tablename = 'DocumentChunks' AND indexdef ILIKE '%hnsw%'");
        var columnType = await ScalarAsync(
            connection,
            "SELECT format_type(atttypid, atttypmod) FROM pg_attribute WHERE attrelid = '\"DocumentChunks\"'::regclass AND attname = 'Embedding'");

        extension.Should().Be("vector");
        indexDefinition.Should().Contain("vector_cosine_ops");
        columnType.Should().Be($"vector({TestDatabase.Dimensions})");
    }

    private async Task<string> NewMigratedDatabaseAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var context = await TestDatabase.CreateMigratedContextAsync(connectionString);
        return connectionString;
    }

    private static async Task<string?> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))?.ToString();
    }
}
