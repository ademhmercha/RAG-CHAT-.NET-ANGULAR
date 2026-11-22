namespace DocMind.Api.Common;

internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private static readonly string[] DocumentationPrefixes = ["/scalar", "/openapi"];

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var http = (HttpContext)state;
            var headers = http.Response.Headers;

            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cross-Origin-Resource-Policy"] = "same-site";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            if (!DocumentationPrefixes.Any(prefix => http.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            }

            return Task.CompletedTask;
        }, context);

        return next(context);
    }
}
