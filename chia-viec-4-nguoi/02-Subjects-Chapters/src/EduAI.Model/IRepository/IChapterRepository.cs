using EduAI.Model.Entities;

namespace EduAI.Model.IRepository;

public interface IChapterRepository : IGenericRepository<Chapter>
{
    Task<IReadOnlyList<Chapter>> GetBySubjectIdAsync(int subjectId);
    Task<Chapter?> GetWithDocumentAsync(int id);
}
