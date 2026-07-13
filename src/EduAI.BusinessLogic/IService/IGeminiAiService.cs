using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface IGeminiAiService
{
    Task<float[]> EmbedTextAsync(string text, CancellationToken cancellationToken = default);

    Task<string> GenerateAnswerAsync(
        string question,
        string context,
        string subjectName,
        IReadOnlyList<ChatHistoryItemDto>? history = null,
        CancellationToken cancellationToken = default);
}
