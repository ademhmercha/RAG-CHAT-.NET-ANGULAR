using DocMind.Application.Features.Documents;
using DocMind.Application.Features.Retrieval;
using Microsoft.Extensions.Logging;

namespace DocMind.Application.Features.Tools;

public sealed record ToolSource(Guid DocumentId, string DocumentName, int Page, string Text, double Score);

public sealed record ToolSummary(Guid DocumentId, string? Summary, string? Error);

public static class ToolDefinitions
{
    public const string SearchName = "search_documents";
    public const string SummarizeName = "summarize_document";

    public const string SearchDescription =
        "Searches the user's own documents and returns the most relevant passages with their document name and page number. "
        + "Use it to find facts before answering questions about the documents.";

    public const string SummarizeDescription =
        "Returns a concise summary of one of the user's documents, identified by its document identifier.";

    public const string QueryDescription = "What to look for, phrased as a short question or a list of keywords.";
    public const string MaxResultsDescription = "Maximum number of passages to return, between 1 and 10 (default 5).";
    public const string DocumentIdDescription = "The identifier (GUID) of the document to summarize.";
}

public sealed class DocumentTools(
    SearchDocumentsHandler search,
    SummarizeDocumentHandler summarize,
    ILogger<DocumentTools> logger)
{
    private const int DefaultResults = 5;
    private const int MaxResults = 10;
    private const int MaxPassageCharacters = 1500;
    private const string SummaryFailure = "The summary could not be generated right now.";

    public async Task<IReadOnlyList<ToolSource>> SearchAsync(
        Guid userId,
        IReadOnlyCollection<Guid>? scope,
        string query,
        int? maxResults,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(maxResults ?? DefaultResults, 1, MaxResults);
        var chunks = await search.HandleAsync(userId, new SearchDocumentsQuery(query, scope, limit), cancellationToken);

        return chunks
            .Select(c => new ToolSource(c.DocumentId, c.DocumentName, c.Page, Truncate(c.Content), Math.Round(c.Score, 3)))
            .ToList();
    }

    public async Task<ToolSummary> SummarizeAsync(
        Guid userId,
        IReadOnlyCollection<Guid>? scope,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        if (scope is { Count: > 0 } && !scope.Contains(documentId))
        {
            return new ToolSummary(documentId, null, "Document not found.");
        }

        try
        {
            var result = await summarize.HandleAsync(userId, documentId, cancellationToken);
            return result.IsSuccess
                ? new ToolSummary(documentId, result.Value, null)
                : new ToolSummary(documentId, null, result.Error.Description);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Summarizing document {DocumentId} failed.", documentId);
            return new ToolSummary(documentId, null, SummaryFailure);
        }
    }

    private static string Truncate(string text) =>
        text.Length <= MaxPassageCharacters ? text : string.Concat(text.AsSpan(0, MaxPassageCharacters - 1), "…");
}
