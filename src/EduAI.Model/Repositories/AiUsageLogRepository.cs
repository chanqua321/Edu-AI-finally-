using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.Repositories;

public class AiUsageLogRepository : GenericRepository<AiUsageLog>, IAiUsageLogRepository
{
    public AiUsageLogRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<AiUsageLog>> GetRecentAsync(
        int? subjectId,
        int days,
        CancellationToken cancellationToken = default)
    {
        var from = DateTime.UtcNow.Date.AddDays(-(days - 1));
        var query = DbSet
            .AsNoTracking()
            .Include(l => l.Subject)
            .Where(l => l.CreatedAt >= from);

        if (subjectId.HasValue)
            query = query.Where(l => l.SubjectId == subjectId.Value);

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
