using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.Repositories;

public class ChapterRepository : GenericRepository<Chapter>, IChapterRepository
{
    public ChapterRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Chapter>> GetBySubjectIdAsync(int subjectId) =>
        await DbSet.AsNoTracking()
            .Where(c => c.SubjectId == subjectId)
            .OrderBy(c => c.OrderNumber)
            .ToListAsync();

    public async Task<Chapter?> GetWithDocumentAsync(int id) =>
        await DbSet.Include(c => c.Documents).Include(c => c.Subject)
            .FirstOrDefaultAsync(c => c.Id == id);
}
