using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.Repositories;

public class LessonRepository : GenericRepository<Lesson>, ILessonRepository
{
    public LessonRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Lesson>> GetByChapterIdAsync(int chapterId) =>
        await DbSet.AsNoTracking()
            .Where(l => l.ChapterId == chapterId)
            .OrderBy(l => l.OrderNumber)
            .ThenBy(l => l.Id)
            .ToListAsync();

    public async Task<IReadOnlyList<Lesson>> GetBySubjectIdAsync(int subjectId) =>
        await DbSet.AsNoTracking()
            .Include(l => l.Chapter)
            .Where(l => l.Chapter.SubjectId == subjectId)
            .OrderBy(l => l.OrderNumber)
            .ThenBy(l => l.Id)
            .ToListAsync();

    public async Task<Lesson?> GetWithChapterAsync(int id) =>
        await DbSet.Include(l => l.Chapter)
            .FirstOrDefaultAsync(l => l.Id == id);
}
