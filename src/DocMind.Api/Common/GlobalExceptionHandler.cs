using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocMind.Api.Common;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    private const int ClientClosedRequest = 499;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var problem = exception is BadHttpRequestException badRequest
            ? ToProblem(badRequest)
            : InternalServerError();

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "Rejected request on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails ToProblem(BadHttpRequestException exception)
    {
        var status = IsLimitExceeded(exception) ? StatusCodes.Status413PayloadTooLarge : exception.StatusCode;

        return new ProblemDetails
        {
            Status = status,
            Title = "The request could not be processed.",
            Detail = status == StatusCodes.Status413PayloadTooLarge
                ? "The request body is too large."
                : "The request is malformed.",
            Type = ProblemTypes.ForStatus(status),
        };
    }

    private static bool IsLimitExceeded(BadHttpRequestException exception) =>
        exception.StatusCode == StatusCodes.Status413PayloadTooLarge
        || (exception.InnerException is InvalidDataException inner && inner.Message.Contains("limit", StringComparison.OrdinalIgnoreCase));

    private static ProblemDetails InternalServerError() => new()
    {
        Status = StatusCodes.Status500InternalServerError,
        Title = "An unexpected error occurred.",
        Detail = "The server failed to process the request. Quote the trace identifier when contacting support.",
        Type = ProblemTypes.ForStatus(StatusCodes.Status500InternalServerError),
    };
}
