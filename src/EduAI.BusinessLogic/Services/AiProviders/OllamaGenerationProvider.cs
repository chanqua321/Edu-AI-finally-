using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduAI.BusinessLogic.Services.AiProviders;

/// <summary>Ollama local generation via REST /api/chat — same RAG prompt shape as Gemini.</summary>
public sealed class OllamaGenerationProvider : IAiGenerationProvider
{
    private readonly HttpClient _httpClient;
    // appsettings: "AIProviders:Ollama" → BaseUrl/Model dùng gọi Ollama local sinh câu trả lời chat.
    private readonly OllamaProviderSettings _settings;
    private readonly GeminiSettings _geminiSettings;
    // appsettings: "AiRuntime" → Temperature, MaxOutputTokens dùng trong prompt Ollama.
    private readonly AiRuntimeSettings _aiRuntime;
    private readonly ILogger<OllamaGenerationProvider> _logger;

    public OllamaGenerationProvider(
        HttpClient httpClient,
        IOptions<AiProvidersSettings> aiProviders,
        IOptions<GeminiSettings> geminiSettings,
        IOptions<AiRuntimeSettings> aiRuntime,
        ILogger<OllamaGenerationProvider> logger)
    {
        _httpClient = httpClient;
        _settings = aiProviders.Value.Ollama;
        _geminiSettings = geminiSettings.Value;
        _aiRuntime = aiRuntime.Value;
        _logger = logger;
    }

    public string ProviderId => AiProviderIds.Ollama;
    public string DisplayName => "Ollama (Local)";
    public string ModelName =>
        string.IsNullOrWhiteSpace(_settings.Model) ? "llama3.2" : _settings.Model.Trim();

    public async Task<GenerateAnswerResultDto> GenerateAnswerAsync(
        string question,
        string context,
        string subjectName,
        IReadOnlyList<ChatHistoryItemDto>? history = null,
        GeminiGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var model = string.IsNullOrWhiteSpace(options?.GenerationModel)
            ? ModelName
            : options.GenerationModel.Trim();

        var systemPrompt = string.IsNullOrWhiteSpace(_geminiSettings.SystemPrompt)
            ? "You are an educational assistant. Answer only from the provided materials."
            : _geminiSettings.SystemPrompt;

        // Identical user prompt format as GeminiAiService
        var userPrompt =
            $"Subject: {subjectName}\n\n" +
            $"Authorized materials:\n{context}\n\n" +
            $"Student question: {question}";

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        if (history != null)
        {
            foreach (var item in history)
            {
                var role = item.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                    ? "assistant"
                    : "user";
                messages.Add(new { role, content = item.Content });
            }
        }

        messages.Add(new { role = "user", content = userPrompt });

        var temperature = options?.Temperature ?? _aiRuntime.Temperature;
        var maxTokens = options?.MaxOutputTokens ?? _aiRuntime.MaxOutputTokens;

        var payload = new
        {
            model,
            messages,
            stream = false,
            options = new
            {
                temperature,
                num_predict = maxTokens
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/chat")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Ollama generate failed ({StatusCode}): {Body}", response.StatusCode, body);
            throw new InvalidOperationException(
                $"Gọi Ollama thất bại ({(int)response.StatusCode}). Kiểm tra Ollama đang chạy tại {_settings.BaseUrl}.");
        }

        var parsed = JsonSerializer.Deserialize<OllamaChatResponse>(body);
        var answer = parsed?.Message?.Content;

        if (string.IsNullOrWhiteSpace(answer))
            throw new InvalidOperationException("Ollama trả về câu trả lời rỗng.");

        AiTokenUsageDto? usage = null;
        if (parsed != null && (parsed.PromptEvalCount > 0 || parsed.EvalCount > 0))
        {
            usage = new AiTokenUsageDto
            {
                PromptTokens = parsed.PromptEvalCount,
                CompletionTokens = parsed.EvalCount,
                TotalTokens = parsed.PromptEvalCount + parsed.EvalCount
            };
        }

        return new GenerateAnswerResultDto
        {
            Answer = answer.Trim(),
            Usage = usage
        };
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaMessage? Message { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int PromptEvalCount { get; set; }

        [JsonPropertyName("eval_count")]
        public int EvalCount { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
