using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using EduAI.Model.Enums;
using UglyToad.PdfPig;

namespace EduAI.BusinessLogic.Helpers;

public static class DocumentTextExtractor
{
    private const int DefaultChunkSize = 800;
    private const int DefaultOverlap = 120;

    public static async Task<string> ExtractTextAsync(Stream stream, string fileName)
    {
        if (stream.CanSeek)
            stream.Position = 0;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".txt" => await ReadTextAsync(stream),
            ".pdf" => ExtractPdfText(stream),
            ".docx" => ExtractDocxText(stream),
            ".pptx" => ExtractPresentationText(stream),
            ".ppt" => throw new InvalidOperationException("Định dạng .ppt (cũ) chưa được hỗ trợ. Vui lòng lưu lại dưới dạng .pptx."),
            _ => throw new InvalidOperationException($"Định dạng file không được hỗ trợ: {extension}")
        };
    }

    private static async Task<string> ReadTextAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return Normalize(await reader.ReadToEndAsync());
    }

    private static string ExtractPdfText(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(string.Join(' ', page.GetWords().Select(w => w.Text)));
        }

        return Normalize(builder.ToString());
    }

    private static string ExtractDocxText(Stream stream)
    {
        using var wordDoc = WordprocessingDocument.Open(stream, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body == null)
            return string.Empty;

        var builder = new StringBuilder();
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var text = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
            if (!string.IsNullOrWhiteSpace(text))
                builder.AppendLine(text);
        }

        return Normalize(builder.ToString());
    }

    private static string ExtractPresentationText(Stream stream)
    {
        using var presentation = PresentationDocument.Open(stream, false);
        var builder = new StringBuilder();
        var slideParts = presentation.PresentationPart?.SlideParts;
        if (slideParts == null)
            return string.Empty;

        foreach (var slide in slideParts)
        {
            var texts = slide.Slide?.Descendants<DocumentFormat.OpenXml.Drawing.Text>()
                .Select(t => t.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t));
            if (texts != null)
                builder.AppendLine(string.Join(' ', texts));
        }

        return Normalize(builder.ToString());
    }

    /// <summary>
    /// Chunk theo chế độ Teacher (Paragraph/Word) hoặc fallback theo ký tự (Admin global).
    /// </summary>
    public static IReadOnlyList<string> ChunkText(
        string text,
        ChunkMode? mode,
        int chunkSize,
        int overlap,
        bool useCharacterFallback = false)
    {
        if (useCharacterFallback || mode == null)
            return ChunkByCharacters(text, chunkSize, overlap);

        return mode.Value switch
        {
            ChunkMode.Paragraph => ChunkByParagraphs(text, chunkSize, overlap),
            ChunkMode.Word => ChunkByWords(text, chunkSize, overlap),
            _ => ChunkByCharacters(text, chunkSize, overlap)
        };
    }

    /// <summary>Giữ API cũ: chia theo ký tự (Admin global default).</summary>
    public static IReadOnlyList<string> ChunkText(
        string text,
        int chunkSize = DefaultChunkSize,
        int overlap = DefaultOverlap) =>
        ChunkByCharacters(text, chunkSize, overlap);

    private static IReadOnlyList<string> ChunkByParagraphs(string text, int maxParagraphs, int overlapParagraphs)
    {
        text = Normalize(text);
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        maxParagraphs = Math.Max(1, maxParagraphs);
        overlapParagraphs = Math.Clamp(overlapParagraphs, 0, maxParagraphs - 1);

        var paragraphs = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (paragraphs.Length == 0)
            return Array.Empty<string>();

        var chunks = new List<string>();
        var step = Math.Max(1, maxParagraphs - overlapParagraphs);

        for (var i = 0; i < paragraphs.Length; i += step)
        {
            var take = Math.Min(maxParagraphs, paragraphs.Length - i);
            if (take <= 0)
                break;

            chunks.Add(string.Join("\n", paragraphs.Skip(i).Take(take)));
            if (i + take >= paragraphs.Length)
                break;
        }

        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    private static IReadOnlyList<string> ChunkByWords(string text, int maxWords, int overlapWords)
    {
        text = Normalize(text);
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        maxWords = Math.Max(1, maxWords);
        overlapWords = Math.Clamp(overlapWords, 0, maxWords - 1);

        var words = Regex.Split(text, @"\s+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToArray();
        if (words.Length == 0)
            return Array.Empty<string>();

        var chunks = new List<string>();
        var step = Math.Max(1, maxWords - overlapWords);

        for (var i = 0; i < words.Length; i += step)
        {
            var take = Math.Min(maxWords, words.Length - i);
            if (take <= 0)
                break;

            chunks.Add(string.Join(' ', words.Skip(i).Take(take)));
            if (i + take >= words.Length)
                break;
        }

        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    private static IReadOnlyList<string> ChunkByCharacters(
        string text,
        int chunkSize = DefaultChunkSize,
        int overlap = DefaultOverlap)
    {
        text = Normalize(text);
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();

        var paragraphs = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            if (current.Length + paragraph.Length + 1 <= chunkSize)
            {
                if (current.Length > 0)
                    current.Append(' ');
                current.Append(paragraph);
                continue;
            }

            if (current.Length > 0)
            {
                chunks.Add(current.ToString());
                var tail = GetOverlapTail(current.ToString(), overlap);
                current.Clear();
                current.Append(tail);
                if (current.Length > 0)
                    current.Append(' ');
            }

            if (paragraph.Length <= chunkSize)
            {
                current.Append(paragraph);
                continue;
            }

            for (var i = 0; i < paragraph.Length; i += Math.Max(1, chunkSize - overlap))
            {
                var length = Math.Min(chunkSize, paragraph.Length - i);
                chunks.Add(paragraph.Substring(i, length));
            }
            current.Clear();
        }

        if (current.Length > 0)
            chunks.Add(current.ToString());

        return chunks.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
    }

    private static string GetOverlapTail(string text, int overlap)
    {
        if (overlap <= 0 || text.Length <= overlap)
            return string.Empty;
        return text[^overlap..];
    }

    private static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }
}
