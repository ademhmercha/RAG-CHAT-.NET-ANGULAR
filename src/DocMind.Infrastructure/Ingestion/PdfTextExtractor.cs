using DocMind.Application.Abstractions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DocMind.Infrastructure.Ingestion;

internal static class PdfTextExtractor
{
    public static ExtractedDocument Extract(Stream stream, ExtractionLimits limits, CancellationToken cancellationToken)
    {
        try
        {
            using var pdf = PdfDocument.Open(stream, new ParsingOptions { UseLenientParsing = true });

            limits.EnsureWithin(pdf.NumberOfPages, 0);

            var pages = new List<ExtractedPage>(pdf.NumberOfPages);
            long characters = 0;

            foreach (var page in pdf.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var text = ContentOrderTextExtractor.GetText(page);
                characters += text.Length;
                limits.EnsureWithin(0, characters);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    pages.Add(new ExtractedPage(page.Number, text));
                }
            }

            return new ExtractedDocument(pdf.NumberOfPages, pages);
        }
        catch (PdfDocumentEncryptedException exception)
        {
            throw new UnsupportedDocumentException("Password-protected PDF documents are not supported.", exception);
        }
        catch (Exception exception) when (exception is not (UnsupportedDocumentException or OperationCanceledException))
        {
            throw new UnsupportedDocumentException("The PDF file is corrupt or uses an unsupported format.", exception);
        }
    }
}
