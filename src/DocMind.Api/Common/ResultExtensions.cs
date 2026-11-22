using DocMind.Domain.Common;
using Microsoft.AspNetCore.Http;

namespace DocMind.Api.Common;

internal static class ResultExtensions
{
    public const string ErrorCodeKey = "code";

    public static IResult ToProblem(this Error error)
    {
        var status = StatusFor(error.Type);

        return Results.Problem(
            statusCode: status,
            title: TitleFor(error.Type),
            detail: error.Description,
            type: ProblemTypes.ForStatus(status),
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { [ErrorCodeKey] = error.Code });
    }

    public static IResult ToProblem(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("A successful result cannot be converted to a problem.");
        }

        return result.Error.ToProblem();
    }

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error.ToProblem();

    public static int StatusFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.PayloadTooLarge => StatusCodes.Status413PayloadTooLarge,
        ErrorType.UnsupportedMediaType => StatusCodes.Status415UnsupportedMediaType,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static string TitleFor(ErrorType type) => type switch
    {
        ErrorType.Validation => "Bad Request",
        ErrorType.Unauthorized => "Unauthorized",
        ErrorType.Forbidden => "Forbidden",
        ErrorType.NotFound => "Not Found",
        ErrorType.Conflict => "Conflict",
        ErrorType.PayloadTooLarge => "Payload Too Large",
        ErrorType.UnsupportedMediaType => "Unsupported Media Type",
        _ => "Internal Server Error",
    };
}
