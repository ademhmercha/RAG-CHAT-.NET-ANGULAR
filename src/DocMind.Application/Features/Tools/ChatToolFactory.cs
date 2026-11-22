using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace DocMind.Application.Features.Tools;

public sealed class ChatToolFactory(DocumentTools tools)
{
    public IList<AITool> Create(Guid userId, IReadOnlyCollection<Guid>? scope) =>
    [
        AIFunctionFactory.Create(
            async (
                [Description(ToolDefinitions.QueryDescription)] string query,
                [Description(ToolDefinitions.MaxResultsDescription)] int? maxResults = null,
                CancellationToken cancellationToken = default) =>
                await tools.SearchAsync(userId, scope, query, maxResults, cancellationToken),
            new AIFunctionFactoryOptions { Name = ToolDefinitions.SearchName, Description = ToolDefinitions.SearchDescription }),

        AIFunctionFactory.Create(
            async (
                [Description(ToolDefinitions.DocumentIdDescription)] Guid documentId,
                CancellationToken cancellationToken = default) =>
                await tools.SummarizeAsync(userId, scope, documentId, cancellationToken),
            new AIFunctionFactoryOptions { Name = ToolDefinitions.SummarizeName, Description = ToolDefinitions.SummarizeDescription }),
    ];
}
