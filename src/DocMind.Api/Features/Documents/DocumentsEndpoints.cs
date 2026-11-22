using DocMind.Api.Common;
using DocMind.Api.Configuration;
using DocMind.Application.Abstractions;
using DocMind.Application.Features.Documents;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.Options;

namespace DocMind.Api.Features.Documents;

internal static class DocumentsEndpoints
{
    public const long MultipartOverheadBytes = 1024 * 1024;

    public static RouteGroupBuilder MapDocumentsEndpoints(this RouteGroupBuilder api, long maxUploadBytes)
    {
        var group = api.MapGroup("/documents").WithTags("Documents");

        group.MapPost("/", UploadAsync)
            .DisableAntiforgery()
            .RequireRateLimiting(RateLimiting.UploadPolicy)
            .WithMetadata(new UploadSizeLimit(maxUploadBytes + MultipartOverheadBytes))
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<DocumentDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status415UnsupportedMediaType);

        group.MapGet("/", ListAsync)
            .AddEndpointFilter<ValidationFilter<ListDocumentsRequest>>()
            .Produces<PagedResult<DocumentDto>>()
            .ProducesValidationProblem();

        group.MapGet("/events", StreamEventsAsync)
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream");

        group.MapGet("/{id:guid}", GetAsync)
            .Produces<DocumentDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<IResult> UploadAsync(
        IFormFile? file,
        ICurrentUser user,
        UploadDocumentHandler handler,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["file"] = ["A file is required."] },
                title: "One or more validation errors occurred.",
                type: ProblemTypes.ForStatus(StatusCodes.Status400BadRequest));
        }

        await using var content = file.OpenReadStream();
        var result = await handler.HandleAsync(user.RequiredUserId, new UploadDocumentCommand(file.FileName, file.Length, content), cancellationToken);

        return result.ToHttpResult(document => Results.Created($"/api/v1/documents/{document.Id}", document));
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] ListDocumentsRequest request,
        ICurrentUser user,
        ListDocumentsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(user.RequiredUserId, request, cancellationToken));

    private static async Task<IResult> GetAsync(
        Guid id,
        ICurrentUser user,
        GetDocumentHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(user.RequiredUserId, id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        ICurrentUser user,
        DeleteDocumentHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(user.RequiredUserId, id, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.ToProblem();
    }

    private static async Task<IResult> StreamEventsAsync(
        HttpContext http,
        ICurrentUser user,
        StreamDocumentEventsHandler handler,
        IOptions<JsonOptions> json,
        IOptions<SseOptions> sse,
        CancellationToken cancellationToken)
    {
        var subscription = await handler.HandleAsync(user.RequiredUserId, cancellationToken);
        var options = json.Value.SerializerOptions;
        var traceId = ProblemDetailsCustomizer.TraceIdFor(http);

        return new ServerSentEventsResult(
            DocumentSseMapper.MapAsync(subscription, options, cancellationToken),
            _ => ChatSseMapperFault.Create(options, traceId),
            TimeSpan.FromMilliseconds(sse.Value.HeartbeatMilliseconds));
    }

    private sealed class UploadSizeLimit(long maxBytes) : IRequestSizeLimitMetadata
    {
        public long? MaxRequestBodySize { get; } = maxBytes;
    }
}
