using System.ComponentModel.DataAnnotations;

namespace DocMind.Application.Features.Documents;

public sealed class DocumentSettings
{
    public const string SectionName = "Documents";

    [Range(1024, 500L * 1024 * 1024)]
    public long MaxSizeBytes { get; init; } = 20L * 1024 * 1024;

    [Range(1, 100000)]
    public int MaxDocumentsPerUser { get; init; } = 500;

    [Range(1, 100)]
    public int MaxPageSize { get; init; } = 100;
}
