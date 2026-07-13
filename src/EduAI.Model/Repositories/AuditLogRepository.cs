using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.Repositories;

public class AuditLogRepository : GenericRepository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<AuditLog?> GetDetailByIdAsync(int id) =>
        await DbSet.AsNoTracking()
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<IReadOnlyList<AuditLog>> GetRecentAsync(int count = 100) =>
        await DbSet.AsNoTracking()
            .Include(a => a.User)
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToListAsync();

    public async Task<IReadOnlyList<AuditLog>> SearchAsync(
        string? action,
        string? userId,
        string? ipAddress,
        DateTime? from,
        DateTime? to,
        string? detailsContains,
        int maxResults = 200)
    {
        var query = DbSet.AsNoTracking()
            .Include(a => a.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(a => a.UserId == userId);

        if (!string.IsNullOrWhiteSpace(ipAddress))
            query = query.Where(a => a.IpAddress == ipAddress);

        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.Timestamp <= to.Value);

        if (!string.IsNullOrWhiteSpace(detailsContains))
            query = query.Where(a => a.Details != null && a.Details.Contains(detailsContains));

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Take(Math.Max(1, maxResults))
            .ToListAsync();
    }
}
