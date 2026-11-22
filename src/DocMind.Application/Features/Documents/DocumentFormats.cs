namespace DocMind.Application.Features.Documents;

public static class DocumentFormats
{
    public const string Pdf = "application/pdf";
    public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];

    public static int SignatureLength => PdfSignature.Length;

    public static string? ContentTypeForExtension(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf" => Pdf,
            ".docx" => Docx,
            _ => null,
        };

    public static bool MatchesSignature(string contentType, ReadOnlySpan<byte> header) => contentType switch
    {
        Pdf => header.StartsWith(PdfSignature),
        Docx => header.StartsWith(ZipSignature),
        _ => false,
    };

    public static string SanitizeFileName(string? fileName, int maxLength)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        var cleaned = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();

        if (cleaned.Length == 0)
        {
            return "document";
        }

        if (cleaned.Length <= maxLength)
        {
            return cleaned;
        }

        var extension = Path.GetExtension(cleaned);
        return string.Concat(cleaned.AsSpan(0, maxLength - extension.Length), extension);
    }
}
