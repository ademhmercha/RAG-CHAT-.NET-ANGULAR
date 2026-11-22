using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DocMind.IntegrationTests.Infrastructure;

public sealed class DocMindApiFactory(
    string connectionString,
    bool applyMigrations = true,
    bool documentationEnabled = true,
    TimeProvider? timeProvider = null,
    IReadOnlyDictionary<string, string?>? settings = null,
    Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    public const string SigningKey = "integration-tests-signing-key-0123456789-abcdef";

    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:docmind", connectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", applyMigrations.ToString());
        builder.UseSetting("Documentation:Enabled", documentationEnabled.ToString());
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("RateLimiting:Enabled", "false");

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>(StringComparer.Ordinal))
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, FaultInjectionStartupFilter>();

            if (timeProvider is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(timeProvider);
            }

            configureServices?.Invoke(services);
        });
    }
}
