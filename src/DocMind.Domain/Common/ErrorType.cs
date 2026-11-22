namespace DocMind.Domain.Common;

public enum ErrorType
{
    Failure = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    Unauthorized = 4,
    Forbidden = 5,
    PayloadTooLarge = 6,
    UnsupportedMediaType = 7,
}
