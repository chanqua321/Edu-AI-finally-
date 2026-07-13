using EduAI.Model.Entities;

namespace EduAI.Model.IRepository;

public interface ILessonRepository : IGenericRepository<Lesson>
{
    Task<IReadOnlyList<Lesson>> GetByChapterIdAsync(int chapterId);
    Task<IReadOnlyList<Lesson>> GetBySubjectIdAsync(int subjectId);
    Task<Lesson?> GetWithChapterAsync(int id);
}
