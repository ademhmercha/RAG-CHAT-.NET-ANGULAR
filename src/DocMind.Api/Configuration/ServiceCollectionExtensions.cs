using System.Text.Json.Serialization;
using Asp.Versioning;
using DocMind.Api.Common;
using DocMind.Application.Abstractions;
using DocMind.Api.Features.Documents;
using DocMind.Application.Features.Documents;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace DocMind.Api.Configuration;

internal static class ServiceCollectionExtensions
{
    public const string ApiDocumentName = "v1";
    public const string CorsPolicyName = "frontend";
    public const string CorsOriginsKey = "Cors:AllowedOrigins";
    public const string ForwardedHeadersEnabledKey = "ForwardedHeaders:Enabled";

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JsonOptions>(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context => ProblemDetailsCustomizer.Apply(context));

        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = false;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });

        services.AddOpenApi(ApiDocumentName, options => options.AddDocumentTransformer<ApiInfoTransformer>());

        services.AddOptions<SseOptions>().Bind(configuration.GetSection(SseOptions.SectionName));

        services.AddOptions<FormOptions>().Configure<IOptions<DocumentSettings>>((form, documents) =>
            form.MultipartBodyLengthLimit = documents.Value.MaxSizeBytes + DocumentsEndpoints.MultipartOverheadBytes);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        AddCors(services, configuration);
        services.AddApiRateLimiting(configuration);
        AddForwardedHeaders(services, configuration);

        return services;
    }

    public static string[] GetAllowedOrigins(this IConfiguration configuration) =>
        configuration.GetSection(CorsOriginsKey).Get<string[]>() ?? [];

    private static void AddForwardedHeaders(IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>(ForwardedHeadersEnabledKey))
        {
            return;
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = configuration.GetValue("ForwardedHeaders:ForwardLimit", 1);
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
    }

    private static void AddCors(IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetAllowedOrigins();
        if (origins.Length == 0)
        {
            return;
        }

        services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Retry-After")));
    }
}
