using System.ComponentModel.DataAnnotations;

namespace DocMind.Application.Ai;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";
    public const int MaxIndexableDimensions = 2000;

    [Range(1, MaxIndexableDimensions)]
    public int Dimensions { get; init; } = 768;
}
