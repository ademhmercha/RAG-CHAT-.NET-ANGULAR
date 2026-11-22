using System.ComponentModel;
using DocMind.Application.Features.Tools;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

namespace DocMind.McpServer;

[McpServerToolType]
public static class DocumentMcpTools
{
    [McpServerTool(Name = ToolDefinitions.SearchName), Description(ToolDefinitions.SearchDescription)]
    public static async Task<IReadOnlyList<ToolSource>> SearchDocuments(
        DocumentTools tools,
        IOptions<McpUserOptions> user,
        [Description(ToolDefinitions.QueryDescription)] string query,
        [Description(ToolDefinitions.MaxResultsDescription)] int? maxResults = null,
        CancellationToken cancellationToken = default) =>
        await tools.SearchAsync(user.Value.UserId, null, query, maxResults, cancellationToken);

    [McpServerTool(Name = ToolDefinitions.SummarizeName), Description(ToolDefinitions.SummarizeDescription)]
    public static async Task<ToolSummary> SummarizeDocument(
        DocumentTools tools,
        IOptions<McpUserOptions> user,
        [Description(ToolDefinitions.DocumentIdDescription)] Guid documentId,
        CancellationToken cancellationToken = default) =>
        await tools.SummarizeAsync(user.Value.UserId, null, documentId, cancellationToken);
}
