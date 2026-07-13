using EduAI.Model.Entities;

namespace EduAI.Model.IRepository;

public interface ISubjectAssignmentRepository : IGenericRepository<SubjectAssignment>
{
    Task<SubjectAssignment?> GetCurrentBySubjectAsync(int subjectId);
    Task<IReadOnlyList<SubjectAssignment>> GetHistoryBySubjectAsync(int subjectId);
    Task<bool> HasAnyAssignmentAsync(string teacherId, int subjectId);
}
