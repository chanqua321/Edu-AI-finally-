using EduAI.Model.Entities;

namespace EduAI.Model.IRepository;

public interface ISubjectRepository : IGenericRepository<Subject>
{
    Task<Subject?> GetWithTeacherAsync(int id);
    Task<IReadOnlyList<Subject>> GetAllWithTeacherAsync(bool includeInactive = false);
    Task<IReadOnlyList<Subject>> GetByTeacherIdAsync(string teacherId);
    Task<IReadOnlyList<Subject>> GetForTeacherWithHistoryAsync(string teacherId);
    Task<bool> IsTeacherAssignedToAnotherSubjectAsync(string teacherId, int? excludeSubjectId = null);
    Task<bool> ExistsByNameAsync(string name, int? excludeSubjectId = null);
}
