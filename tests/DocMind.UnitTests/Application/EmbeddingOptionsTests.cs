using System.ComponentModel.DataAnnotations;
using DocMind.Application.Ai;

namespace DocMind.UnitTests.Application;

public sealed class EmbeddingOptionsTests
{
    private static bool IsValid(int dimensions)
    {
        var options = new EmbeddingOptions { Dimensions = dimensions };
        return Validator.TryValidateObject(options, new ValidationContext(options), [], validateAllProperties: true);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(768)]
    [InlineData(EmbeddingOptions.MaxIndexableDimensions)]
    public void ValidDimensions_AreAccepted(int dimensions) => IsValid(dimensions).Should().BeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(EmbeddingOptions.MaxIndexableDimensions + 1)]
    public void InvalidDimensions_AreRejected(int dimensions) => IsValid(dimensions).Should().BeFalse();

    [Fact]
    public void DefaultDimensions_MatchTheLocalEmbeddingModel() => new EmbeddingOptions().Dimensions.Should().Be(768);
}
