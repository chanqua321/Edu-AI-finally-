using EduAI.Model.Enums;

namespace EduAI.Model.DTOs;

public class DocumentDto
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int ChapterId { get; set; }
    public string ChapterName { get; set; } = string.Empty;
    public int LessonId { get; set; }
    public string LessonName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DocumentCategory Category { get; set; } = DocumentCategory.Lecture;
    public string CategoryLabel { get; set; } = string.Empty;
    public string UploadedByName { get; set; } = string.Empty;
    public string? LastModifiedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long FileSizeBytes { get; set; }
    public int ChunkCount { get; set; }
    public string IndexStatus { get; set; } = "Pending";
    public string? IndexError { get; set; }
}

public class DocumentDetailsDto : DocumentDto
{
    public DateTime? ProcessedAt { get; set; }
    public IReadOnlyList<ChunkDto> Chunks { get; set; } = Array.Empty<ChunkDto>();
}

public class UploadDocumentDto
{
    public int SubjectId { get; set; }
    public int ChapterId { get; set; }
    public int LessonId { get; set; }
    public string UploadedByUserId { get; set; } = string.Empty;
    public string UploaderRole { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DocumentCategory Category { get; set; } = DocumentCategory.Lecture;
    public Stream FileStream { get; set; } = Stream.Null;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
}

public class UploadDocumentResultDto
{
    public bool Success { get; set; }
    public int? DocumentId { get; set; }
    public string? ErrorMessage { get; set; }
    public int ChunksCreated { get; set; }
}

public class UpdateDocumentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int ChapterId { get; set; }
    public int LessonId { get; set; }
    public DocumentCategory Category { get; set; } = DocumentCategory.Lecture;

    // Optional file replacement. When provided, the stored file is replaced and the document is re-indexed.
    public Stream? NewFileStream { get; set; }
    public string? NewFileOriginalName { get; set; }
    public long NewFileSizeBytes { get; set; }
}

public class DocumentOperationResultDto
{
    public bool Success { get; set; }
    public DocumentDto? Document { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DocumentDownloadResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
}
