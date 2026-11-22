using System.ComponentModel.DataAnnotations;

namespace DocMind.Application.Features.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinSigningKeyLength = 32;

    [Required]
    public string Issuer { get; init; } = "docmind";

    [Required]
    public string Audience { get; init; } = "docmind-web";

    [Required]
    [MinLength(MinSigningKeyLength)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 60)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 90)]
    public int RefreshTokenDays { get; init; } = 7;

    [Range(1, 365)]
    public int RefreshTokenAbsoluteDays { get; init; } = 30;

    [Range(0, 120)]
    public int RefreshReuseLeewaySeconds { get; init; } = 10;

    [Required]
    public string RefreshCookieName { get; init; } = "docmind_refresh";
}
