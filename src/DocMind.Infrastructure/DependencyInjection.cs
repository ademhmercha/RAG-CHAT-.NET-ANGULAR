using DocMind.Application.Abstractions;
using DocMind.Application.Ai;
using DocMind.Infrastructure.Ai;
using DocMind.Infrastructure.Aws;
using DocMind.Infrastructure.Ingestion;
using DocMind.Infrastructure.Maintenance;
using DocMind.Infrastructure.Messaging;
using DocMind.Infrastructure.Persistence;
using DocMind.Infrastructure.Retrieval;
using DocMind.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocMind.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "docmind";
    public const string ReadinessTag = "ready";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);

        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<EmbeddingOptions>()
            .Bind(configuration.GetSection(EmbeddingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>(options => options.UseDocMindNpgsql(connectionString));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IDocumentStatusNotifier, PostgresDocumentStatusNotifier>();
        services.AddScoped<IChunkSearcher, PgVectorChunkSearcher>();
        services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();

        services.AddAiInfrastructure(configuration);
        services.AddStorage(configuration);
        services.AddMessaging(configuration);

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("postgres", tags: [ReadinessTag]);

        return services;
    }

    public static IServiceCollection AddDocumentEventStreaming(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);

        services.AddSingleton(sp => new DocumentEventHub(connectionString, sp.GetRequiredService<ILogger<DocumentEventHub>>()));
        services.AddSingleton<IDocumentEventSubscriber>(sp => sp.GetRequiredService<DocumentEventHub>());
        services.AddHostedService(sp => sp.GetRequiredService<DocumentEventHub>());

        return services;
    }

    public static IServiceCollection AddMaintenanceJobs(this IServiceCollection services) =>
        services.AddHostedService<ExpiredTokenPurgeService>();

    private static string GetConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");

    private static void AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>();

        var provider = configuration[$"{StorageOptions.SectionName}:Provider"] ?? StorageProviders.Local;

        switch (provider)
        {
            case StorageProviders.Local:
                services.AddSingleton<IFileStorage, LocalFileStorage>();
                break;
            case StorageProviders.S3:
                services.AddSingleton(sp => AwsClients.CreateS3(sp.GetRequiredService<IOptions<StorageOptions>>().Value.S3));
                services.AddSingleton<IFileStorage, S3FileStorage>();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown storage provider '{provider}'. Supported providers: {StorageProviders.Local}, {StorageProviders.S3}.");
        }
    }

    private static void AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<QueueOptions>()
            .Bind(configuration.GetSection(QueueOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<QueueOptions>, QueueOptionsValidator>();

        var provider = configuration[$"{QueueOptions.SectionName}:Provider"] ?? QueueProviders.Channels;

        switch (provider)
        {
            case QueueProviders.Channels:
                services.AddSingleton<IMessageQueue, ChannelMessageQueue>();
                break;
            case QueueProviders.Sqs:
                services.AddSingleton(sp => AwsClients.CreateSqs(sp.GetRequiredService<IOptions<QueueOptions>>().Value.Sqs));
                services.AddSingleton<IMessageQueue, SqsMessageQueue>();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown queue provider '{provider}'. Supported providers: {QueueProviders.Channels}, {QueueProviders.Sqs}.");
        }
    }
}
