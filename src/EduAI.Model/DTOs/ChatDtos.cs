namespace EduAI.Model.DTOs;

public class ChatHistoryItemDto
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public class ChatSessionDto
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string? StudentName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Citations { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendChatMessageDto
{
    public int SessionId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? ProviderId { get; set; }
    /// <summary>Optional: chỉ retrieve chunk từ tài liệu này.</summary>
    public int? DocumentId { get; set; }
    /// <summary>Override AiUsageLog.Operation (e.g. Warmup). Default GenerateAnswer.</summary>
    public string? UsageOperation { get; set; }
}

public class ChatUsedChunkDto
{
    public int ChunkId { get; set; }
    public int DocumentId { get; set; }
    public string DocumentFileName { get; set; } = string.Empty;
    public string? ChapterName { get; set; }
    public int ChunkIndex { get; set; }
    public float RelevanceScore { get; set; }
    public string Preview { get; set; } = string.Empty;
}

public class ChatResponseDto
{
    public bool Success { get; set; }
    public string Answer { get; set; } = string.Empty;
    public string? Citations { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<ChatUsedChunkDto> UsedChunks { get; set; } = Array.Empty<ChatUsedChunkDto>();
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string? ProviderId { get; set; }
    public string? ProviderName { get; set; }
    public ProviderQuotaCheckResult? Quota { get; set; }
    public string? FallbackProviderId { get; set; }
    public string? FallbackProviderName { get; set; }
    public ProviderQuotaCheckResult? FallbackQuota { get; set; }
}

public class CreateChatSessionDto
{
    public string StudentId { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
}

public class CreateChatSessionResultDto
{
    public bool Success { get; set; }
    public ChatSessionDto? Session { get; set; }
    public string? ErrorMessage { get; set; }
}

public class UpdateChatSessionDto
{
    public int SessionId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}

public class ChatSessionOperationResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class CreateChatMessageDto
{
    public int SessionId { get; set; }
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public string? Citations { get; set; }
}

public class UpdateChatMessageDto
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class ChatMessageOperationResultDto
{
    public bool Success { get; set; }
    public ChatMessageDto? Message { get; set; }
    public string? ErrorMessage { get; set; }
}
