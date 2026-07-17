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

    // appsettings: "Gemini" (inject qua IOptions) → ApiKey dùng xác thực API; BaseUrl/Model dùng embed + chat.
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



    public async Task<float[]> EmbedTextAsync(

        string text,

        GeminiGenerationOptions? options = null,

        CancellationToken cancellationToken = default)

    {

        var embeddingModel = ResolveEmbeddingModel(options);

        var url = $"models/{embeddingModel}:embedContent";

        var payload = new

        {

            model = $"models/{embeddingModel}",

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



    public async Task<GenerateAnswerResultDto> GenerateAnswerAsync(

        string question,

        string context,

        string subjectName,

        IReadOnlyList<ChatHistoryItemDto>? history = null,

        GeminiGenerationOptions? options = null,

        CancellationToken cancellationToken = default)

    {

        var generationModel = ResolveGenerationModel(options);

        var url = $"models/{generationModel}:generateContent";

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



        var generationConfig = new Dictionary<string, object>();

        if (options?.Temperature is double temperature)

            generationConfig["temperature"] = temperature;

        if (options?.MaxOutputTokens is int maxOutputTokens)

            generationConfig["maxOutputTokens"] = maxOutputTokens;



        var payload = new Dictionary<string, object>

        {

            ["systemInstruction"] = new { parts = new[] { new { text = systemPrompt } } },

            ["contents"] = contents

        };

        if (generationConfig.Count > 0)

            payload["generationConfig"] = generationConfig;



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



        AiTokenUsageDto? usage = null;

        if (parsed?.UsageMetadata != null)

        {

            usage = new AiTokenUsageDto

            {

                PromptTokens = parsed.UsageMetadata.PromptTokenCount,

                CompletionTokens = parsed.UsageMetadata.CandidatesTokenCount,

                TotalTokens = parsed.UsageMetadata.TotalTokenCount > 0

                    ? parsed.UsageMetadata.TotalTokenCount

                    : parsed.UsageMetadata.PromptTokenCount + parsed.UsageMetadata.CandidatesTokenCount

            };

        }



        return new GenerateAnswerResultDto

        {

            Answer = answer.Trim(),

            Usage = usage

        };

    }



    private string ResolveEmbeddingModel(GeminiGenerationOptions? options) =>

        string.IsNullOrWhiteSpace(options?.EmbeddingModel) ? _settings.EmbeddingModel : options.EmbeddingModel.Trim();



    private string ResolveGenerationModel(GeminiGenerationOptions? options) =>

        string.IsNullOrWhiteSpace(options?.GenerationModel) ? _settings.ChatModel : options.GenerationModel.Trim();



    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl, object payload)

    {

        var request = new HttpRequestMessage(method, relativeUrl);

        // appsettings: "Gemini:ApiKey" → gắn header x-goog-api-key khi gọi Gemini REST API (embed + generate).
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



        [JsonPropertyName("usageMetadata")]

        public UsageMetadataData? UsageMetadata { get; set; }

    }



    private sealed class UsageMetadataData

    {

        [JsonPropertyName("promptTokenCount")]

        public int PromptTokenCount { get; set; }



        [JsonPropertyName("candidatesTokenCount")]

        public int CandidatesTokenCount { get; set; }



        [JsonPropertyName("totalTokenCount")]

        public int TotalTokenCount { get; set; }

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

