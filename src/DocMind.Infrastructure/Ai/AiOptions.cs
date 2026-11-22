using System.ComponentModel.DataAnnotations;
using DocMind.Application.Ai;
using Microsoft.Extensions.Options;

namespace DocMind.Infrastructure.Ai;

public static class AiProviders
{
    public const string Ollama = "Ollama";
    public const string Bedrock = "Bedrock";
}

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    [Required]
    public string Provider { get; init; } = AiProviders.Ollama;

    [ValidateObjectMembers]
    public OllamaOptions Ollama { get; init; } = new();

    public BedrockOptions Bedrock { get; init; } = new();
}

public sealed class OllamaOptions
{
    [Required]
    [Url]
    public string Endpoint { get; init; } = "http://localhost:11434";

    [Required]
    public string ChatModel { get; init; } = "llama3.2";

    [Required]
    public string EmbeddingModel { get; init; } = "nomic-embed-text";

    [Range(5, 1800)]
    public int TimeoutSeconds { get; init; } = 300;
}

public sealed class BedrockOptions
{
    public string Region { get; init; } = string.Empty;

    public string? ServiceUrl { get; init; }

    public string ChatModelId { get; init; } = string.Empty;

    public string EmbeddingModelId { get; init; } = "amazon.titan-embed-text-v2:0";

    public int TimeoutSeconds { get; init; } = 300;
}

public sealed class AiOptionsValidator(IOptions<EmbeddingOptions> embedding) : IValidateOptions<AiOptions>
{
    private static readonly int[] TitanV2Dimensions = [256, 512, 1024];
    private static readonly string[] KnownProviders = [AiProviders.Ollama, AiProviders.Bedrock];

    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        var failures = new List<string>();

        if (!KnownProviders.Contains(options.Provider, StringComparer.Ordinal))
        {
            failures.Add($"Ai:Provider must be one of: {string.Join(", ", KnownProviders)}.");
        }

        if (string.Equals(options.Provider, AiProviders.Bedrock, StringComparison.Ordinal))
        {
            ValidateBedrock(options.Bedrock, embedding.Value.Dimensions, failures);
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }

    private static void ValidateBedrock(BedrockOptions bedrock, int dimensions, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(bedrock.Region) && string.IsNullOrWhiteSpace(bedrock.ServiceUrl))
        {
            failures.Add("Ai:Bedrock:Region is required when Ai:Provider is Bedrock.");
        }

        if (string.IsNullOrWhiteSpace(bedrock.ChatModelId))
        {
            failures.Add("Ai:Bedrock:ChatModelId is required when Ai:Provider is Bedrock.");
        }

        if (string.IsNullOrWhiteSpace(bedrock.EmbeddingModelId))
        {
            failures.Add("Ai:Bedrock:EmbeddingModelId is required when Ai:Provider is Bedrock.");
        }

        if (bedrock.TimeoutSeconds is < 5 or > 1800)
        {
            failures.Add("Ai:Bedrock:TimeoutSeconds must be between 5 and 1800.");
        }

        if (bedrock.EmbeddingModelId.Contains("titan-embed-text-v2", StringComparison.OrdinalIgnoreCase) && !TitanV2Dimensions.Contains(dimensions))
        {
            failures.Add($"Embedding:Dimensions must be one of {string.Join(", ", TitanV2Dimensions)} for Titan Text Embeddings V2.");
        }
    }
}
