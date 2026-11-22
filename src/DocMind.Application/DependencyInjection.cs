using DocMind.Application.Features.Chat;
using DocMind.Application.Features.Documents;
using DocMind.Application.Features.Ingestion;
using DocMind.Application.Features.Retrieval;
using DocMind.Application.Features.Tools;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocMind.Application;

public static class DependencyInjection
{
    private const string HandlerSuffix = "Handler";

    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        foreach (var handler in assembly.GetTypes().Where(IsHandler))
        {
            services.AddScoped(handler);
        }

        services.AddOptionsSection<ChatSettings>(configuration, ChatSettings.SectionName);
        services.AddOptionsSection<DocumentSettings>(configuration, DocumentSettings.SectionName);
        services.AddOptionsSection<SummarySettings>(configuration, SummarySettings.SectionName);
        services.AddOptionsSection<RagSettings>(configuration, RagSettings.SectionName);
        services.AddOptionsSection<IngestionSettings>(configuration, IngestionSettings.SectionName);
        services.AddSingleton<IValidateOptions<IngestionSettings>, IngestionSettingsValidator>();

        services.AddHybridCache();
        services.AddScoped<DocumentTools>();
        services.AddScoped<ChatToolFactory>();

        services.AddSingleton<IngestionQueueProcessor>();
        services.AddSingleton<DocumentRecoverySweeper>();

        return services;
    }

    private static void AddOptionsSection<TOptions>(this IServiceCollection services, IConfiguration configuration, string section)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
            .Bind(configuration.GetSection(section))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static bool IsHandler(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsPublic: true }
        && type.Name.EndsWith(HandlerSuffix, StringComparison.Ordinal);
}
