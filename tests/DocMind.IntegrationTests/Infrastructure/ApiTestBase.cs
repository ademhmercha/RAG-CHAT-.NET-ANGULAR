using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace DocMind.IntegrationTests.Infrastructure;

public abstract class ApiTestBase(PostgresFixture postgres) : IAsyncLifetime
{
    protected PostgresFixture Postgres { get; } = postgres;

    protected FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero));

    protected DocMindApiFactory Factory { get; private set; } = null!;

    protected HttpClient Client { get; private set; } = null!;

    protected string ConnectionString { get; private set; } = null!;

    protected virtual IReadOnlyDictionary<string, string?>? Settings => null;

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    public virtual async ValueTask InitializeAsync()
    {
        ConnectionString = await Postgres.CreateDatabaseAsync();
        Factory = new DocMindApiFactory(
            ConnectionString,
            timeProvider: Time,
            settings: Settings,
            configureServices: ConfigureServices);
        Client = Factory.CreateApiClient();
    }

    public virtual async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await Factory.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
