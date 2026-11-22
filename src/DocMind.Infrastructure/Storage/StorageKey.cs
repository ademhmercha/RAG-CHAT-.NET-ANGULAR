namespace DocMind.Infrastructure.Storage;

internal static class StorageKey
{
    public static string[] Segments(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.StartsWith('/') || key.Contains('\\', StringComparison.Ordinal) || Path.IsPathRooted(key))
        {
            throw new ArgumentException("The storage key is invalid.", nameof(key));
        }

        var segments = key.Split('/');
        if (segments.Any(s => s.Length == 0 || s is "." or ".." || s.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
        {
            throw new ArgumentException("The storage key is invalid.", nameof(key));
        }

        return segments;
    }
}
