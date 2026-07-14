using System.ComponentModel.DataAnnotations;

namespace EduAI.Model.Settings;

public class GeminiSettings
{
    public const string SectionName = "Gemini";

    [Required(ErrorMessage = "Gemini:ApiKey is required in appsettings.")]
    public string ApiKey { get; set; } = string.Empty;

    [Required(ErrorMessage = "Gemini:BaseUrl is required in appsettings.")]
    public string BaseUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "Gemini:ChatModel is required in appsettings.")]
    public string ChatModel { get; set; } = string.Empty;

    [Required(ErrorMessage = "Gemini:EmbeddingModel is required in appsettings.")]
    public string EmbeddingModel { get; set; } = string.Empty;

    public string SystemPrompt { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 120;
}
