using EduAI.Model.Entities;

namespace EduAI.Model.IRepository;

public interface IAuditLogRepository : IGenericRepository<AuditLog>
{
    Task<AuditLog?> GetDetailByIdAsync(int id);
    Task<IReadOnlyList<AuditLog>> GetRecentAsync(int count = 100);
    Task<IReadOnlyList<AuditLog>> SearchAsync(
        string? action,
        string? userId,
        string? ipAddress,
        DateTime? from,
        DateTime? to,
        string? detailsContains,
        int maxResults = 200);
}
