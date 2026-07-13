using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
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

    public static IReadOnlyList<string> ChunkText(
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

            for (var i = 0; i < paragraph.Length; i += chunkSize - overlap)
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
