using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.Repositories;

public class SubjectRepository : GenericRepository<Subject>, ISubjectRepository
{
    public SubjectRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Subject?> GetWithTeacherAsync(int id) =>
        await DbSet.Include(s => s.Teacher).FirstOrDefaultAsync(s => s.Id == id);

    public async Task<IReadOnlyList<Subject>> GetAllWithTeacherAsync(bool includeInactive = false)
    {
        var query = DbSet.Include(s => s.Teacher).AsNoTracking();
        if (!includeInactive)
            query = query.Where(s => s.IsActive);
        return await query.OrderBy(s => s.Name).ToListAsync();
    }

    public async Task<IReadOnlyList<Subject>> GetByTeacherIdAsync(string teacherId) =>
        await DbSet.Include(s => s.Teacher).AsNoTracking()
            .Where(s => s.TeacherId == teacherId && s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

    // Active subjects the teacher currently teaches OR previously taught (assignment history).
    public async Task<IReadOnlyList<Subject>> GetForTeacherWithHistoryAsync(string teacherId) =>
        await DbSet.Include(s => s.Teacher).AsNoTracking()
            .Where(s => s.IsActive &&
                (s.TeacherId == teacherId ||
                 Context.Set<SubjectAssignment>().Any(a => a.SubjectId == s.Id && a.TeacherId == teacherId)))
            .OrderBy(s => s.Name)
            .ToListAsync();

    public async Task<bool> IsTeacherAssignedToAnotherSubjectAsync(string teacherId, int? excludeSubjectId = null)
    {
        var query = DbSet.Where(s => s.TeacherId == teacherId);
        if (excludeSubjectId.HasValue)
            query = query.Where(s => s.Id != excludeSubjectId.Value);
        return await query.AnyAsync();
    }

    public async Task<bool> ExistsByNameAsync(string name, int? excludeSubjectId = null)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var query = DbSet.Where(s => s.Name.ToLower() == normalized);
        if (excludeSubjectId.HasValue)
            query = query.Where(s => s.Id != excludeSubjectId.Value);
        return await query.AnyAsync();
    }
}
