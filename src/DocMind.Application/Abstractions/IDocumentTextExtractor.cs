namespace DocMind.Application.Abstractions;

public sealed record ExtractedPage(int Number, string Text);

public sealed record ExtractedDocument(int PageCount, IReadOnlyList<ExtractedPage> Pages);

public sealed class UnsupportedDocumentException : Exception
{
    public UnsupportedDocumentException()
    {
    }

    public UnsupportedDocumentException(string message)
        : base(message)
    {
    }

    public UnsupportedDocumentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public interface IDocumentTextExtractor
{
    Task<ExtractedDocument> ExtractAsync(Stream content, string contentType, CancellationToken cancellationToken);
}
