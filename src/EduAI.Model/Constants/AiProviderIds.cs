namespace EduAI.Model.Constants;

public static class AiProviderIds
{
    public const string Gemini = "Gemini";
    public const string Ollama = "Ollama";

    public static readonly IReadOnlyList<(string Id, string DisplayName)> All =
    [
        (Gemini, "Gemini 3 Flash"),
        (Ollama, "Ollama (Local)")
    ];

    public static bool IsValid(string? id) =>
        string.Equals(id, Gemini, StringComparison.OrdinalIgnoreCase)
        || string.Equals(id, Ollama, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? id) =>
        IsValid(id) ? (string.Equals(id, Ollama, StringComparison.OrdinalIgnoreCase) ? Ollama : Gemini) : Gemini;
}
