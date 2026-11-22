using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace DocMind.Api.Common;

internal sealed class RequestSizeGuardMiddleware(RequestDelegate next, IProblemDetailsService problems)
{
    public const string TooLargeCode = "request.too_large";

    public async Task InvokeAsync(HttpContext context)
    {
        var limit = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;

        if (limit is null || context.Request.ContentLength is not { } length || length <= limit)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;

        await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status413PayloadTooLarge,
                Title = "Payload Too Large",
                Detail = "The request body is too large.",
                Type = ProblemTypes.ForStatus(StatusCodes.Status413PayloadTooLarge),
                Extensions = { [ResultExtensions.ErrorCodeKey] = TooLargeCode },
            },
        });
    }
}
