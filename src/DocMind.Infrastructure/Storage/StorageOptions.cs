using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace DocMind.Infrastructure.Storage;

public static class StorageProviders
{
    public const string Local = "Local";
    public const string S3 = "S3";
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public string Provider { get; init; } = StorageProviders.Local;

    [ValidateObjectMembers]
    public LocalStorageOptions Local { get; init; } = new();

    public S3StorageOptions S3 { get; init; } = new();
}

public sealed class LocalStorageOptions
{
    [Required]
    public string RootPath { get; init; } = "data/storage";
}

public sealed class S3StorageOptions
{
    public string BucketName { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string? ServiceUrl { get; init; }

    public bool ForcePathStyle { get; init; }

    public string KeyPrefix { get; init; } = string.Empty;

    public int TimeoutSeconds { get; init; } = 120;
}

public sealed class StorageOptionsValidator : IValidateOptions<StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        if (!string.Equals(options.Provider, StorageProviders.S3, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        var s3 = options.S3;

        if (string.IsNullOrWhiteSpace(s3.BucketName))
        {
            failures.Add("Storage:S3:BucketName is required when Storage:Provider is S3.");
        }

        if (string.IsNullOrWhiteSpace(s3.Region) && string.IsNullOrWhiteSpace(s3.ServiceUrl))
        {
            failures.Add("Storage:S3:Region (or Storage:S3:ServiceUrl) is required when Storage:Provider is S3.");
        }

        if (s3.TimeoutSeconds is < 5 or > 900)
        {
            failures.Add("Storage:S3:TimeoutSeconds must be between 5 and 900.");
        }

        if (s3.KeyPrefix.StartsWith('/') || s3.KeyPrefix.Contains("..", StringComparison.Ordinal))
        {
            failures.Add("Storage:S3:KeyPrefix must be a relative prefix without '..'.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}
