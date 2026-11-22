using Microsoft.AspNetCore.Http;

namespace DocMind.Api.Common;

internal static class ProblemTypes
{
    public static string ForStatus(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
        StatusCodes.Status401Unauthorized => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.2",
        StatusCodes.Status403Forbidden => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.4",
        StatusCodes.Status404NotFound => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.10",
        StatusCodes.Status413PayloadTooLarge => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.14",
        StatusCodes.Status415UnsupportedMediaType => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.16",
        StatusCodes.Status429TooManyRequests => "https://www.rfc-editor.org/rfc/rfc6585#section-4",
        >= 500 => "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1",
        _ => "about:blank",
    };
}
