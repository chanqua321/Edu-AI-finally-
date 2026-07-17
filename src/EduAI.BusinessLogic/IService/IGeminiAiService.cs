using EduAI.Model.DTOs;



namespace EduAI.BusinessLogic.IService;



public interface IGeminiAiService

{

    Task<float[]> EmbedTextAsync(

        string text,

        GeminiGenerationOptions? options = null,

        CancellationToken cancellationToken = default);



    Task<GenerateAnswerResultDto> GenerateAnswerAsync(

        string question,

        string context,

        string subjectName,

        IReadOnlyList<ChatHistoryItemDto>? history = null,

        GeminiGenerationOptions? options = null,

        CancellationToken cancellationToken = default);

}

