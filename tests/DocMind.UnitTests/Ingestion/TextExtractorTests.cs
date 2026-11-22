using DocMind.Application.Abstractions;
using DocMind.Application.Features.Ingestion;
using DocMind.Infrastructure.Ingestion;
using DocMind.TestSupport;
using Microsoft.Extensions.Options;

namespace DocMind.UnitTests.Ingestion;

public sealed class TextExtractorTests
{
    private static DocumentTextExtractor CreateExtractor(int maxPages = 100, int maxCharacters = 1_000_000) =>
        new(Options.Create(new IngestionSettings { MaxPages = maxPages, MaxCharacters = maxCharacters }));

    private static async Task<ExtractedDocument> ExtractAsync(byte[] bytes, string contentType, DocumentTextExtractor? extractor = null)
    {
        await using var stream = new MemoryStream(bytes);
        return await (extractor ?? CreateExtractor()).ExtractAsync(stream, contentType, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Pdf_ReturnsTheTextOfEachPageWithItsNumber()
    {
        var result = await ExtractAsync(TestDocuments.Pdf("Quarterly revenue grew", "Operating costs fell", "Outlook remains positive"), TestDocuments.PdfContentType);

        result.PageCount.Should().Be(3);
        result.Pages.Select(p => p.Number).Should().Equal(1, 2, 3);
        result.Pages[0].Text.Should().Contain("Quarterly revenue grew");
        result.Pages[1].Text.Should().Contain("Operating costs fell");
        result.Pages[2].Text.Should().Contain("Outlook remains positive");
    }

    [Fact]
    public async Task Pdf_SkipsBlankPagesButCountsThem()
    {
        var result = await ExtractAsync(TestDocuments.BlankPdf(3), TestDocuments.PdfContentType);

        result.PageCount.Should().Be(3);
        result.Pages.Should().BeEmpty();
    }

    [Fact]
    public async Task Pdf_RejectsCorruptFilesWithAReadableMessage()
    {
        var act = () => ExtractAsync(TestDocuments.CorruptPdf(), TestDocuments.PdfContentType);

        (await act.Should().ThrowAsync<UnsupportedDocumentException>()).Which.Message.Should().Contain("corrupt");
    }

    [Fact]
    public async Task Pdf_EnforcesThePageLimit()
    {
        var act = () => ExtractAsync(TestDocuments.Pdf("a", "b", "c"), TestDocuments.PdfContentType, CreateExtractor(maxPages: 2));

        (await act.Should().ThrowAsync<UnsupportedDocumentException>()).Which.Message.Should().Contain("2 pages");
    }

    [Fact]
    public async Task Docx_SplitsPagesOnPageBreaks()
    {
        var result = await ExtractAsync(TestDocuments.Docx("Introduction text\nSecond line", "Chapter two content"), TestDocuments.DocxContentType);

        result.Pages.Should().HaveCount(2);
        result.Pages[0].Number.Should().Be(1);
        result.Pages[0].Text.Should().Contain("Introduction text").And.Contain("Second line");
        result.Pages[1].Number.Should().Be(2);
        result.Pages[1].Text.Should().Contain("Chapter two content");
        result.PageCount.Should().Be(2);
    }

    [Fact]
    public async Task Docx_WithoutBreaksIsASinglePage()
    {
        var result = await ExtractAsync(TestDocuments.Docx("Only page"), TestDocuments.DocxContentType);

        result.Pages.Should().ContainSingle().Which.Number.Should().Be(1);
    }

    [Fact]
    public async Task Docx_RejectsCorruptFiles()
    {
        var act = () => ExtractAsync(TestDocuments.CorruptDocx(), TestDocuments.DocxContentType);

        await act.Should().ThrowAsync<UnsupportedDocumentException>();
    }

    [Fact]
    public async Task Docx_EnforcesTheCharacterLimit()
    {
        var act = () => ExtractAsync(TestDocuments.Docx(new string('a', 5000)), TestDocuments.DocxContentType, CreateExtractor(maxCharacters: 1000));

        (await act.Should().ThrowAsync<UnsupportedDocumentException>()).Which.Message.Should().Contain("too much text");
    }

    [Fact]
    public async Task UnknownContentTypesAreRejected()
    {
        var act = () => ExtractAsync([1, 2, 3], "text/plain");

        await act.Should().ThrowAsync<UnsupportedDocumentException>();
    }
}
