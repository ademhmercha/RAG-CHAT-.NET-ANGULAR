using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocMind.IntegrationTests.Infrastructure;

public abstract class ChatApiTestBase(PostgresFixture postgres) : ApiTestBase(postgres)
{
    protected FakeChatClient Chat { get; } = new();

    protected FakeEmbeddingGenerator Embeddings { get; } = new();

    protected virtual bool UseFunctionInvocation => false;

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IChatClient>();

        if (UseFunctionInvocation)
        {
            services.AddSingleton<IChatClient>(sp => new ChatClientBuilder(Chat).UseFunctionInvocation(sp.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()).Build(sp));
        }
        else
        {
            services.AddSingleton<IChatClient>(Chat);
        }

        services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(Embeddings);
    }
}
