using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace DocMind.Api.Common;

internal static class ProblemDetailsCustomizer
{
    public const string TraceIdKey = "traceId";

    public static void Apply(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var request = context.HttpContext.Request;

        problem.Instance ??= $"{request.Method} {request.Path}";
        problem.Extensions[TraceIdKey] = TraceIdFor(context.HttpContext);
    }

    public static string TraceIdFor(HttpContext httpContext) =>
        Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
}
