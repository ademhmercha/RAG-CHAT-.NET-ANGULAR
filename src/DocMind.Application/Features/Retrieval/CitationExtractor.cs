using System.Text.RegularExpressions;
using DocMind.Application.Abstractions;
using DocMind.Domain.Conversations;

namespace DocMind.Application.Features.Retrieval;

public static partial class CitationExtractor
{
    private const int RegexTimeoutMilliseconds = 500;

    public static IReadOnlyList<Citation> Extract(string answer, IReadOnlyList<RetrievedChunk> sources, RagSettings settings)
    {
        if (sources.Count == 0)
        {
            return [];
        }

        var referenced = ReferencedNumbers(answer, sources.Count);

        var selected = referenced.Count > 0
            ? referenced.Select(number => sources[number - 1])
            : sources.Where(s => s.Score >= settings.FallbackCitationScore).Take(settings.MaxFallbackCitations);

        return selected
            .Select(source => new Citation(source.DocumentId, source.DocumentName, source.Page, Excerpt(source.Content, settings.ExcerptCharacters)))
            .DistinctBy(citation => (citation.DocumentId, citation.Page, citation.Excerpt))
            .ToList();
    }

    public static string Excerpt(string content, int maxCharacters)
    {
        var text = string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (text.Length <= maxCharacters)
        {
            return text;
        }

        var cut = text.LastIndexOf(' ', maxCharacters - 1);
        return string.Concat(text.AsSpan(0, cut > maxCharacters / 2 ? cut : maxCharacters - 1), "…");
    }

    private static List<int> ReferencedNumbers(string answer, int sourceCount)
    {
        var numbers = new List<int>();

        foreach (Match match in Marker().Matches(answer))
        {
            var number = int.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture);
            if (number >= 1 && number <= sourceCount && !numbers.Contains(number))
            {
                numbers.Add(number);
            }
        }

        return numbers;
    }

    [GeneratedRegex("\\[(?<number>\\d{1,2})\\]", RegexOptions.ExplicitCapture, RegexTimeoutMilliseconds)]
    private static partial Regex Marker();
}
