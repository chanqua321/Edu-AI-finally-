namespace EduAI.Model.Entities;

public class DocumentChunk : BaseEntity
{
    public int SubjectId { get; set; }
    public int ChapterId { get; set; }
    public int DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }

    public Subject Subject { get; set; } = null!;
    public Chapter Chapter { get; set; } = null!;
    public Document Document { get; set; } = null!;
    public DocumentEmbedding? Embedding { get; set; }
}
