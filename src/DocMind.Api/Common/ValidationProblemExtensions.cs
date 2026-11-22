using FluentValidation.Results;

namespace DocMind.Api.Common;

internal static class ValidationProblemExtensions
{
    public const string ValidationFailedCode = "validation.failed";

    public static IResult ToValidationProblem(this ValidationResult result)
    {
        var errors = result.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(e => e.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        return Results.ValidationProblem(
            errors,
            title: "One or more validation errors occurred.",
            type: ProblemTypes.ForStatus(StatusCodes.Status400BadRequest),
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { [ResultExtensions.ErrorCodeKey] = ValidationFailedCode });
    }

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) || char.IsLower(value[0]) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
