using EduAI.Model.Enums;

namespace EduAI.Model.Entities;

public class Document : BaseEntity
{
    public int SubjectId { get; set; }
    public int ChapterId { get; set; }
    public int LessonId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DocumentFileType FileType { get; set; }
    public DocumentCategory Category { get; set; } = DocumentCategory.Lecture;
    public string UploadedByUserId { get; set; } = string.Empty;
    public string? LastModifiedByUserId { get; set; }
    public long FileSizeBytes { get; set; }
    public DocumentIndexStatus IndexStatus { get; set; } = DocumentIndexStatus.Pending;
    public string? IndexError { get; set; }
    public DateTime? IndexedAt { get; set; }

    public Subject Subject { get; set; } = null!;
    public Chapter Chapter { get; set; } = null!;
    public Lesson Lesson { get; set; } = null!;
    public ApplicationUser UploadedBy { get; set; } = null!;
    public ApplicationUser? LastModifiedBy { get; set; }
    public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}
