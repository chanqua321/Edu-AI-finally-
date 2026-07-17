namespace EduAI.Model.DTOs;

public class GeminiGenerationOptions
{
    public string? GenerationModel { get; set; }
    public string? EmbeddingModel { get; set; }
    public double? Temperature { get; set; }
    public int? MaxOutputTokens { get; set; }
}
