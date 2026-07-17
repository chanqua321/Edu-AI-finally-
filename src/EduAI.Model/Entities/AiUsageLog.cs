namespace EduAI.Model.Entities;

/// <summary>Ghi nhận token sử dụng mỗi lần gọi AI (chat).</summary>
public class AiUsageLog : BaseEntity
{
    public int SubjectId { get; set; }
    public int? ChatSessionId { get; set; }
    public int? ChatMessageId { get; set; }
    public string? UserId { get; set; }
    public string Operation { get; set; } = "GenerateAnswer";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public string? Model { get; set; }

    /// <summary>Generation provider id (Gemini | Ollama).</summary>
    public string? Provider { get; set; }

    // RAG Benchmarks/Metrics
    public int EmbeddingTimeMs { get; set; }
    public int RetrievalTimeMs { get; set; }
    public int GenerationTimeMs { get; set; }
    public int TotalTimeMs { get; set; }
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }

    public Subject Subject { get; set; } = null!;
    public ChatSession? ChatSession { get; set; }
    public ChatMessage? ChatMessage { get; set; }
    public ApplicationUser? User { get; set; }
}
