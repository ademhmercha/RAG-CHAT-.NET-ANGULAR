using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using DocMind.Api.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DocMind.Api.Configuration;

internal sealed class RateLimitRule
{
    public RateLimitRule()
    {
    }

    public RateLimitRule(int permitLimit, int windowSeconds)
    {
        PermitLimit = permitLimit;
        WindowSeconds = windowSeconds;
    }

    [Range(1, 100000)]
    public int PermitLimit { get; init; } = 60;

    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;
}

internal sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; init; } = true;

    [ValidateObjectMembers]
    public RateLimitRule Global { get; init; } = new(300, 60);

    [ValidateObjectMembers]
    public RateLimitRule Auth { get; init; } = new(20, 60);

    [ValidateObjectMembers]
    public RateLimitRule Chat { get; init; } = new(20, 60);

    [ValidateObjectMembers]
    public RateLimitRule Upload { get; init; } = new(20, 60);
}

internal static class RateLimiting
{
    public const string AuthPolicy = "auth";
    public const string ChatPolicy = "chat";
    public const string UploadPolicy = "upload";
    public const string ExceededCode = "rate_limit.exceeded";

    private const int SlidingSegments = 6;
    private const string SubjectClaim = "sub";
    private const string UnknownClient = "unknown";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitSettings>()
            .Bind(configuration.GetSection(RateLimitSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var settings = configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>() ?? new RateLimitSettings();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            if (!settings.Enabled)
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ => RateLimitPartition.GetNoLimiter("disabled"));
                options.AddPolicy(AuthPolicy, _ => RateLimitPartition.GetNoLimiter("disabled"));
                options.AddPolicy(ChatPolicy, _ => RateLimitPartition.GetNoLimiter("disabled"));
                options.AddPolicy(UploadPolicy, _ => RateLimitPartition.GetNoLimiter("disabled"));
                return;
            }

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                IsProbe(context)
                    ? RateLimitPartition.GetNoLimiter("probe")
                    : Fixed($"global:{UserOrAddress(context)}", settings.Global));

            options.AddPolicy(AuthPolicy, context => Fixed($"auth:{Address(context)}", settings.Auth));
            options.AddPolicy(ChatPolicy, context => Sliding($"chat:{UserOrAddress(context)}", settings.Chat));
            options.AddPolicy(UploadPolicy, context => Sliding($"upload:{UserOrAddress(context)}", settings.Upload));
        });

        return services;
    }

    private static bool IsProbe(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

    private static string Address(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? UnknownClient;

    private static string UserOrAddress(HttpContext context) =>
        context.User.FindFirst(SubjectClaim)?.Value is { Length: > 0 } subject ? $"user:{subject}" : $"ip:{Address(context)}";

    private static RateLimitPartition<string> Fixed(string key, RateLimitRule rule) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rule.PermitLimit,
            Window = TimeSpan.FromSeconds(rule.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true,
        });

    private static RateLimitPartition<string> Sliding(string key, RateLimitRule rule) =>
        RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = rule.PermitLimit,
            Window = TimeSpan.FromSeconds(rule.WindowSeconds),
            SegmentsPerWindow = SlidingSegments,
            QueueLimit = 0,
            AutoReplenishment = true,
        });

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            http.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        await http.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too Many Requests",
                Detail = "Rate limit exceeded. Please slow down and try again shortly.",
                Type = ProblemTypes.ForStatus(StatusCodes.Status429TooManyRequests),
                Extensions = { [ResultExtensions.ErrorCodeKey] = ExceededCode },
            },
        });
    }
}
