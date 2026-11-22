using System.Text;
using DocMind.Application.Abstractions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace DocMind.Infrastructure.Ingestion;

internal static class DocxTextExtractor
{
    public static ExtractedDocument Extract(Stream stream, ExtractionLimits limits, CancellationToken cancellationToken)
    {
        try
        {
            using var document = WordprocessingDocument.Open(stream, isEditable: false);
            var body = document.MainDocumentPart?.Document?.Body
                ?? throw new UnsupportedDocumentException("The DOCX file does not contain a document body.");

            return Read(body, limits, cancellationToken);
        }
        catch (Exception exception) when (exception is not (UnsupportedDocumentException or OperationCanceledException))
        {
            throw new UnsupportedDocumentException("The DOCX file is corrupt or uses an unsupported format.", exception);
        }
    }

    private static ExtractedDocument Read(Body body, ExtractionLimits limits, CancellationToken cancellationToken)
    {
        var pages = new List<ExtractedPage>();
        var current = new StringBuilder();
        var pageNumber = 1;
        long characters = 0;

        void CloseAfterBreak()
        {
            if (string.IsNullOrWhiteSpace(current.ToString()))
            {
                return;
            }

            pages.Add(new ExtractedPage(pageNumber, current.ToString()));
            current.Clear();
            pageNumber++;
            limits.EnsureWithin(pageNumber, characters);
        }

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var element in paragraph.Descendants())
            {
                switch (element)
                {
                    case Text text:
                        current.Append(text.Text);
                        characters += text.Text.Length;
                        break;
                    case TabChar:
                        current.Append('\t');
                        break;
                    case Break pageBreak when pageBreak.Type?.Value == BreakValues.Page:
                    case LastRenderedPageBreak:
                        CloseAfterBreak();
                        break;
                    case Break:
                    case CarriageReturn:
                        current.Append('\n');
                        break;
                }
            }

            current.Append('\n');
            limits.EnsureWithin(0, characters);
        }

        if (!string.IsNullOrWhiteSpace(current.ToString()))
        {
            pages.Add(new ExtractedPage(pageNumber, current.ToString()));
        }

        return new ExtractedDocument(pages.Count == 0 ? 1 : pages[^1].Number, pages);
    }
}
