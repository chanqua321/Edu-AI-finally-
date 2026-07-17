using EduAI.Model.Enums;

namespace EduAI.Model.Settings;

/// <summary>
/// Fixed AI/LLM runtime parameters — not editable via Admin UI.
/// Overridable via appsettings section "AiRuntime" or environment variables.
/// </summary>
public class AiRuntimeSettings
{
    public const string SectionName = "AiRuntime";

    /// <summary>Always Character for stable indexing.</summary>
    public ChunkMode ChunkMode { get; set; } = ChunkMode.Character;

    public double Temperature { get; set; } = 0.3;

    public int MaxChatHistory { get; set; } = 10;

    public int MaxOutputTokens { get; set; } = 4096;

    public string EmbeddingModel { get; set; } = "gemini-embedding-001";

    public string GenerationModel { get; set; } = "gemini-3-flash-preview";

    /// <summary>Tỷ giá quy đổi chi phí AI (USD) sang VND khi cân đối với doanh thu mua gói.</summary>
    public decimal UsdToVndRate { get; set; } = 25_000m;
}
