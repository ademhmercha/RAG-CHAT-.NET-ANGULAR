using Amazon.BedrockRuntime;
using AWS.Bedrock.MEAI;
using DocMind.Application.Ai;
using DocMind.Application.Features.Chat;
using DocMind.Infrastructure.Aws;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace DocMind.Infrastructure.Ai;

public static class AiServiceCollectionExtensions
{
    public const string TelemetrySourceName = "DocMind";

    private const string OllamaHttpClientKey = "ollama";
    private static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(5);

    public static IServiceCollection AddAiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AiOptions>()
            .Bind(configuration.GetSection(AiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AiOptions>, AiOptionsValidator>();

        var provider = configuration[$"{AiOptions.SectionName}:Provider"] ?? AiProviders.Ollama;

        switch (provider)
        {
            case AiProviders.Ollama:
                services.AddOllama();
                break;
            case AiProviders.Bedrock:
                services.AddBedrock();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown AI provider '{provider}'. Supported providers: {AiProviders.Ollama}, {AiProviders.Bedrock}.");
        }

        return services;
    }

    private static IChatClient Decorate(IChatClient inner, IServiceProvider services)
    {
        var chat = services.GetRequiredService<IOptions<ChatSettings>>().Value;

        return new ChatClientBuilder(inner)
            .UseFunctionInvocation(services.GetService<ILoggerFactory>(), client =>
            {
                client.MaximumIterationsPerRequest = chat.MaxToolIterations;
                client.AllowConcurrentInvocation = false;
            })
            .UseOpenTelemetry(services.GetService<ILoggerFactory>(), TelemetrySourceName)
            .Build(services);
    }

    private static void AddOllama(this IServiceCollection services)
    {
        services.AddKeyedSingleton<HttpClient>(OllamaHttpClientKey, (sp, _) =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value.Ollama;

            return new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = ConnectionLifetime })
            {
                BaseAddress = new Uri(options.Endpoint),
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds),
            };
        });

        services.AddSingleton<IChatClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value.Ollama;
            var http = sp.GetRequiredKeyedService<HttpClient>(OllamaHttpClientKey);

            return Decorate(new OllamaApiClient(http, options.ChatModel), sp);
        });

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value.Ollama;
            var http = sp.GetRequiredKeyedService<HttpClient>(OllamaHttpClientKey);

            return new OllamaApiClient(http, options.EmbeddingModel);
        });
    }

    private static void AddBedrock(this IServiceCollection services)
    {
        services.AddSingleton(sp => AwsClients.CreateBedrock(sp.GetRequiredService<IOptions<AiOptions>>().Value.Bedrock));

        services.AddSingleton<IChatClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value.Bedrock;
            var runtime = sp.GetRequiredService<IAmazonBedrockRuntime>();

            return Decorate(runtime.AsIChatClient(options.ChatModelId), sp);
        });

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AiOptions>>().Value.Bedrock;
            var dimensions = sp.GetRequiredService<IOptions<EmbeddingOptions>>().Value.Dimensions;

            return sp.GetRequiredService<IAmazonBedrockRuntime>().AsIEmbeddingGenerator(options.EmbeddingModelId, dimensions);
        });
    }
}
