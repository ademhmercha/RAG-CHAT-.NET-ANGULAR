using Asp.Versioning;
using DocMind.Api.Common;
using DocMind.Api.Features.Auth;
using DocMind.Api.Features.Conversations;
using DocMind.Api.Features.Documents;
using DocMind.Application.Features.Documents;
using DocMind.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

namespace DocMind.Api.Configuration;

internal static class WebApplicationExtensions
{
    public const string MigrateOnStartupKey = "Database:ApplyMigrationsOnStartup";
    public const string DocumentationEnabledKey = "Documentation:Enabled";

    public static async Task ApplyMigrationsIfConfiguredAsync(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>(MigrateOnStartupKey))
        {
            await DatabaseMigrator.ApplyMigrationsAsync(app.Services);
        }
    }

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>(ServiceCollectionExtensions.ForwardedHeadersEnabledKey))
        {
            app.UseForwardedHeaders();
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();

        if (app.Configuration.GetValue<bool>("Security:Hsts"))
        {
            app.UseHsts();
        }

        if (app.Configuration.GetValue<bool>("Security:RedirectToHttps"))
        {
            app.UseHttpsRedirection();
        }

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (app.Configuration.GetAllowedOrigins().Length > 0)
        {
            app.UseCors(ServiceCollectionExtensions.CorsPolicyName);
        }

        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.UseMiddleware<RequestSizeGuardMiddleware>();

        return app;
    }

    public static RouteGroupBuilder MapApiV1(this IEndpointRouteBuilder app)
    {
        var versionSet = app.NewApiVersionSet()
            .HasApiVersion(new ApiVersion(1, 0))
            .ReportApiVersions()
            .Build();

        var api = app.MapGroup("/api/v{version:apiVersion}").WithApiVersionSet(versionSet);

        api.MapAuthEndpoints();
        api.MapConversationsEndpoints();
        api.MapDocumentsEndpoints(app.ServiceProvider.GetRequiredService<IOptions<DocumentSettings>>().Value.MaxSizeBytes);

        return api;
    }

    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>(DocumentationEnabledKey))
        {
            return app;
        }

        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(options => options.WithTitle("DocMind API")).AllowAnonymous();

        return app;
    }
}
