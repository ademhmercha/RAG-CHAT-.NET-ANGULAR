using DocMind.Application.Abstractions;
using DocMind.Application.Features.Ingestion;

namespace DocMind.UnitTests.Ingestion;

public sealed class TextChunkerTests
{
    private static string Words(int count) => string.Join(' ', Enumerable.Range(1, count).Select(i => $"word{i}"));

    private static IReadOnlyList<TextChunk> Chunk(string text, int size = 200, int overlap = 40, int page = 1) =>
        TextChunker.Chunk([new ExtractedPage(page, text)], size, overlap);

    [Fact]
    public void Chunk_KeepsShortTextInASingleChunk()
    {
        var chunks = Chunk("A short paragraph.");

        chunks.Should().ContainSingle().Which.Should().Be(new TextChunk(0, 1, "A short paragraph."));
    }

    [Fact]
    public void Chunk_NeverExceedsTheConfiguredSize()
    {
        var chunks = Chunk(Words(500), size: 150, overlap: 30);

        chunks.Should().HaveCountGreaterThan(5);
        chunks.Should().OnlyContain(c => c.Text.Length <= 150);
    }

    [Fact]
    public void Chunk_OverlapsConsecutiveChunks()
    {
        var chunks = Chunk(Words(400), size: 200, overlap: 50);

        for (var i = 1; i < chunks.Count; i++)
        {
            var previousTail = chunks[i - 1].Text.Split(' ').TakeLast(3);
            chunks[i].Text.Split(' ').Take(12).Should().Contain(previousTail.Last(), $"chunk {i} should repeat the end of chunk {i - 1}");
        }
    }

    [Fact]
    public void Chunk_CoversEveryWordOfTheSource()
    {
        var text = Words(300);

        var covered = Chunk(text, size: 120, overlap: 20).SelectMany(c => c.Text.Split(' ')).ToHashSet(StringComparer.Ordinal);

        covered.Should().BeEquivalentTo(text.Split(' '));
    }

    [Fact]
    public void Chunk_DoesNotCutWordsInTheMiddle()
    {
        var chunks = Chunk(Words(300), size: 120, overlap: 25);

        chunks.SelectMany(c => c.Text.Split(' ')).Should().OnlyContain(token => token.StartsWith("word", StringComparison.Ordinal));
    }

    [Fact]
    public void Chunk_PrefersParagraphBoundaries()
    {
        var text = $"{new string('a', 90)}\n\n{new string('b', 90)}\n\n{new string('c', 90)}";

        var chunks = Chunk(text, size: 100, overlap: 10);

        chunks[0].Text.Should().Be(new string('a', 90));
    }

    [Fact]
    public void Chunk_PrefersSentenceBoundaries()
    {
        var text = "The first sentence is a little longer than before. Second sentence follows right after it and keeps going for a while longer. Third.";

        var chunks = Chunk(text, size: 60, overlap: 10);

        chunks[0].Text.Should().Be("The first sentence is a little longer than before.");
    }

    [Fact]
    public void Chunk_KeepsPageNumbersAndNumbersChunksSequentially()
    {
        var pages = new[] { new ExtractedPage(2, Words(80)), new ExtractedPage(1, Words(80)), new ExtractedPage(5, "tail") };

        var chunks = TextChunker.Chunk(pages, 120, 20);

        chunks.Select(c => c.Index).Should().Equal(Enumerable.Range(0, chunks.Count));
        chunks.Select(c => c.Page).Should().BeInAscendingOrder();
        chunks.Select(c => c.Page).Distinct().Should().Equal(1, 2, 5);
    }

    [Fact]
    public void Chunk_SplitsTextWithoutAnyWhitespaceAndStillTerminates()
    {
        var chunks = Chunk(new string('x', 1000), size: 100, overlap: 20);

        chunks.Should().HaveCountGreaterThan(9);
        chunks.Should().OnlyContain(c => c.Text.Length <= 100);
        chunks.Sum(c => c.Text.Length).Should().BeGreaterThanOrEqualTo(1000);
    }

    [Fact]
    public void Chunk_IgnoresPagesWithoutText()
    {
        var chunks = TextChunker.Chunk([new ExtractedPage(1, "  \n\t "), new ExtractedPage(2, "content")], 100, 10);

        chunks.Should().ContainSingle().Which.Page.Should().Be(2);
    }

    [Fact]
    public void Normalize_CollapsesWhitespaceAndLineEndings()
    {
        var normalized = TextChunker.Normalize("Line one  \r\nLine\t two\r\n\r\n\r\n\r\nNext\0 paragraph here");

        normalized.Should().Be("Line one\nLine two\n\nNext paragraph here");
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(100, 100)]
    [InlineData(100, 150)]
    [InlineData(100, -1)]
    public void Chunk_RejectsInvalidSettings(int size, int overlap)
    {
        var act = () => TextChunker.Chunk([new ExtractedPage(1, "text")], size, overlap);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
