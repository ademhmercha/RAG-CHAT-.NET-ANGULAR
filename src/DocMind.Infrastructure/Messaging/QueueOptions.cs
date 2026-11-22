using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace DocMind.Infrastructure.Messaging;

public static class QueueProviders
{
    public const string Channels = "Channels";
    public const string Sqs = "Sqs";
}

public sealed class QueueOptions
{
    public const string SectionName = "Queue";

    [Required]
    public string Provider { get; init; } = QueueProviders.Channels;

    public SqsQueueOptions Sqs { get; init; } = new();
}

public sealed class SqsQueueOptions
{
    public const int MaxLongPollSeconds = 20;
    public const int MaxBatch = 10;

    public string QueueUrl { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string? ServiceUrl { get; init; }

    public int VisibilityTimeoutSeconds { get; init; } = 900;
}

public sealed class QueueOptionsValidator : IValidateOptions<QueueOptions>
{
    public ValidateOptionsResult Validate(string? name, QueueOptions options)
    {
        if (!string.Equals(options.Provider, QueueProviders.Sqs, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (!Uri.TryCreate(options.Sqs.QueueUrl, UriKind.Absolute, out _))
        {
            failures.Add("Queue:Sqs:QueueUrl must be an absolute URL when Queue:Provider is Sqs.");
        }

        if (string.IsNullOrWhiteSpace(options.Sqs.Region) && string.IsNullOrWhiteSpace(options.Sqs.ServiceUrl))
        {
            failures.Add("Queue:Sqs:Region (or Queue:Sqs:ServiceUrl) is required when Queue:Provider is Sqs.");
        }

        if (options.Sqs.VisibilityTimeoutSeconds is < 30 or > 43200)
        {
            failures.Add("Queue:Sqs:VisibilityTimeoutSeconds must be between 30 and 43200.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
