using DocMind.Application.Ai;
using DocMind.Domain.Documents;
using DocMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DocMind.UnitTests.Infrastructure;

public sealed class ModelConfigurationTests
{
    private static AppDbContext CreateContext(int dimensions)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseDocMindNpgsql("Host=localhost;Database=unused")
            .Options;

        return new AppDbContext(options, Options.Create(new EmbeddingOptions { Dimensions = dimensions }));
    }

    private static IModel DesignModel(AppDbContext context) => context.GetService<IDesignTimeModel>().Model;

    [Theory]
    [InlineData(768)]
    [InlineData(1024)]
    [InlineData(256)]
    public void EmbeddingColumn_FollowsTheConfiguredDimension(int dimensions)
    {
        using var context = CreateContext(dimensions);

        var property = DesignModel(context).FindEntityType(typeof(DocumentChunk))!.FindProperty(nameof(DocumentChunk.Embedding))!;

        property.GetColumnType().Should().Be($"vector({dimensions})");
    }

    [Fact]
    public void EmbeddingIndex_UsesHnswWithCosineOperators()
    {
        using var context = CreateContext(768);

        var index = DesignModel(context).FindEntityType(typeof(DocumentChunk))!
            .GetIndexes()
            .Single(i => i.Properties.Any(p => string.Equals(p.Name, nameof(DocumentChunk.Embedding), StringComparison.Ordinal)));

        index.GetMethod().Should().Be("hnsw");
        index.GetOperators().Should().ContainSingle().Which.Should().Be("vector_cosine_ops");
    }

    [Fact]
    public void Model_EnablesThePgvectorExtension()
    {
        using var context = CreateContext(768);

        DesignModel(context).GetPostgresExtensions().Should().Contain(e => e.Name == "vector");
    }

    [Fact]
    public void ChunkIndex_IsUniquePerDocument()
    {
        using var context = CreateContext(768);

        var unique = DesignModel(context).FindEntityType(typeof(DocumentChunk))!
            .GetIndexes()
            .Single(i => i.IsUnique);

        unique.Properties.Select(p => p.Name)
            .Should().Equal(nameof(DocumentChunk.DocumentId), nameof(DocumentChunk.ChunkIndex));
    }
}
