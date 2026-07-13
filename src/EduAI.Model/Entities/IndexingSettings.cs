namespace EduAI.Model.Entities;

/// <summary>Singleton row — cấu hình chia đoạn khi index tài liệu.</summary>
public class IndexingSettings
{
    public int Id { get; set; }

    public int ChunkSize { get; set; } = 800;

    public int ChunkOverlap { get; set; } = 120;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedByUserId { get; set; }

    public ApplicationUser? UpdatedBy { get; set; }
}
