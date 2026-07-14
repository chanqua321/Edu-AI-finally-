namespace EduAI.Model.DTOs;

public class EmbeddingDto
{
    public int Id { get; set; }
    public int ChunkId { get; set; }
    public int SubjectId { get; set; }
    public int DocumentId { get; set; }
    public int ChapterId { get; set; }
    public int DimensionCount { get; set; }
    public string VectorPreview { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class EmbeddingOperationResultDto
{
    public bool Success { get; set; }
    public EmbeddingDto? Embedding { get; set; }
    public string? ErrorMessage { get; set; }
}
