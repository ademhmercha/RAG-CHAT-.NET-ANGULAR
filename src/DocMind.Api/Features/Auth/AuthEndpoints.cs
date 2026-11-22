using DocMind.Api.Common;
using DocMind.Api.Configuration;
using DocMind.Application.Abstractions;
using DocMind.Application.Features.Auth;
using Microsoft.Extensions.Options;

namespace DocMind.Api.Features.Auth;

internal static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimiting.AuthPolicy)
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .Produces<UserDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimiting.AuthPolicy)
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .Produces<AuthResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimiting.AuthPolicy)
            .Produces<AuthResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimiting.AuthPolicy)
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", GetCurrentUserAsync)
            .Produces<UserDto>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return api;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        RegisterHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request, cancellationToken);
        return result.ToHttpResult(user => Results.Json(user, statusCode: StatusCodes.Status201Created));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginHandler handler,
        IOptions<JwtOptions> jwt,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request, cancellationToken);
        return CompleteAuthentication(result, http, jwt.Value);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshHandler handler,
        IOptions<JwtOptions> jwt,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(RefreshTokenCookie.Read(http.Request, jwt.Value), cancellationToken);
        return CompleteAuthentication(result, http, jwt.Value);
    }

    private static async Task<IResult> LogoutAsync(
        LogoutHandler handler,
        IOptions<JwtOptions> jwt,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(RefreshTokenCookie.Read(http.Request, jwt.Value), cancellationToken);
        RefreshTokenCookie.Delete(http.Response, jwt.Value);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        ICurrentUser currentUser,
        GetCurrentUserHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(currentUser.RequiredUserId, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static IResult CompleteAuthentication(Domain.Common.Result<AuthResult> result, HttpContext http, JwtOptions options)
    {
        http.Response.Headers.CacheControl = "no-store";

        if (result.IsFailure)
        {
            RefreshTokenCookie.Delete(http.Response, options);
            return result.Error.ToProblem();
        }

        RefreshTokenCookie.Append(http.Response, result.Value.RefreshToken, options);
        return Results.Ok(AuthResponse.From(result.Value));
    }
}
