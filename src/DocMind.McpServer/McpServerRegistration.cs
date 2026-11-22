using DocMind.Application;
using DocMind.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace DocMind.McpServer;

public static class McpServerRegistration
{
    public static IMcpServerBuilder AddDocMindMcpServer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<McpUserOptions>()
            .Bind(configuration.GetSection(McpUserOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddApplication(configuration).AddInfrastructure(configuration);

        return services.AddMcpServer(options => options.ServerInfo = new() { Name = "docmind", Version = "1.0.0" })
            .WithToolsFromAssembly(typeof(DocumentMcpTools).Assembly);
    }
}
