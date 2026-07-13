using EduAI.Model.Entities;
using EduAI.Model.Enums;
using EduAI.Model.IRepository;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.Repositories;

public class SubjectAssignmentRepository : GenericRepository<SubjectAssignment>, ISubjectAssignmentRepository
{
    public SubjectAssignmentRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<SubjectAssignment?> GetCurrentBySubjectAsync(int subjectId) =>
        await DbSet.FirstOrDefaultAsync(a =>
            a.SubjectId == subjectId && a.Status == AssignmentStatus.Current);

    public async Task<IReadOnlyList<SubjectAssignment>> GetHistoryBySubjectAsync(int subjectId) =>
        await DbSet.Include(a => a.Teacher).AsNoTracking()
            .Where(a => a.SubjectId == subjectId)
            .OrderByDescending(a => a.Status == AssignmentStatus.Current)
            .ThenByDescending(a => a.StartDate)
            .ToListAsync();

    public async Task<bool> HasAnyAssignmentAsync(string teacherId, int subjectId) =>
        await DbSet.AsNoTracking()
            .AnyAsync(a => a.SubjectId == subjectId && a.TeacherId == teacherId);
}
