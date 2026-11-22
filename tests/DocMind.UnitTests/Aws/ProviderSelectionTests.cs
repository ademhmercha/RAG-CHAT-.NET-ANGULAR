using System.Text;
using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using DocMind.Application;
using DocMind.Application.Abstractions;
using DocMind.Infrastructure;
using DocMind.Infrastructure.Aws;
using DocMind.Infrastructure.Messaging;
using DocMind.Infrastructure.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace DocMind.UnitTests.Aws;

public sealed class ProviderSelectionTests
{
    private static readonly Dictionary<string, string?> Baseline = new(StringComparer.Ordinal)
    {
        ["ConnectionStrings:docmind"] = "Host=localhost;Database=unused",
        ["Storage:Local:RootPath"] = Path.Combine(Path.GetTempPath(), "docmind-provider-tests"),
    };

    private static ServiceProvider Build(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string?>(Baseline, StringComparer.Ordinal);
        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<Microsoft.Extensions.Hosting.IHostEnvironment>());
        services.AddApplication(configuration).AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    private static readonly (string, string)[] AwsEverything =
    [
        ("Storage:Provider", "S3"),
        ("Storage:S3:BucketName", "docmind-documents"),
        ("Storage:S3:Region", "eu-west-1"),
        ("Queue:Provider", "Sqs"),
        ("Queue:Sqs:QueueUrl", "https://sqs.eu-west-1.amazonaws.com/123456789012/docmind-ingestion"),
        ("Queue:Sqs:Region", "eu-west-1"),
        ("Ai:Provider", "Bedrock"),
        ("Ai:Bedrock:Region", "eu-west-1"),
        ("Ai:Bedrock:ChatModelId", "eu.anthropic.claude-3-5-haiku-20241022-v1:0"),
        ("Embedding:Dimensions", "512"),
    ];

    [Fact]
    public void Defaults_UseLocalStorageChannelsAndOllama()
    {
        using var provider = Build();

        provider.GetRequiredService<IMessageQueue>().Should().BeOfType<ChannelMessageQueue>();
        provider.GetRequiredService<IChatClient>().GetService<ChatClientMetadata>()!.ProviderName.Should().Be("ollama");
        provider.GetRequiredService<IOptions<StorageOptions>>().Value.Provider.Should().Be("Local");
    }

    [Fact]
    public void AwsConfiguration_SelectsS3SqsAndBedrock()
    {
        using var provider = Build(AwsEverything);

        provider.GetRequiredService<IFileStorage>().Should().BeOfType<S3FileStorage>();
        provider.GetRequiredService<IMessageQueue>().Should().BeOfType<SqsMessageQueue>();

        var chat = provider.GetRequiredService<IChatClient>().GetService<ChatClientMetadata>()!;
        chat.ProviderName.Should().NotBe("ollama");
        chat.DefaultModelId.Should().Be("eu.anthropic.claude-3-5-haiku-20241022-v1:0");
    }

    [Fact]
    public void Providers_CanBeMixedIndependently()
    {
        using var provider = Build(("Queue:Provider", "Channels"), ("Storage:Provider", "S3"), ("Storage:S3:BucketName", "b"), ("Storage:S3:Region", "eu-west-1"));

        provider.GetRequiredService<IFileStorage>().Should().BeOfType<S3FileStorage>();
        provider.GetRequiredService<IMessageQueue>().Should().BeOfType<ChannelMessageQueue>();
    }

    [Theory]
    [InlineData("Storage:Provider", "Dropbox")]
    [InlineData("Queue:Provider", "Kafka")]
    [InlineData("Ai:Provider", "OpenAI")]
    public void UnknownProviders_FailFastWithAHelpfulMessage(string key, string value)
    {
        var act = () => Build((key, value));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{value}*Supported providers*");
    }

    [Fact]
    public void S3_RequiresABucketAndARegion()
    {
        using var provider = Build(("Storage:Provider", "S3"));

        var act = () => provider.GetRequiredService<IOptions<StorageOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().Contain(f => f.Contains("BucketName", StringComparison.Ordinal))
            .And.Contain(f => f.Contains("Region", StringComparison.Ordinal));
    }

    [Fact]
    public void Sqs_RequiresAQueueUrl()
    {
        using var provider = Build(("Queue:Provider", "Sqs"), ("Queue:Sqs:Region", "eu-west-1"));

        var act = () => provider.GetRequiredService<IOptions<QueueOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().ContainSingle(f => f.Contains("QueueUrl", StringComparison.Ordinal));
    }

    [Fact]
    public void Bedrock_RequiresAModelAndARegion()
    {
        using var provider = Build(("Ai:Provider", "Bedrock"));

        var act = () => provider.GetRequiredService<IOptions<DocMind.Infrastructure.Ai.AiOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures
            .Should().Contain(f => f.Contains("Region", StringComparison.Ordinal))
            .And.Contain(f => f.Contains("ChatModelId", StringComparison.Ordinal));
    }

    [Fact]
    public void Bedrock_RejectsEmbeddingDimensionsTitanDoesNotSupport()
    {
        using var provider = Build(
            ("Ai:Provider", "Bedrock"),
            ("Ai:Bedrock:Region", "eu-west-1"),
            ("Ai:Bedrock:ChatModelId", "model"),
            ("Embedding:Dimensions", "768"));

        var act = () => provider.GetRequiredService<IOptions<DocMind.Infrastructure.Ai.AiOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().ContainSingle(f => f.Contains("256, 512, 1024", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Bedrock_ChatGoesThroughTheConverseApiOfTheConfiguredModel()
    {
        var runtime = Substitute.For<IAmazonBedrockRuntime>();
        ConverseRequest? captured = null;
        runtime.ConverseAsync(Arg.Do<ConverseRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new ConverseResponse
            {
                Output = new ConverseOutput { Message = new Message { Role = ConversationRole.Assistant, Content = [new ContentBlock { Text = "Hello from Bedrock" }] } },
                StopReason = StopReason.End_turn,
                Usage = new TokenUsage { InputTokens = 11, OutputTokens = 4, TotalTokens = 15 },
            });
        using var provider = BuildWithRuntime(runtime, AwsEverything);

        var response = await provider.GetRequiredService<IChatClient>().GetResponseAsync("Hi", cancellationToken: TestContext.Current.CancellationToken);

        response.Text.Should().Be("Hello from Bedrock");
        response.Usage!.InputTokenCount.Should().Be(11);
        response.Usage.OutputTokenCount.Should().Be(4);
        captured!.ModelId.Should().Be("eu.anthropic.claude-3-5-haiku-20241022-v1:0");
    }

    [Fact]
    public async Task Bedrock_EmbeddingsAskTitanForTheConfiguredDimension()
    {
        var runtime = Substitute.For<IAmazonBedrockRuntime>();
        InvokeModelRequest? captured = null;
        string? requestBody = null;
        runtime.InvokeModelAsync(
                Arg.Do<InvokeModelRequest>(r =>
                {
                    captured = r;
                    requestBody = Encoding.UTF8.GetString(r.Body.ToArray());
                }),
                Arg.Any<CancellationToken>())
            .Returns(_ => new InvokeModelResponse
            {
                Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { embedding = new float[512], inputTextTokenCount = 3 }))),
                ContentType = "application/json",
            });
        using var provider = BuildWithRuntime(runtime, AwsEverything);

        var embeddings = await provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>()
            .GenerateAsync(["hello"], cancellationToken: TestContext.Current.CancellationToken);

        embeddings.Should().ContainSingle().Which.Vector.Length.Should().Be(512);
        captured!.ModelId.Should().Be("amazon.titan-embed-text-v2:0");
        JsonDocument.Parse(requestBody!).RootElement.GetProperty("dimensions").GetInt32().Should().Be(512);
    }

    private static ServiceProvider BuildWithRuntime(IAmazonBedrockRuntime runtime, params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, string?>(Baseline, StringComparer.Ordinal);
        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Substitute.For<Microsoft.Extensions.Hosting.IHostEnvironment>());
        services.AddApplication(configuration).AddInfrastructure(configuration);
        services.RemoveAll<IAmazonBedrockRuntime>();
        services.AddSingleton(runtime);

        return services.BuildServiceProvider();
    }
}
