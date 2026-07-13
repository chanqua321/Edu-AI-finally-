using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface ILessonService
{
    Task<IReadOnlyList<LessonDto>> GetBySubjectAsync(int subjectId, string userId, string role);
    Task<IReadOnlyList<LessonDto>> GetByChapterAsync(int chapterId, string userId, string role);
    Task<LessonDto?> GetByIdAsync(int id, string userId, string role);
    Task<LessonDto> CreateAsync(CreateLessonDto dto, string userId, string role);
    Task<LessonDto?> UpdateAsync(UpdateLessonDto dto, string userId, string role);
    Task<bool> DeleteAsync(int id, string userId, string role, string? ipAddress);
}
