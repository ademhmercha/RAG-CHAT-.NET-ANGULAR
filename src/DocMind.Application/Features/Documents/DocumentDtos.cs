using DocMind.Domain.Documents;

namespace DocMind.Application.Features.Documents;

public sealed record DocumentDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DocumentStatus Status,
    string? ErrorMessage,
    int? PageCount,
    int ChunkCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

internal static class DocumentMappings
{
    public static DocumentDto ToDto(this Document document) => new(
        document.Id,
        document.FileName,
        document.ContentType,
        document.SizeBytes,
        document.Status,
        document.ErrorMessage,
        document.PageCount,
        document.ChunkCount,
        document.CreatedAt,
        document.UpdatedAt);
}
