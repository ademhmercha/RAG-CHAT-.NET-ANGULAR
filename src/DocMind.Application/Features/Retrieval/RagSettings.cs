using System.ComponentModel.DataAnnotations;

namespace DocMind.Application.Features.Retrieval;

public sealed class RagSettings
{
    public const string SectionName = "Rag";

    [Range(1, 20)]
    public int TopK { get; init; } = 6;

    [Range(0.0, 1.0)]
    public double MinScore { get; init; } = 0.30;

    [Range(0.0, 1.0)]
    public double FallbackCitationScore { get; init; } = 0.55;

    [Range(1, 5)]
    public int MaxFallbackCitations { get; init; } = 3;

    [Range(50, 2000)]
    public int ExcerptCharacters { get; init; } = 300;

    [Range(1000, 100000)]
    public int MaxContextCharacters { get; init; } = 12000;

    [Range(10, 500)]
    public int ShortQueryCharacters { get; init; } = 40;
}
