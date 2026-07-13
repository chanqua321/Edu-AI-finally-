using EduAI.BusinessLogic.Helpers;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.Enums;
using EduAI.Model.IRepository;
using EduAI.BusinessLogic.IService;
using EduAI.Model.Settings;
using Microsoft.Extensions.Options;

namespace EduAI.BusinessLogic.Services;

public class DocumentService : IDocumentService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".pptx", ".txt"
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubjectService _subjectService;
    private readonly IAuditLogService _auditLogService;
    private readonly IGeminiAiService _geminiAiService;
    private readonly INotificationService _notificationService;
    private readonly ISubjectNotificationService _subjectNotificationService;
    private readonly IDocumentIndexingQueue _indexingQueue;
    private readonly AppSettings _appSettings;

    public DocumentService(
        IUnitOfWork unitOfWork,
        ISubjectService subjectService,
        IAuditLogService auditLogService,
        IGeminiAiService geminiAiService,
        INotificationService notificationService,
        ISubjectNotificationService subjectNotificationService,
        IDocumentIndexingQueue indexingQueue,
        IOptions<AppSettings> appSettings)
    {
        _unitOfWork = unitOfWork;
        _subjectService = subjectService;
        _auditLogService = auditLogService;
        _geminiAiService = geminiAiService;
        _notificationService = notificationService;
        _subjectNotificationService = subjectNotificationService;
        _indexingQueue = indexingQueue;
        _appSettings = appSettings.Value;
    }

    public async Task<IReadOnlyList<DocumentDto>> GetBySubjectAsync(int subjectId, string userId, string role)
    {
        if (role == Roles.Teacher && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, subjectId))
            return Array.Empty<DocumentDto>();

        if (role == Roles.Student)
        {
            if (!await _subjectService.HasDocumentsAsync(subjectId))
                return Array.Empty<DocumentDto>();

            var studentDocs = await _unitOfWork.Documents.GetBySubjectIdAsync(subjectId);
            return studentDocs.Select(MapToDto).ToList();
        }

        var documents = await _unitOfWork.Documents.GetBySubjectIdAsync(subjectId);
        var result = new List<DocumentDto>();
        foreach (var document in documents)
        {
            var dto = MapToDto(document);
            await EnrichWithChunkStatsAsync(dto, document.Id, role);
            result.Add(dto);
        }

        return result;
    }

    public async Task<DocumentDto?> GetByIdAsync(int id, string userId, string role)
    {
        var document = await _unitOfWork.Documents.GetWithDetailsAsync(id);
        if (document == null) return null;

        if (role == Roles.Teacher && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, document.SubjectId))
            return null;

        if (role == Roles.Student)
            return null;

        var dto = MapToDto(document);
        await EnrichWithChunkStatsAsync(dto, document.Id, role);
        return dto;
    }

    public async Task<DocumentDetailsDto?> GetDetailsByIdAsync(int id, string userId, string role)
    {
        var document = await _unitOfWork.Documents.GetWithDetailsAsync(id);
        if (document == null)
            return null;

        if (role == Roles.Teacher && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, document.SubjectId))
            return null;

        if (role == Roles.Student)
            return null;

        var chunks = await _unitOfWork.Chunks.GetByDocumentIdAsync(id);
        var embeddingChunkIds = role == Roles.Admin
            ? (await _unitOfWork.Embeddings.GetBySubjectIdAsync(document.SubjectId))
                .Where(e => e.DocumentId == id)
                .Select(e => e.ChunkId)
                .ToHashSet()
            : [];

        var chunkDtos = chunks.Select(c => new ChunkDto
        {
            Id = c.Id,
            SubjectId = c.SubjectId,
            SubjectName = document.Subject?.Name ?? string.Empty,
            ChapterId = c.ChapterId,
            ChapterName = document.Chapter?.Name ?? string.Empty,
            DocumentId = c.DocumentId,
            DocumentName = document.FileName,
            ChunkIndex = c.ChunkIndex,
            Content = c.Content,
            HasEmbedding = role == Roles.Admin && embeddingChunkIds.Contains(c.Id)
        }).ToList();

        var details = new DocumentDetailsDto
        {
            Id = document.Id,
            SubjectId = document.SubjectId,
            SubjectName = document.Subject?.Name ?? string.Empty,
            ChapterId = document.ChapterId,
            ChapterName = document.Chapter?.Name ?? string.Empty,
            LessonId = document.LessonId,
            LessonName = document.Lesson?.Name ?? string.Empty,
            FileName = document.FileName,
            FileType = document.FileType.ToString(),
            Category = document.Category,
            CategoryLabel = document.Category.ToVietnameseLabel(),
            UploadedByName = document.UploadedBy?.FullName ?? string.Empty,
            LastModifiedByName = document.LastModifiedBy?.FullName,
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt,
            FileSizeBytes = document.FileSizeBytes,
            ChunkCount = chunkDtos.Count,
            IndexStatus = document.IndexStatus.ToString(),
            IndexError = document.IndexError,
            ProcessedAt = document.IndexedAt,
            Chunks = chunkDtos
        };

        return details;
    }

    public async Task<UploadDocumentResultDto> UploadAsync(UploadDocumentDto dto, string? ipAddress)
    {
        var extension = Path.GetExtension(dto.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            return new UploadDocumentResultDto
            {
                Success = false,
                ErrorMessage = "Chỉ hỗ trợ file PDF, DOCX, PPTX và TXT."
            };
        }

        var chapter = await _unitOfWork.Chapters.GetByIdAsync(dto.ChapterId);
        if (chapter == null)
        {
            return new UploadDocumentResultDto { Success = false, ErrorMessage = "Chương không tồn tại." };
        }

        if (chapter.SubjectId != dto.SubjectId)
        {
            return new UploadDocumentResultDto { Success = false, ErrorMessage = "Chương không thuộc môn học đã chọn." };
        }

        var lesson = await _unitOfWork.Lessons.GetByIdAsync(dto.LessonId);
        if (lesson == null || lesson.ChapterId != dto.ChapterId)
        {
            return new UploadDocumentResultDto { Success = false, ErrorMessage = "Bài học không hợp lệ hoặc không thuộc chương đã chọn." };
        }

        if (dto.UploaderRole != Roles.Teacher ||
            !await _subjectService.IsTeacherAssignedToSubjectAsync(dto.UploadedByUserId, dto.SubjectId))
        {
            return new UploadDocumentResultDto
            {
                Success = false,
                ErrorMessage = "Bạn chưa được phân công tải tài liệu cho môn học này."
            };
        }

        var maxBytes = _appSettings.MaxUploadBytes;
        if (dto.FileSizeBytes <= 0 || dto.FileSizeBytes > maxBytes)
        {
            return new UploadDocumentResultDto
            {
                Success = false,
                ErrorMessage = $"Kích thước file phải từ 1 byte đến {maxBytes / (1024 * 1024)} MB."
            };
        }

        var lessonDocuments = await _unitOfWork.Documents.GetByLessonIdAsync(dto.LessonId);
        var trimmedFileName = dto.FileName.Trim();
        if (lessonDocuments.Any(d => string.Equals(d.FileName.Trim(), trimmedFileName, StringComparison.OrdinalIgnoreCase)))
        {
            return new UploadDocumentResultDto
            {
                Success = false,
                ErrorMessage = "Tên file đã tồn tại trong bài học này. Vui lòng đổi tên file khác."
            };
        }

        var uploadRoot = DocumentPathHelper.ResolveUploadRoot(_appSettings.UploadPath);
        var subjectFolder = Path.Combine(uploadRoot, dto.SubjectId.ToString(), dto.ChapterId.ToString(), dto.LessonId.ToString());
        Directory.CreateDirectory(subjectFolder);

        var safeFileName = $"{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(subjectFolder, safeFileName);

        await using (var fileStream = File.Create(fullPath))
        {
            dto.FileStream.Position = 0;
            await dto.FileStream.CopyToAsync(fileStream);
        }

        var document = new Document
        {
            SubjectId = dto.SubjectId,
            ChapterId = dto.ChapterId,
            LessonId = dto.LessonId,
            FileName = dto.FileName,
            FilePath = fullPath,
            FileType = MapFileType(extension),
            Category = dto.Category,
            UploadedByUserId = dto.UploadedByUserId,
            FileSizeBytes = dto.FileSizeBytes
        };

        await _unitOfWork.Documents.AddAsync(document);
        await _unitOfWork.SaveChangesAsync();

        await _indexingQueue.EnqueueAsync(document.Id);

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = dto.UploadedByUserId,
            Action = AuditActions.UploadDocument,
            IpAddress = ipAddress,
            Details = $"Uploaded document '{dto.FileName}' to lesson {dto.LessonId} (chapter {dto.ChapterId})"
        });

        var subject = await _subjectService.GetByIdAsync(dto.SubjectId, null, Roles.Admin);
        if (subject != null)
        {
            await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
            {
                Action = SubjectRealtimeActions.Updated,
                SubjectId = subject.Id,
                Subject = subject
            });
        }

        return new UploadDocumentResultDto
        {
            Success = true,
            DocumentId = document.Id,
            ChunksCreated = 0
        };
    }

    public async Task<DocumentOperationResultDto> UpdateAsync(
        UpdateDocumentDto dto, string userId, string role, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(dto.FileName))
            return DocFail("Vui lòng nhập tên file.");

        var document = await _unitOfWork.Documents.GetWithDetailsAsync(dto.Id);
        if (document == null)
            return DocFail("Không tìm thấy tài liệu.");

        // Chỉ giáo viên phụ trách môn học mới được sửa tài liệu.
        if (!CanManageDocument(document, userId, role))
            return DocFail("Bạn không có quyền thao tác trên tài liệu này.");

        if (dto.LessonId != document.LessonId)
        {
            var lesson = await _unitOfWork.Lessons.GetWithChapterAsync(dto.LessonId);
            if (lesson == null || lesson.Chapter.SubjectId != document.SubjectId)
                return DocFail("Bài học không thuộc môn này.");

            document.LessonId = dto.LessonId;
            document.ChapterId = lesson.ChapterId;

            var chunks = await _unitOfWork.Chunks.GetByDocumentIdAsync(document.Id);
            foreach (var chunk in chunks)
            {
                chunk.ChapterId = lesson.ChapterId;
                _unitOfWork.Chunks.Update(chunk);
            }

            var embeddings = await _unitOfWork.Embeddings.GetBySubjectIdAsync(document.SubjectId);
            foreach (var embedding in embeddings.Where(e => e.DocumentId == document.Id))
            {
                embedding.ChapterId = lesson.ChapterId;
                _unitOfWork.Embeddings.Update(embedding);
            }
        }

        var newFileName = dto.FileName.Trim();
        var siblings = await _unitOfWork.Documents.GetByLessonIdAsync(document.LessonId);
        if (siblings.Any(d => d.Id != document.Id &&
                              string.Equals(d.FileName.Trim(), newFileName, StringComparison.OrdinalIgnoreCase)))
        {
            return DocFail("Tên file đã tồn tại trong bài học này. Vui lòng đổi tên file khác.");
        }

        document.FileName = newFileName;
        document.Category = dto.Category;

        var fileReplaced = false;
        if (dto.NewFileStream != null && dto.NewFileSizeBytes > 0)
        {
            var newExtension = Path.GetExtension(dto.NewFileOriginalName ?? string.Empty);
            if (!AllowedExtensions.Contains(newExtension))
                return DocFail("Chỉ hỗ trợ file PDF, DOCX, PPTX và TXT.");

            var maxBytes = _appSettings.MaxUploadBytes;
            if (dto.NewFileSizeBytes > maxBytes)
                return DocFail($"Kích thước file không được vượt quá {maxBytes / (1024 * 1024)} MB.");

            var uploadRoot = DocumentPathHelper.ResolveUploadRoot(_appSettings.UploadPath);
            var folder = Path.Combine(uploadRoot, document.SubjectId.ToString(), document.ChapterId.ToString(), document.LessonId.ToString());
            Directory.CreateDirectory(folder);

            var safeFileName = $"{Guid.NewGuid()}{newExtension}";
            var newFullPath = Path.Combine(folder, safeFileName);
            await using (var fileStream = File.Create(newFullPath))
            {
                dto.NewFileStream.Position = 0;
                await dto.NewFileStream.CopyToAsync(fileStream);
            }

            var oldPath = DocumentPathHelper.ResolvePhysicalPath(document.FilePath);
            document.FilePath = newFullPath;
            document.FileType = MapFileType(newExtension);
            document.FileSizeBytes = dto.NewFileSizeBytes;

            if (!string.IsNullOrEmpty(oldPath) && oldPath != newFullPath && File.Exists(oldPath))
                File.Delete(oldPath);

            // Drop the existing index so re-processing does not duplicate chunks/embeddings.
            await ClearDocumentIndexAsync(document.Id, document.SubjectId);
            document.IndexStatus = DocumentIndexStatus.Pending;
            document.IndexError = null;
            document.IndexedAt = null;
            fileReplaced = true;
        }

        document.LastModifiedByUserId = userId;
        document.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync();

        if (fileReplaced)
            await _indexingQueue.EnqueueAsync(document.Id);

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = userId,
            Action = AuditActions.UpdateDocument,
            IpAddress = ipAddress,
            Details = fileReplaced
                ? $"Replaced file and updated document '{document.FileName}' (Id: {document.Id})"
                : $"Updated document '{document.FileName}' (Id: {document.Id})"
        });

        await _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "Document",
            Action = "Updated",
            EntityId = document.Id,
            Message = $"Document '{document.FileName}' updated"
        });

        var updated = await GetByIdAsync(document.Id, userId, role);
        return new DocumentOperationResultDto { Success = true, Document = updated };
    }

    private async Task ClearDocumentIndexAsync(int documentId, int subjectId)
    {
        var embeddings = await _unitOfWork.Embeddings.GetBySubjectIdAsync(subjectId);
        foreach (var embedding in embeddings.Where(e => e.DocumentId == documentId))
            _unitOfWork.Embeddings.Remove(embedding);

        var chunks = await _unitOfWork.Chunks.GetByDocumentIdAsync(documentId);
        foreach (var chunk in chunks)
            _unitOfWork.Chunks.Remove(chunk);

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id, string userId, string role, string? ipAddress)
    {
        var document = await _unitOfWork.Documents.GetWithDetailsAsync(id);
        if (document == null) return false;

        // Chỉ giáo viên phụ trách môn học mới được xóa tài liệu.
        if (!CanManageDocument(document, userId, role))
            return false;

        var subjectId = document.SubjectId;

        var physicalPath = DocumentPathHelper.ResolvePhysicalPath(document.FilePath);

        if (File.Exists(physicalPath))
            File.Delete(physicalPath);

        _unitOfWork.Documents.Remove(document);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = userId,
            Action = AuditActions.DeleteDocument,
            IpAddress = ipAddress,
            Details = $"Deleted document '{document.FileName}' (Id: {document.Id})"
        });

        var subject = await _subjectService.GetByIdAsync(subjectId, null, Roles.Admin);
        if (subject != null)
        {
            await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
            {
                Action = subject.HasMaterials ? SubjectRealtimeActions.Updated : SubjectRealtimeActions.MaterialsRemoved,
                SubjectId = subject.Id,
                Subject = subject
            });
        }

        return true;
    }

    public async Task<DocumentDownloadResultDto> GetDownloadFileAsync(int documentId, string userId, string role, string? ipAddress)
    {
        var document = await _unitOfWork.Documents.GetWithDetailsAsync(documentId);
        if (document == null)
        {
            return new DocumentDownloadResultDto
            {
                Success = false,
                ErrorMessage = "Không tìm thấy tài liệu."
            };
        }

        if (role == Roles.Teacher && !await _subjectService.CanTeacherAccessSubjectAsync(userId, document.SubjectId))
        {
            return new DocumentDownloadResultDto
            {
                Success = false,
                ErrorMessage = "Bạn chưa được phân công cho môn học này."
            };
        }

        if (role == Roles.Student && !await _subjectService.HasDocumentsAsync(document.SubjectId))
        {
            return new DocumentDownloadResultDto
            {
                Success = false,
                ErrorMessage = "Môn học này chưa có tài liệu khả dụng."
            };
        }

        if (role != Roles.Admin && role != Roles.Teacher && role != Roles.Student)
        {
            return new DocumentDownloadResultDto
            {
                Success = false,
                ErrorMessage = "Bạn không có quyền truy cập."
            };
        }

        var physicalPath = DocumentPathHelper.ResolvePhysicalPath(document.FilePath);

        if (!File.Exists(physicalPath))
        {
            return new DocumentDownloadResultDto
            {
                Success = false,
                ErrorMessage = "File không còn tồn tại trên máy chủ."
            };
        }

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = userId,
            Action = AuditActions.DownloadDocument,
            IpAddress = ipAddress,
            Details = $"Downloaded document '{document.FileName}' (Id: {document.Id})"
        });

        return new DocumentDownloadResultDto
        {
            Success = true,
            FilePath = physicalPath,
            FileName = document.FileName,
            ContentType = ResolveContentType(document.FileName)
        };
    }

    public async Task<DocumentOperationResultDto> ReindexAsync(int documentId, string userId, string role, string? ipAddress)
    {
        var document = await _unitOfWork.Documents.GetWithDetailsAsync(documentId);
        if (document == null)
            return DocFail("Không tìm thấy tài liệu.");

        // Chỉ giáo viên phụ trách môn học mới được xử lý lại nội dung (re-index).
        if (!CanManageDocument(document, userId, role))
            return DocFail("Bạn không có quyền thao tác trên tài liệu này.");

        var physicalPath = DocumentPathHelper.ResolvePhysicalPath(document.FilePath);

        if (!File.Exists(physicalPath))
            return DocFail("File không còn tồn tại trên máy chủ.");

        await ClearDocumentIndexAsync(document.Id, document.SubjectId);
        document.IndexStatus = DocumentIndexStatus.Pending;
        document.IndexError = null;
        document.IndexedAt = null;
        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync();

        await _indexingQueue.EnqueueAsync(document.Id);

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = userId,
            Action = AuditActions.ReindexDocument,
            IpAddress = ipAddress,
            Details = $"Re-queued document '{document.FileName}' (Id: {document.Id}) for indexing"
        });

        return new DocumentOperationResultDto { Success = true, Document = MapToDto(document) };
    }

    // Indexing is executed in background (see IDocumentIndexingService).

    private static DocumentFileType MapFileType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => DocumentFileType.Pdf,
        ".docx" => DocumentFileType.Docx,
        ".pptx" => DocumentFileType.Pptx,
        _ => DocumentFileType.Pdf
    };

    private static DocumentDto MapToDto(Document document) => new()
    {
        Id = document.Id,
        SubjectId = document.SubjectId,
        SubjectName = document.Subject?.Name ?? string.Empty,
        ChapterId = document.ChapterId,
        ChapterName = document.Chapter?.Name ?? string.Empty,
        LessonId = document.LessonId,
        LessonName = document.Lesson?.Name ?? string.Empty,
        FileName = document.FileName,
        FileType = document.FileType.ToString(),
        Category = document.Category,
        CategoryLabel = document.Category.ToVietnameseLabel(),
        UploadedByName = document.UploadedBy?.FullName ?? string.Empty,
        LastModifiedByName = document.LastModifiedBy?.FullName,
        CreatedAt = document.CreatedAt,
        UpdatedAt = document.UpdatedAt,
        FileSizeBytes = document.FileSizeBytes,
        IndexStatus = document.IndexStatus.ToString(),
        IndexError = document.IndexError
    };

    private async Task EnrichWithChunkStatsAsync(DocumentDto dto, int documentId, string role)
    {
        var chunks = await _unitOfWork.Chunks.GetByDocumentIdAsync(documentId);
        dto.ChunkCount = chunks.Count;
    }

    private static string ResolveContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };

    private static bool CanManageDocument(Document document, string userId, string role) =>
        role == Roles.Teacher && document.Subject?.TeacherId == userId;

    private static DocumentOperationResultDto DocFail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
