using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface IChapterService
{
    Task<IReadOnlyList<ChapterDto>> GetBySubjectAsync(int subjectId, string userId, string role);
    Task<ChapterDto?> GetByIdAsync(int id, string userId, string role);
    Task<ChapterDto> CreateAsync(CreateChapterDto dto, string userId, string role);
    Task<ChapterDto?> UpdateAsync(UpdateChapterDto dto, string userId, string role);
    Task<bool> DeleteAsync(int id, string userId, string role, string? ipAddress);
}
