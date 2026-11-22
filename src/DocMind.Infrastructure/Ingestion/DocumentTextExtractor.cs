using DocMind.Application.Abstractions;
using DocMind.Application.Features.Documents;
using DocMind.Application.Features.Ingestion;
using Microsoft.Extensions.Options;

namespace DocMind.Infrastructure.Ingestion;

internal sealed class DocumentTextExtractor(IOptions<IngestionSettings> settings) : IDocumentTextExtractor
{
    public async Task<ExtractedDocument> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken)
    {
        var limits = new ExtractionLimits(settings.Value.MaxPages, settings.Value.MaxCharacters);

        var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Seek(0, SeekOrigin.Begin);

        await using (buffer)
        {
            return await Task.Run(() => Extract(buffer, contentType, limits, cancellationToken), cancellationToken);
        }
    }

    private static ExtractedDocument Extract(Stream stream, string contentType, ExtractionLimits limits, CancellationToken cancellationToken) =>
        contentType switch
        {
            DocumentFormats.Pdf => PdfTextExtractor.Extract(stream, limits, cancellationToken),
            DocumentFormats.Docx => DocxTextExtractor.Extract(stream, limits, cancellationToken),
            _ => throw new UnsupportedDocumentException("Only PDF and DOCX documents are supported."),
        };
}

internal sealed record ExtractionLimits(int MaxPages, int MaxCharacters)
{
    public void EnsureWithin(int pages, long characters)
    {
        if (pages > MaxPages)
        {
            throw new UnsupportedDocumentException($"The document exceeds the maximum of {MaxPages} pages.");
        }

        if (characters > MaxCharacters)
        {
            throw new UnsupportedDocumentException("The document contains too much text to process.");
        }
    }
}
