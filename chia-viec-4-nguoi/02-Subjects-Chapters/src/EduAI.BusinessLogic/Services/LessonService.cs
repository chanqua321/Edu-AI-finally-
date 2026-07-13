using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using EduAI.BusinessLogic.IService;

namespace EduAI.BusinessLogic.Services;

public class LessonService : ILessonService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubjectService _subjectService;
    private readonly IAuditLogService _auditLogService;

    public LessonService(IUnitOfWork unitOfWork, ISubjectService subjectService, IAuditLogService auditLogService)
    {
        _unitOfWork = unitOfWork;
        _subjectService = subjectService;
        _auditLogService = auditLogService;
    }

    public async Task<IReadOnlyList<LessonDto>> GetBySubjectAsync(int subjectId, string userId, string role)
    {
        if (!await CanAccessSubjectAsync(subjectId, userId, role))
            return Array.Empty<LessonDto>();

        var lessons = await _unitOfWork.Lessons.GetBySubjectIdAsync(subjectId);
        var documentCounts = await _unitOfWork.Documents.GetCountsByLessonIdsAsync(lessons.Select(l => l.Id).ToList());
        var result = new List<LessonDto>();
        foreach (var lesson in lessons)
            result.Add(MapToDto(lesson, documentCounts));
        return result;
    }

    public async Task<IReadOnlyList<LessonDto>> GetByChapterAsync(int chapterId, string userId, string role)
    {
        var chapter = await _unitOfWork.Chapters.GetByIdAsync(chapterId);
        if (chapter == null || !await CanAccessSubjectAsync(chapter.SubjectId, userId, role))
            return Array.Empty<LessonDto>();

        var lessons = await _unitOfWork.Lessons.GetByChapterIdAsync(chapterId);
        var documentCounts = await _unitOfWork.Documents.GetCountsByLessonIdsAsync(lessons.Select(l => l.Id).ToList());
        var result = new List<LessonDto>();
        foreach (var lesson in lessons)
            result.Add(MapToDto(lesson, documentCounts));
        return result;
    }

    public async Task<LessonDto?> GetByIdAsync(int id, string userId, string role)
    {
        var lesson = await _unitOfWork.Lessons.GetWithChapterAsync(id);
        if (lesson == null || !await CanAccessSubjectAsync(lesson.Chapter.SubjectId, userId, role))
            return null;

        var documentCounts = await _unitOfWork.Documents.GetCountsByLessonIdsAsync(new[] { lesson.Id });
        return MapToDto(lesson, documentCounts);
    }

    public async Task<LessonDto> CreateAsync(CreateLessonDto dto, string userId, string role)
    {
        var chapter = await _unitOfWork.Chapters.GetByIdAsync(dto.ChapterId)
            ?? throw new InvalidOperationException("Chương không tồn tại.");

        if (!await CanManageSubjectAsync(chapter.SubjectId, userId, role))
            throw new UnauthorizedAccessException("Bạn không được phân công cho môn học này.");

        var trimmedName = dto.Name.Trim();
        var siblings = await _unitOfWork.Lessons.GetByChapterIdAsync(dto.ChapterId);
        if (siblings.Any(l => string.Equals(l.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Bài \"{trimmedName}\" đã tồn tại trong chương này.");

        var lesson = new Lesson
        {
            ChapterId = dto.ChapterId,
            Name = trimmedName,
            OrderNumber = dto.OrderNumber
        };

        await _unitOfWork.Lessons.AddAsync(lesson);
        await _unitOfWork.SaveChangesAsync();

        lesson.Chapter = chapter;
        return MapToDto(lesson, new Dictionary<int, int>());
    }

    public async Task<LessonDto?> UpdateAsync(UpdateLessonDto dto, string userId, string role)
    {
        var lesson = await _unitOfWork.Lessons.GetWithChapterAsync(dto.Id);
        if (lesson == null) return null;

        if (!await CanManageSubjectAsync(lesson.Chapter.SubjectId, userId, role))
            return null;

        lesson.Name = dto.Name.Trim();
        lesson.OrderNumber = dto.OrderNumber;
        lesson.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Lessons.Update(lesson);
        await _unitOfWork.SaveChangesAsync();

        var documentCounts = await _unitOfWork.Documents.GetCountsByLessonIdsAsync(new[] { lesson.Id });
        return MapToDto(lesson, documentCounts);
    }

    public async Task<bool> DeleteAsync(int id, string userId, string role, string? ipAddress)
    {
        var lesson = await _unitOfWork.Lessons.GetWithChapterAsync(id);
        if (lesson == null) return false;

        if (!await CanManageSubjectAsync(lesson.Chapter.SubjectId, userId, role))
            return false;

        // Remove physical files first; the DB cascade (Lesson -> Document -> Chunk -> Embedding)
        // will clear the related rows when the lesson is removed.
        var documents = await _unitOfWork.Documents.GetByLessonIdAsync(id);
        foreach (var document in documents)
        {
            if (!string.IsNullOrEmpty(document.FilePath) && File.Exists(document.FilePath))
                File.Delete(document.FilePath);
        }

        var lessonName = lesson.Name;
        var chapterId = lesson.ChapterId;

        _unitOfWork.Lessons.Remove(lesson);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = userId,
            Action = AuditActions.DeleteLesson,
            IpAddress = ipAddress,
            Details = $"Deleted lesson '{lessonName}' (Id: {id}, Chapter: {chapterId}) along with {documents.Count} document(s)"
        });

        return true;
    }

    private static LessonDto MapToDto(Lesson lesson, IReadOnlyDictionary<int, int> documentCounts)
    {
        documentCounts.TryGetValue(lesson.Id, out var documentCount);
        return new LessonDto
        {
            Id = lesson.Id,
            ChapterId = lesson.ChapterId,
            ChapterName = lesson.Chapter?.Name ?? string.Empty,
            SubjectId = lesson.Chapter?.SubjectId ?? 0,
            Name = lesson.Name,
            OrderNumber = lesson.OrderNumber,
            DocumentCount = documentCount
        };
    }

    private async Task<bool> CanAccessSubjectAsync(int subjectId, string userId, string role)
    {
        if (role == Roles.Admin || role == Roles.Student)
            return true;
        if (role == Roles.Teacher)
            return await _subjectService.IsTeacherAssignedToSubjectAsync(userId, subjectId);
        return false;
    }

    private async Task<bool> CanManageSubjectAsync(int subjectId, string userId, string role)
    {
        if (role == Roles.Teacher)
            return await _subjectService.IsTeacherAssignedToSubjectAsync(userId, subjectId);
        return false;
    }
}
