using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

/// <summary>
/// Strategy interface for LLM answer generation.
/// Embedding stays on Gemini; only generation is pluggable.
/// </summary>
public interface IAiGenerationProvider
{
    /// <summary>Stable id used in config and SystemSettings (Gemini | Ollama).</summary>
    string ProviderId { get; }

    string DisplayName { get; }

    /// <summary>Configured model name for this provider.</summary>
    string ModelName { get; }

    Task<GenerateAnswerResultDto> GenerateAnswerAsync(
        string question,
        string context,
        string subjectName,
        IReadOnlyList<ChatHistoryItemDto>? history = null,
        GeminiGenerationOptions? options = null,
        CancellationToken cancellationToken = default);
}

public interface IAiGenerationProviderResolver
{
    IAiGenerationProvider Resolve(string? providerId = null);

    IReadOnlyList<IAiGenerationProvider> GetAll();
}
