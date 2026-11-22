using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace DocMind.TestSupport;

public static class TestDocuments
{
    public const string PdfContentType = "application/pdf";
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private const int LinesPerPage = 40;
    private const double LeftMargin = 40;
    private const double TopMargin = 800;
    private const double LineHeight = 18;

    public static byte[] Pdf(params string[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);

        foreach (var text in pages)
        {
            var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
            var lines = text.Split('\n');

            for (var i = 0; i < lines.Length && i < LinesPerPage; i++)
            {
                if (lines[i].Length > 0)
                {
                    page.AddText(lines[i], 11, new PdfPoint(LeftMargin, TopMargin - (i * LineHeight)), font);
                }
            }
        }

        return builder.Build();
    }

    public static byte[] BlankPdf(int pages = 1)
    {
        var builder = new PdfDocumentBuilder();
        for (var i = 0; i < pages; i++)
        {
            builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        }

        return builder.Build();
    }

    public static byte[] Docx(params string[] pages)
    {
        using var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            var body = new Body();

            for (var i = 0; i < pages.Length; i++)
            {
                foreach (var line in pages[i].Split('\n'))
                {
                    body.Append(new Paragraph(new Run(new Text(line) { Space = SpaceProcessingModeValues.Preserve })));
                }

                if (i < pages.Length - 1)
                {
                    body.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                }
            }

            main.Document = new Document(body);
            main.Document.Save();
        }

        return stream.ToArray();
    }

    public static byte[] CorruptPdf() => "%PDF-1.4\nthis is definitely not a valid pdf body"u8.ToArray();

    public static byte[] CorruptDocx() => [0x50, 0x4B, 0x03, 0x04, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05];

    public static string Sentences(string topic, int count) =>
        string.Join(' ', Enumerable.Range(1, count).Select(i => $"Sentence {i} about {topic} explains an important detail of the subject."));
}
