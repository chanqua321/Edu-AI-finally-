using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduAI.BusinessLogic.Services;

public class GeminiAiService : IGeminiAiService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiAiService> _logger;

    public GeminiAiService(
        HttpClient httpClient,
        IOptions<GeminiSettings> settings,
        ILogger<GeminiAiService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default)
    {
        var url = $"models/{_settings.EmbeddingModel}:embedContent";
        var payload = new
        {
            model = $"models/{_settings.EmbeddingModel}",
            content = new
            {
                parts = new[] { new { text } }
            }
        };

        using var request = CreateRequest(HttpMethod.Post, url, payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini embed failed ({StatusCode}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException("Gọi Gemini để tạo embedding thất bại.");
        }

        var parsed = JsonSerializer.Deserialize<EmbedContentResponse>(body);
        var values = parsed?.Embedding?.Values;
        if (values is not { Count: > 0 })
            throw new InvalidOperationException("Phản hồi embedding từ Gemini rỗng.");

        return values.ToArray();
    }

    public async Task<string> GenerateAnswerAsync(
        string question,
        string context,
        string subjectName,
        IReadOnlyList<ChatHistoryItemDto>? history = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"models/{_settings.ChatModel}:generateContent";
        var systemPrompt = string.IsNullOrWhiteSpace(_settings.SystemPrompt)
            ? "You are an educational assistant. Answer only from the provided materials."
            : _settings.SystemPrompt;

        var userPrompt =
            $"Subject: {subjectName}\n\n" +
            $"Authorized materials:\n{context}\n\n" +
            $"Student question: {question}";

        var contents = new List<object>();
        if (history != null)
        {
            foreach (var item in history)
            {
                var role = item.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
                contents.Add(new
                {
                    role,
                    parts = new[] { new { text = item.Content } }
                });
            }
        }

        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = userPrompt } }
        });

        var payload = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents
        };

        using var request = CreateRequest(HttpMethod.Post, url, payload);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini generate failed ({StatusCode}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException("Gọi Gemini để tạo câu trả lời thất bại.");
        }

        var parsed = JsonSerializer.Deserialize<GenerateContentResponse>(body);
        var answer = parsed?.Candidates?
            .FirstOrDefault()?
            .Content?
            .Parts?
            .Select(p => p.Text)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

        if (string.IsNullOrWhiteSpace(answer))
            throw new InvalidOperationException("Gemini trả về câu trả lời rỗng.");

        return answer.Trim();
    }
    // dùng để gọi API key và tạo request gửi lên server của google để lấy embedding và generate content
    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, object payload)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Add("x-goog-api-key", _settings.ApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
        return request;
    }

    private sealed class EmbedContentResponse
    {
        [JsonPropertyName("embedding")]
        public EmbeddingData? Embedding { get; set; }
    }

    private sealed class EmbeddingData
    {
        [JsonPropertyName("values")]
        public List<float>? Values { get; set; }
    }

    private sealed class GenerateContentResponse
    {
        [JsonPropertyName("candidates")]
        public List<CandidateData>? Candidates { get; set; }
    }

    private sealed class CandidateData
    {
        [JsonPropertyName("content")]
        public ContentData? Content { get; set; }
    }

    private sealed class ContentData
    {
        [JsonPropertyName("parts")]
        public List<PartData>? Parts { get; set; }
    }

    private sealed class PartData
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
