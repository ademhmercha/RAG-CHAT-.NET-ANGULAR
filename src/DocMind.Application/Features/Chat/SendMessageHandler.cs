using System.Runtime.CompilerServices;
using DocMind.Application.Abstractions;
using DocMind.Application.Features.Conversations;
using DocMind.Application.Features.Retrieval;
using DocMind.Application.Features.Tools;
using DocMind.Domain.Common;
using DocMind.Domain.Conversations;
using DocMind.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocMind.Application.Features.Chat;

public sealed class SendMessageHandler(
    IAppDbContext db,
    IChatClient chatClient,
    SearchDocumentsHandler search,
    ChatToolFactory toolFactory,
    TimeProvider time,
    IOptions<ChatSettings> settings,
    IOptions<RagSettings> rag,
    ILogger<SendMessageHandler> logger)
{
    public const string SearchToolName = "search_documents";

    private const int TitleMaxLength = 60;
    private const int CharactersPerToken = 4;

    public async Task<Result<IAsyncEnumerable<ChatEvent>>> HandleAsync(
        Guid userId,
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var conversation = await db.Conversations
            .SingleOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId, cancellationToken);

        if (conversation is null)
        {
            return ConversationErrors.NotFound(conversationId);
        }

        var scope = await ResolveScopeAsync(userId, request.DocumentIds, cancellationToken);
        if (scope.IsFailure)
        {
            return scope.Error;
        }

        var history = await LoadHistoryAsync(conversationId, cancellationToken);
        var content = request.Content.Trim();
        var userMessageAt = time.GetUtcNow();

        conversation.AddMessage(MessageRole.User, content, null, null, userMessageAt);
        if (history.Count == 0 && string.Equals(conversation.Title, Conversation.DefaultTitle, StringComparison.Ordinal))
        {
            conversation.Rename(DeriveTitle(content), userMessageAt);
        }

        await db.SaveChangesAsync(cancellationToken);

        var turn = new ChatTurn(userId, conversation, content, history, scope.Value, userMessageAt);
        return Result.Success(StreamAsync(turn, cancellationToken));
    }

    private static string DeriveTitle(string content)
    {
        var singleLine = string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length <= TitleMaxLength ? singleLine : string.Concat(singleLine.AsSpan(0, TitleMaxLength - 1), "…");
    }

    private static int EstimateTokens(int characters) => (characters + CharactersPerToken - 1) / CharactersPerToken;

    private static TokenUsage EstimateUsage(IEnumerable<ChatMessage> prompt, string answer) =>
        new(EstimateTokens(prompt.Sum(m => m.Text.Length)), EstimateTokens(answer.Length));

    private static List<ChatMessage> BuildPrompt(List<ChatMessage> history, string userTurn)
    {
        var prompt = new List<ChatMessage>(history.Count + 2) { new(ChatRole.System, RagPromptBuilder.SystemPrompt) };
        prompt.AddRange(history);
        prompt.Add(new ChatMessage(ChatRole.User, userTurn));
        return prompt;
    }

    private async Task<Result<IReadOnlyCollection<Guid>?>> ResolveScopeAsync(
        Guid userId,
        IReadOnlyList<Guid>? requested,
        CancellationToken cancellationToken)
    {
        if (requested is not { Count: > 0 })
        {
            return Result.Success<IReadOnlyCollection<Guid>?>(null);
        }

        var ids = requested.Distinct().ToList();
        var owned = await db.Documents
            .AsNoTracking()
            .Where(d => d.UserId == userId && ids.Contains(d.Id))
            .Select(d => new { d.Id, d.Status })
            .ToListAsync(cancellationToken);

        var missing = ids.FirstOrDefault(id => owned.All(d => d.Id != id));
        if (missing != Guid.Empty)
        {
            return Result.Failure<IReadOnlyCollection<Guid>?>(DocumentErrors.NotFound(missing));
        }

        var notReady = owned.FirstOrDefault(d => d.Status != DocumentStatus.Ready);
        return notReady is not null
            ? Result.Failure<IReadOnlyCollection<Guid>?>(DocumentErrors.NotReady(notReady.Id))
            : Result.Success<IReadOnlyCollection<Guid>?>(ids);
    }

    private async Task<List<ChatMessage>> LoadHistoryAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var recent = await db.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => EF.Property<long>(m, MessageOrdering.Column))
            .Take(settings.Value.HistoryMessages)
            .Select(m => new { m.Role, m.Content })
            .ToListAsync(cancellationToken);

        recent.Reverse();

        return recent
            .Select(m => new ChatMessage(m.Role == MessageRole.User ? ChatRole.User : ChatRole.Assistant, m.Content))
            .ToList();
    }

    private async Task<bool?> ProbeAsync(ChatTurn turn, CancellationToken cancellationToken)
    {
        try
        {
            return await search.HasSearchableDocumentsAsync(turn.UserId, turn.Scope, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Checking the searchable documents failed for conversation {ConversationId}.", turn.Conversation.Id);
            return null;
        }
    }

    private async Task<(IReadOnlyList<RetrievedChunk> Sources, Error? Error)> RetrieveAsync(ChatTurn turn, CancellationToken cancellationToken)
    {
        var previous = turn.History.LastOrDefault(m => m.Role == ChatRole.User)?.Text;
        var query = RagPromptBuilder.BuildSearchQuery(turn.Content, previous, rag.Value.ShortQueryCharacters);

        try
        {
            var sources = await search.HandleAsync(turn.UserId, new SearchDocumentsQuery(query, turn.Scope), cancellationToken);
            return (sources, null);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Retrieving sources failed for conversation {ConversationId}.", turn.Conversation.Id);
            return ([], ChatErrors.ProviderUnavailable);
        }
    }

    private async IAsyncEnumerable<ChatEvent> StreamAsync(ChatTurn turn, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var processor = new ChatStreamProcessor();
        var settled = false;

        try
        {
            var searchable = await ProbeAsync(turn, cancellationToken);
            if (searchable is null)
            {
                settled = true;
                yield return new ErrorChatEvent(ChatErrors.ProviderUnavailable);
                yield break;
            }

            IReadOnlyList<RetrievedChunk> sources = [];
            if (searchable.Value)
            {
                yield return new ToolChatEvent(SearchToolName, ToolStatus.Running);
                var retrieval = await RetrieveAsync(turn, cancellationToken);
                yield return new ToolChatEvent(SearchToolName, retrieval.Error is null ? ToolStatus.Completed : ToolStatus.Failed);

                if (retrieval.Error is not null)
                {
                    settled = true;
                    yield return new ErrorChatEvent(retrieval.Error);
                    yield break;
                }

                sources = retrieval.Sources;
            }

            var userTurn = RagPromptBuilder.BuildUserTurn(turn.Content, sources, searchable.Value, RagPromptBuilder.NewBoundary(), rag.Value.MaxContextCharacters);
            var prompt = BuildPrompt(turn.History, userTurn);
            var options = new ChatOptions
            {
                Temperature = settings.Value.Temperature,
                MaxOutputTokens = settings.Value.MaxOutputTokens,
                Tools = settings.Value.ToolsEnabled ? toolFactory.Create(turn.UserId, turn.Scope) : null,
            };

            var run = new ModelRun();
            await foreach (var chatEvent in RunModelAsync(prompt, options, processor, run, turn, cancellationToken))
            {
                yield return chatEvent;
            }

            var failure = run.Failure;
            var cancelled = run.Cancelled;

            if (cancelled)
            {
                yield break;
            }

            if (failure is not null || !processor.HasText)
            {
                settled = true;
                yield return new ErrorChatEvent(failure ?? ChatErrors.EmptyResponse);
                yield break;
            }

            var citations = CitationExtractor.Extract(processor.Text, sources, rag.Value);
            foreach (var citation in citations)
            {
                yield return new CitationChatEvent(citation);
            }

            var usage = processor.Usage ?? EstimateUsage(prompt, processor.Text);
            var (message, saveError) = await SaveAnswerAsync(turn, processor.Text, citations, usage);
            settled = true;

            yield return message is null
                ? new ErrorChatEvent(saveError ?? ChatErrors.PersistenceFailed)
                : new DoneChatEvent(message.Id, usage);
        }
        finally
        {
            if (!settled && processor.HasText)
            {
                logger.LogInformation("The client left before the answer for conversation {ConversationId} was complete; keeping the partial answer.", turn.Conversation.Id);
                await SavePartialAnswerAsync(turn, processor);
            }
        }
    }

    private async IAsyncEnumerable<ChatEvent> RunModelAsync(
        List<ChatMessage> prompt,
        ChatOptions options,
        ChatStreamProcessor processor,
        ModelRun run,
        ChatTurn turn,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var updates = chatClient.GetStreamingResponseAsync(prompt, options, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            IReadOnlyList<ChatEvent> events;

            try
            {
                if (!await updates.MoveNextAsync())
                {
                    yield break;
                }

                events = processor.Process(updates.Current);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                run.Cancelled = true;
                yield break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The chat provider failed for conversation {ConversationId}.", turn.Conversation.Id);
                run.Failure = ChatErrors.ProviderUnavailable;
                yield break;
            }

            foreach (var chatEvent in events)
            {
                yield return chatEvent;
            }
        }
    }

    private DateTimeOffset NextTimestamp(DateTimeOffset userMessageAt)
    {
        var now = time.GetUtcNow();
        return now > userMessageAt ? now : userMessageAt.AddTicks(TimeSpan.TicksPerMicrosecond);
    }

    private async Task<(Message? Message, Error? Error)> SaveAnswerAsync(
        ChatTurn turn,
        string text,
        IReadOnlyList<Citation> citations,
        TokenUsage usage)
    {
        try
        {
            var message = turn.Conversation.AddMessage(MessageRole.Assistant, text, citations, usage, NextTimestamp(turn.UserMessageAt));
            await db.SaveChangesAsync(CancellationToken.None);
            return (message, null);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Saving the answer failed for conversation {ConversationId}.", turn.Conversation.Id);
            return (null, ChatErrors.PersistenceFailed);
        }
    }

    private async Task SavePartialAnswerAsync(ChatTurn turn, ChatStreamProcessor processor)
    {
        try
        {
            turn.Conversation.AddMessage(MessageRole.Assistant, processor.Text, null, processor.Usage, NextTimestamp(turn.UserMessageAt));
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Saving the interrupted answer failed for conversation {ConversationId}.", turn.Conversation.Id);
        }
    }

    private sealed class ModelRun
    {
        public Error? Failure { get; set; }

        public bool Cancelled { get; set; }
    }

    private sealed record ChatTurn(
        Guid UserId,
        Conversation Conversation,
        string Content,
        List<ChatMessage> History,
        IReadOnlyCollection<Guid>? Scope,
        DateTimeOffset UserMessageAt);
}
