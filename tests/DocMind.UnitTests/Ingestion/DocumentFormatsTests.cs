using System.Text;
using DocMind.Application.Features.Documents;
using DocMind.Application.Features.Ingestion;

namespace DocMind.UnitTests.Ingestion;

public sealed class DocumentFormatsTests
{
    [Theory]
    [InlineData("report.pdf", DocumentFormats.Pdf)]
    [InlineData("REPORT.PDF", DocumentFormats.Pdf)]
    [InlineData("notes.docx", DocumentFormats.Docx)]
    [InlineData("archive.tar.docx", DocumentFormats.Docx)]
    public void ContentTypeForExtension_RecognisesSupportedFormats(string fileName, string expected) =>
        DocumentFormats.ContentTypeForExtension(fileName).Should().Be(expected);

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("legacy.doc")]
    [InlineData("noextension")]
    [InlineData("")]
    public void ContentTypeForExtension_RejectsEverythingElse(string fileName) =>
        DocumentFormats.ContentTypeForExtension(fileName).Should().BeNull();

    [Fact]
    public void MatchesSignature_ChecksTheMagicBytes()
    {
        DocumentFormats.MatchesSignature(DocumentFormats.Pdf, "%PDF-1.7"u8).Should().BeTrue();
        DocumentFormats.MatchesSignature(DocumentFormats.Pdf, "PK\u0003\u0004"u8).Should().BeFalse();
        DocumentFormats.MatchesSignature(DocumentFormats.Docx, [0x50, 0x4B, 0x03, 0x04, 0x14]).Should().BeTrue();
        DocumentFormats.MatchesSignature(DocumentFormats.Docx, "%PDF-"u8).Should().BeFalse();
        DocumentFormats.MatchesSignature(DocumentFormats.Pdf, []).Should().BeFalse();
        DocumentFormats.MatchesSignature("text/plain", "%PDF-"u8).Should().BeFalse();
    }

    [Theory]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData("  spaced name .pdf  ", "spaced name .pdf")]
    [InlineData("C:\\Users\\ada\\secret.pdf", "secret.pdf")]
    [InlineData("../../etc/passwd.pdf", "passwd.pdf")]
    [InlineData("tab\there\u0007.pdf", "tabhere.pdf")]
    [InlineData("", "document")]
    [InlineData(null, "document")]
    [InlineData("   ", "document")]
    public void SanitizeFileName_StripsPathsAndControlCharacters(string? input, string expected) =>
        DocumentFormats.SanitizeFileName(input, 260).Should().Be(expected);

    [Fact]
    public void SanitizeFileName_TruncatesButKeepsTheExtension()
    {
        var result = DocumentFormats.SanitizeFileName($"{new string('a', 300)}.pdf", 50);

        result.Should().HaveLength(50);
        result.Should().EndWith(".pdf");
    }

    [Fact]
    public void IngestionMessage_RoundTripsAndRejectsGarbage()
    {
        var message = new IngestionMessage(Guid.NewGuid(), Guid.NewGuid());

        IngestionMessage.TryParse(message.Serialize()).Should().Be(message);
        IngestionMessage.TryParse("not json").Should().BeNull();
        IngestionMessage.TryParse("{}").Should().BeNull();
        IngestionMessage.TryParse(Encoding.UTF8.GetString(Encoding.UTF8.GetBytes("null"))).Should().BeNull();
    }

    [Fact]
    public void IngestionSettings_RequireOverlapSmallerThanChunkSize()
    {
        var validator = new IngestionSettingsValidator();

        validator.Validate(null, new IngestionSettings { ChunkSize = 500, ChunkOverlap = 100 }).Succeeded.Should().BeTrue();
        validator.Validate(null, new IngestionSettings { ChunkSize = 500, ChunkOverlap = 500 }).Failed.Should().BeTrue();
    }
}
