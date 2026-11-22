using System.Text;
using System.Text.RegularExpressions;
using DocMind.Application.Abstractions;

namespace DocMind.Application.Features.Ingestion;

public sealed record TextChunk(int Index, int Page, string Text);

public static partial class TextChunker
{
    private const int RegexTimeoutMilliseconds = 1000;

    private static readonly char[] Whitespace = [' ', '\n', '\t'];
    private static readonly string[] SentenceEnds = [". ", "? ", "! ", ".\n", "?\n", "!\n"];

    public static IReadOnlyList<TextChunk> Chunk(IEnumerable<ExtractedPage> pages, int chunkSize, int overlap)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 2);
        ArgumentOutOfRangeException.ThrowIfNegative(overlap);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(overlap, chunkSize);

        var chunks = new List<TextChunk>();

        foreach (var page in pages.OrderBy(p => p.Number))
        {
            foreach (var piece in Split(Normalize(page.Text), chunkSize, overlap))
            {
                chunks.Add(new TextChunk(chunks.Count, page.Number, piece));
            }
        }

        return chunks;
    }

    public static string Normalize(string text)
    {
        var unified = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var builder = new StringBuilder(unified.Length);

        foreach (var character in unified)
        {
            if (character == '\0')
            {
                continue;
            }

            builder.Append(character is '\f' or '\v' or ' ' ? ' ' : character);
        }

        var normalized = HorizontalSpace().Replace(builder.ToString(), " ");
        normalized = SpaceAroundNewline().Replace(normalized, "\n");
        normalized = BlankLines().Replace(normalized, "\n\n");

        return normalized.Trim();
    }

    private static IEnumerable<string> Split(string text, int size, int overlap)
    {
        var start = 0;

        while (start < text.Length)
        {
            var end = Math.Min(start + size, text.Length);
            if (end < text.Length)
            {
                end = FindBreak(text, start, end, size);
            }

            var piece = text[start..end].Trim();
            if (piece.Length > 0)
            {
                yield return piece;
            }

            if (end >= text.Length)
            {
                yield break;
            }

            start = NextStart(text, start, end, overlap);
        }
    }

    private static int FindBreak(string text, int start, int end, int size)
    {
        var minimum = start + (size / 2);

        var paragraph = text.LastIndexOf("\n\n", end - 1, end - minimum, StringComparison.Ordinal);
        if (paragraph >= minimum)
        {
            return paragraph + 2;
        }

        var sentence = SentenceEnds.Max(marker => text.LastIndexOf(marker, end - 1, end - minimum, StringComparison.Ordinal));
        if (sentence >= minimum)
        {
            return sentence + 2;
        }

        var space = text.LastIndexOfAny(Whitespace, end - 1, end - minimum);
        return space >= minimum ? space + 1 : end;
    }

    private static int NextStart(string text, int start, int end, int overlap)
    {
        var next = end - overlap;
        if (next <= start)
        {
            return end;
        }

        if (next > 0 && !char.IsWhiteSpace(text[next - 1]) && !char.IsWhiteSpace(text[next]))
        {
            var boundary = text.IndexOfAny(Whitespace, next, end - next);
            if (boundary >= 0)
            {
                next = boundary + 1;
            }
        }

        return next >= end ? end : next;
    }

    [GeneratedRegex("[ \\t]+", RegexOptions.None, RegexTimeoutMilliseconds)]
    private static partial Regex HorizontalSpace();

    [GeneratedRegex(" ?\\n ?", RegexOptions.None, RegexTimeoutMilliseconds)]
    private static partial Regex SpaceAroundNewline();

    [GeneratedRegex("\\n{3,}", RegexOptions.None, RegexTimeoutMilliseconds)]
    private static partial Regex BlankLines();
}
