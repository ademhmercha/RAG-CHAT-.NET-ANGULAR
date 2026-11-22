using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace DocMind.Application.Features.Ingestion;

public sealed class IngestionSettings
{
    public const string SectionName = "Ingestion";

    [Range(200, 8000)]
    public int ChunkSize { get; init; } = 1000;

    [Range(0, 2000)]
    public int ChunkOverlap { get; init; } = 150;

    [Range(1, 128)]
    public int EmbeddingBatchSize { get; init; } = 16;

    [Range(1, 10)]
    public int MaxAttempts { get; init; } = 3;

    [Range(1, 60000)]
    public int RetryBaseDelayMilliseconds { get; init; } = 1000;

    [Range(1, 120)]
    public int LeaseMinutes { get; init; } = 10;

    [Range(1, 3600)]
    public int SweepIntervalSeconds { get; init; } = 30;

    [Range(0, 600)]
    public int PendingGraceSeconds { get; init; } = 15;

    [Range(1, 16)]
    public int MaxConcurrency { get; init; } = 2;

    [Range(1, 20)]
    public int MaxDeliveries { get; init; } = 5;

    [Range(1, 20000)]
    public int MaxPages { get; init; } = 2000;

    [Range(1000, 100_000_000)]
    public int MaxCharacters { get; init; } = 10_000_000;

    [Range(1, 60)]
    public int PollWaitSeconds { get; init; } = 5;
}

public sealed class IngestionSettingsValidator : IValidateOptions<IngestionSettings>
{
    public ValidateOptionsResult Validate(string? name, IngestionSettings options) =>
        options.ChunkOverlap >= options.ChunkSize
            ? ValidateOptionsResult.Fail("Ingestion:ChunkOverlap must be smaller than Ingestion:ChunkSize.")
            : ValidateOptionsResult.Success;
}
