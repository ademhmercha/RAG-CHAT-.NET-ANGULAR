namespace DocMind.Application.Features.Ingestion;

public sealed class TransientProcessingException : Exception
{
    public TransientProcessingException()
    {
    }

    public TransientProcessingException(string message)
        : base(message)
    {
    }

    public TransientProcessingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class EmbeddingMismatchException : InvalidOperationException
{
    public EmbeddingMismatchException()
    {
    }

    public EmbeddingMismatchException(string message)
        : base(message)
    {
    }

    public EmbeddingMismatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
