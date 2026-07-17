namespace EduAI.Model.Settings;

public class AiProvidersSettings
{
    public const string SectionName = "AIProviders";

    /// <summary>Default generation provider when SystemSettings has no override (Gemini | Ollama).</summary>
    public string Default { get; set; } = "Gemini";

    public OllamaProviderSettings Ollama { get; set; } = new();
}

public class OllamaProviderSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "llama3.2";
    public int TimeoutSeconds { get; set; } = 300;
}
