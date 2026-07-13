using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using EduAI.BusinessLogic.IService;

namespace EduAI.BusinessLogic.Services;

public class ChapterService : IChapterService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubjectService _subjectService;
    private readonly IAuditLogService _auditLogService;

    public ChapterService(IUnitOfWork unitOfWork, ISubjectService subjectService, IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _subjectService = subjectService;
        _auditLogService = auditLogService;
    }

    public async Task<IReadOnlyList<ChapterDto>> GetBySubjectAsync(int subjectId, string userId, string role)
    {
        if (!await CanAccessSubjectAsync(subjectId, userId, role))
            return Array.Empty<ChapterDto>();

        var chapters = await _unitOfWork.Chapters.GetBySubjectIdAsync(subjectId);
        var subject = await _unitOfWork.Subjects.GetByIdAsync(subjectId);
        var result = new List<ChapterDto>();

        foreach (var chapter in chapters)
        {
            var documents = await _unitOfWork.Documents.GetByChapterIdAsync(chapter.Id);
            var lessonCount = (await _unitOfWork.Lessons.GetByChapterIdAsync(chapter.Id)).Count;
            var chunkCount = 0;
            foreach (var document in documents)
                chunkCount += (await _unitOfWork.Chunks.GetByDocumentIdAsync(document.Id)).Count;

            result.Add(new ChapterDto
            {
                Id = chapter.Id,
                SubjectId = chapter.SubjectId,
                SubjectName = subject?.Name ?? string.Empty,
                Name = chapter.Name,
                OrderNumber = chapter.OrderNumber,
                DocumentCount = documents.Count,
                LessonCount = lessonCount,
                ChunkCount = chunkCount
            });
        }

        return result;
    }

    public async Task<ChapterDto?> GetByIdAsync(int id, string userId, string role)
    {
        var chapter = await _unitOfWork.Chapters.GetWithDocumentAsync(id);
        if (chapter == null) return null;

        if (!await CanAccessSubjectAsync(chapter.SubjectId, userId, role))
            return null;

        var chunkCount = 0;
        foreach (var document in chapter.Documents)
            chunkCount += (await _unitOfWork.Chunks.GetByDocumentIdAsync(document.Id)).Count;

        var lessonCount = (await _unitOfWork.Lessons.GetByChapterIdAsync(chapter.Id)).Count;

        return new ChapterDto
        {
            Id = chapter.Id,
            SubjectId = chapter.SubjectId,
            SubjectName = chapter.Subject.Name,
            Name = chapter.Name,
            OrderNumber = chapter.OrderNumber,
            DocumentCount = chapter.Documents.Count,
            LessonCount = lessonCount,
            ChunkCount = chunkCount
        };
    }

    public async Task<ChapterDto> CreateAsync(CreateChapterDto dto, string userId, string role)
    {
        if (role != Roles.Teacher || !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, dto.SubjectId))
            throw new UnauthorizedAccessException("Bạn không được phân công cho môn học này.");

        var chapter = new Chapter
        {
            SubjectId = dto.SubjectId,
            Name = dto.Name.Trim(),
            OrderNumber = dto.OrderNumber
        };

        await _unitOfWork.Chapters.AddAsync(chapter);
        await _unitOfWork.SaveChangesAsync();

        var subject = await _unitOfWork.Subjects.GetByIdAsync(dto.SubjectId);
        return new ChapterDto
        {
            Id = chapter.Id,
            SubjectId = chapter.SubjectId,
            SubjectName = subject?.Name ?? string.Empty,
            Name = chapter.Name,
            OrderNumber = chapter.OrderNumber,
            DocumentCount = 0,
            LessonCount = 0
        };
    }

    public async Task<ChapterDto?> UpdateAsync(UpdateChapterDto dto, string userId, string role)
    {
        var chapter = await _unitOfWork.Chapters.GetByIdAsync(dto.Id);
        if (chapter == null) return null;

        if (role != Roles.Teacher || !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, chapter.SubjectId))
            return null;

        chapter.Name = dto.Name.Trim();
        chapter.OrderNumber = dto.OrderNumber;
        chapter.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Chapters.Update(chapter);
        await _unitOfWork.SaveChangesAsync();

        var subject = await _unitOfWork.Subjects.GetByIdAsync(chapter.SubjectId);
        var documents = await _unitOfWork.Documents.GetByChapterIdAsync(chapter.Id);

        return new ChapterDto
        {
            Id = chapter.Id,
            SubjectId = chapter.SubjectId,
            SubjectName = subject?.Name ?? string.Empty,
            Name = chapter.Name,
            OrderNumber = chapter.OrderNumber,
            DocumentCount = documents.Count
        };
    }

    public async Task<bool> DeleteAsync(int id, string userId, string role, string? ipAddress)
    {
        var chapter = await _unitOfWork.Chapters.GetByIdAsync(id);
        if (chapter == null) return false;

        if (role != Roles.Teacher || !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, chapter.SubjectId))
            return false;

        var documents = await _unitOfWork.Documents.GetByChapterIdAsync(chapter.Id);
        foreach (var document in documents)
        {
            if (!string.IsNullOrEmpty(document.FilePath) && File.Exists(document.FilePath))
                File.Delete(document.FilePath);
        }

        var documentCount = documents.Count;
        var chapterName = chapter.Name;
        var subjectId = chapter.SubjectId;

        // Deleting a chapter cascades to its documents, chunks and embeddings.
        _unitOfWork.Chapters.Remove(chapter);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = userId,
            Action = AuditActions.DeleteChapter,
            IpAddress = ipAddress,
            Details = $"Deleted chapter '{chapterName}' (Id: {id}, Subject: {subjectId}) along with {documentCount} document(s)"
        });

        return true;
    }

    private async Task<bool> CanAccessSubjectAsync(int subjectId, string userId, string role)
    {
        if (role == Roles.Admin || role == Roles.Student)
            return true;

        if (role == Roles.Teacher)
            return await _subjectService.IsTeacherAssignedToSubjectAsync(userId, subjectId);

        return false;
    }
}
