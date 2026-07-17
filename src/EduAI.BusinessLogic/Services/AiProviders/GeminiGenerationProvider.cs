using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Settings;
using Microsoft.Extensions.Options;

namespace EduAI.BusinessLogic.Services.AiProviders;

/// <summary>Gemini generation strategy — delegates to existing IGeminiAiService.</summary>
public sealed class GeminiGenerationProvider : IAiGenerationProvider
{
    private readonly IGeminiAiService _geminiAiService;
    // appsettings: "Gemini" + "AiRuntime:GenerationModel" → model Gemini dùng khi user chọn provider Gemini.
    private readonly GeminiSettings _geminiSettings;
    private readonly AiRuntimeSettings _aiRuntime;

    public GeminiGenerationProvider(
        IGeminiAiService geminiAiService,
        IOptions<GeminiSettings> geminiSettings,
        IOptions<AiRuntimeSettings> aiRuntime)
    {
        _geminiAiService = geminiAiService;
        _geminiSettings = geminiSettings.Value;
        _aiRuntime = aiRuntime.Value;
    }

    public string ProviderId => AiProviderIds.Gemini;
    public string DisplayName => "Gemini 3 Flash";
    public string ModelName =>
        string.IsNullOrWhiteSpace(_aiRuntime.GenerationModel)
            ? _geminiSettings.ChatModel
            : _aiRuntime.GenerationModel;

    public Task<GenerateAnswerResultDto> GenerateAnswerAsync(
        string question,
        string context,
        string subjectName,
        IReadOnlyList<ChatHistoryItemDto>? history = null,
        GeminiGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var merged = options ?? new GeminiGenerationOptions();
        merged.GenerationModel ??= ModelName;
        merged.Temperature ??= _aiRuntime.Temperature;
        merged.MaxOutputTokens ??= _aiRuntime.MaxOutputTokens;

        return _geminiAiService.GenerateAnswerAsync(
            question, context, subjectName, history, merged, cancellationToken);
    }
}
