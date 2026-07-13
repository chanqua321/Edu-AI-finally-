namespace EduAI.Model.Entities;

public class DocumentEmbedding : BaseEntity
{
    public int ChunkId { get; set; }
    public int SubjectId { get; set; }
    public int ChapterId { get; set; }
    public int DocumentId { get; set; }
    public string EmbeddingVector { get; set; } = string.Empty;

    public DocumentChunk Chunk { get; set; } = null!;
}
