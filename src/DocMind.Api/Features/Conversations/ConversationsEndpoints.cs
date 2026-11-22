using DocMind.Api.Common;
using DocMind.Api.Configuration;
using DocMind.Application.Abstractions;
using DocMind.Application.Features.Chat;
using DocMind.Application.Features.Conversations;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace DocMind.Api.Features.Conversations;

internal static class ConversationsEndpoints
{
    public static RouteGroupBuilder MapConversationsEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/conversations").WithTags("Conversations");

        group.MapPost("/", CreateAsync)
            .AddEndpointFilter<ValidationFilter<CreateConversationRequest>>()
            .Produces<ConversationSummaryDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/", ListAsync)
            .Produces<IReadOnlyList<ConversationSummaryDto>>();

        group.MapGet("/{id:guid}", GetAsync)
            .Produces<ConversationDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}", RenameAsync)
            .AddEndpointFilter<ValidationFilter<RenameConversationRequest>>()
            .Produces<ConversationSummaryDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/messages", SendMessageAsync)
            .RequireRateLimiting(RateLimiting.ChatPolicy)
            .AddEndpointFilter<ValidationFilter<SendMessageRequest>>()
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<IResult> CreateAsync(
        CreateConversationRequest? request,
        ICurrentUser user,
        CreateConversationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(user.RequiredUserId, request, cancellationToken);
        return result.ToHttpResult(conversation => Results.Created($"/api/v1/conversations/{conversation.Id}", conversation));
    }

    private static async Task<IResult> ListAsync(
        ICurrentUser user,
        ListConversationsHandler handler,
        CancellationToken cancellationToken) =>
        Results.Ok(await handler.HandleAsync(user.RequiredUserId, cancellationToken));

    private static async Task<IResult> GetAsync(
        Guid id,
        ICurrentUser user,
        GetConversationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(user.RequiredUserId, id, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> RenameAsync(
        Guid id,
        RenameConversationRequest request,
        ICurrentUser user,
        RenameConversationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(user.RequiredUserId, id, request, cancellationToken);
        return result.ToHttpResult(Results.Ok);
    }

    private static async Task<IResult> DeleteAsync(
        Guid id,
        ICurrentUser user,
        DeleteConversationHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(user.RequiredUserId, id, cancellationToken);
        return result.IsSuccess ? Results.NoContent() : result.ToProblem();
    }

    private static async Task<IResult> SendMessageAsync(
        Guid id,
        SendMessageRequest request,
        HttpContext http,
        ICurrentUser user,
        SendMessageHandler handler,
        IOptions<JsonOptions> json,
        IOptions<SseOptions> sse,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(user.RequiredUserId, id, request, cancellationToken);
        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        var options = json.Value.SerializerOptions;
        var traceId = ProblemDetailsCustomizer.TraceIdFor(http);

        return new ServerSentEventsResult(
            ChatSseMapper.MapAsync(result.Value, options, traceId, cancellationToken),
            _ => ChatSseMapper.Fault(options, traceId),
            TimeSpan.FromMilliseconds(sse.Value.HeartbeatMilliseconds));
    }
}
