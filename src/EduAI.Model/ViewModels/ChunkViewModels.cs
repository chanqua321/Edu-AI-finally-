namespace EduAI.Model.ViewModels;

public class ChunkIndexViewModel
{
    public int? SubjectId { get; set; }
    public string? Keyword { get; set; }
    public IReadOnlyList<DTOs.SubjectDto> Subjects { get; set; } = Array.Empty<DTOs.SubjectDto>();
    public IReadOnlyList<DTOs.ChunkDto> Chunks { get; set; } = Array.Empty<DTOs.ChunkDto>();
}

public class ChunkDetailsViewModel
{
    public DTOs.ChunkDto? Chunk { get; set; }
    public DTOs.EmbeddingDto? Embedding { get; set; }
    public bool IsAdmin { get; set; }
}

public class ChunkFormViewModel
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int ChapterId { get; set; }
    public int DocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class ChatMessageIndexViewModel
{
    public int? SessionId { get; set; }
    public IReadOnlyList<DTOs.ChatMessageDto> Messages { get; set; } = Array.Empty<DTOs.ChatMessageDto>();
}

public class ChatMessageFormViewModel
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Citations { get; set; }
}

public class EmbeddingIndexViewModel
{
    public int? SubjectId { get; set; }
    public IReadOnlyList<DTOs.SubjectDto> Subjects { get; set; } = Array.Empty<DTOs.SubjectDto>();
    public IReadOnlyList<DTOs.EmbeddingDto> Embeddings { get; set; } = Array.Empty<DTOs.EmbeddingDto>();
}
