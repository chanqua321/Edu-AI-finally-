namespace EduAI.BusinessLogic.Helpers;

public static class UploadFileValidator
{
    public static IReadOnlySet<string> ParseExtensions(string allowedExtensionsCsv)
    {
        return allowedExtensionsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsExtensionAllowed(string extension, IReadOnlySet<string> allowedExtensions) =>
        allowedExtensions.Contains(extension);

    public static bool MatchesDeclaredContentType(string extension, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return true;

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase),
            ".docx" => contentType.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase)
                       || contentType.Contains("msword", StringComparison.OrdinalIgnoreCase),
            ".pptx" => contentType.Contains("presentationml", StringComparison.OrdinalIgnoreCase)
                       || contentType.Contains("powerpoint", StringComparison.OrdinalIgnoreCase),
            ".txt" => contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    public static bool HasValidMagicNumber(Stream stream, string extension)
    {
        if (!stream.CanSeek)
            return true;

        var position = stream.Position;
        try
        {
            Span<byte> header = stackalloc byte[8];
            var read = stream.Read(header);
            if (read < 4)
                return extension.Equals(".txt", StringComparison.OrdinalIgnoreCase);

            return extension.ToLowerInvariant() switch
            {
                ".pdf" => header[0] == 0x25 && header[1] == 0x50 && header[2] == 0x44 && header[3] == 0x46,
                ".docx" or ".pptx" => header[0] == 0x50 && header[1] == 0x4B,
                ".txt" => true,
                _ => false
            };
        }
        finally
        {
            stream.Position = position;
        }
    }
}
