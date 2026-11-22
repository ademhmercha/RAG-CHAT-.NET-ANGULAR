using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DocMind.Api.Configuration;

internal sealed class ApiInfoTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "DocMind API",
            Version = "v1",
            Description = "Document-grounded AI assistant: upload documents, ask questions, get cited answers.",
        };

        return Task.CompletedTask;
    }
}
