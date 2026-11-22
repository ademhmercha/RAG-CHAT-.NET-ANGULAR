using Amazon;
using Amazon.BedrockRuntime;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using DocMind.Infrastructure.Ai;
using DocMind.Infrastructure.Messaging;
using DocMind.Infrastructure.Storage;

namespace DocMind.Infrastructure.Aws;

internal static class AwsClients
{
    private const int MaxRetries = 3;

    public static IAmazonS3 CreateS3(S3StorageOptions options)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle,
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
            MaxErrorRetry = MaxRetries,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        Apply(config, options.Region, options.ServiceUrl);
        return new AmazonS3Client(config);
    }

    public static IAmazonSQS CreateSqs(SqsQueueOptions options)
    {
        var config = new AmazonSQSConfig { MaxErrorRetry = MaxRetries };

        Apply(config, options.Region, options.ServiceUrl);
        return new AmazonSQSClient(config);
    }

    public static IAmazonBedrockRuntime CreateBedrock(BedrockOptions options)
    {
        var config = new AmazonBedrockRuntimeConfig
        {
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
            MaxErrorRetry = MaxRetries,
        };

        Apply(config, options.Region, options.ServiceUrl);
        return new AmazonBedrockRuntimeClient(config);
    }

    private static void Apply(ClientConfig config, string region, string? serviceUrl)
    {
        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            config.ServiceURL = serviceUrl;
            if (!string.IsNullOrWhiteSpace(region))
            {
                config.AuthenticationRegion = region;
            }

            return;
        }

        config.RegionEndpoint = RegionEndpoint.GetBySystemName(region);
    }
}
