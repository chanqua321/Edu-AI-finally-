namespace EduAI.Model.DTOs;

public class CreateChunkDto
{
    public int SubjectId { get; set; }
    public int ChapterId { get; set; }
    public int DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? ChunkIndex { get; set; }
}

public class UpdateChunkDto
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class ChunkOperationResultDto
{
    public bool Success { get; set; }
    public ChunkDto? Chunk { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ChunkDto
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int ChapterId { get; set; }
    public string ChapterName { get; set; } = string.Empty;
    public int DocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool HasEmbedding { get; set; }
}
