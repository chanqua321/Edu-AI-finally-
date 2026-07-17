using EduAI.Model.Entities;

namespace EduAI.Model.IRepository;

public interface IAiUsageLogRepository : IGenericRepository<AiUsageLog>
{
    Task<IReadOnlyList<AiUsageLog>> GetRecentAsync(int? subjectId, int days, CancellationToken cancellationToken = default);
}
