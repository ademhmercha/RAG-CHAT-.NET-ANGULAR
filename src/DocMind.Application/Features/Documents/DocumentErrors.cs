using DocMind.Domain.Common;

namespace DocMind.Application.Features.Documents;

public static class DocumentUploadErrors
{
    public static readonly Error EmptyFile =
        Error.Validation("documents.empty_file", "The uploaded file is empty.");

    public static readonly Error UnsupportedType =
        Error.UnsupportedMediaType("documents.unsupported_type", "Only PDF and DOCX documents are supported.");

    public static readonly Error ContentMismatch =
        Error.UnsupportedMediaType("documents.content_mismatch", "The file content does not match its extension.");

    public static Error TooLarge(long maxBytes) =>
        Error.PayloadTooLarge("documents.too_large", $"The file exceeds the maximum size of {maxBytes / (1024 * 1024)} MB.");

    public static Error QuotaExceeded(int maxDocuments) =>
        Error.Conflict("documents.quota_exceeded", $"You can store at most {maxDocuments} documents. Delete some before uploading more.");
}
