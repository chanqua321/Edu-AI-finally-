using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.Repositories;

public class ChunkRepository : GenericRepository<DocumentChunk>, IChunkRepository
{
    public ChunkRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetBySubjectIdAsync(int subjectId) =>
        await DbSet.AsNoTracking()
            .Include(c => c.Subject)
            .Include(c => c.Chapter)
            .Include(c => c.Document)
            .Where(c => c.SubjectId == subjectId)
            .OrderBy(c => c.DocumentId)
            .ThenBy(c => c.ChunkIndex)
            .ToListAsync();

    public async Task<IReadOnlyList<DocumentChunk>> GetByDocumentIdAsync(int documentId) =>
        await DbSet.AsNoTracking()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync();

    public async Task<IReadOnlyList<DocumentChunk>> SearchBySubjectAsync(int subjectId, string query, int topK = 5)
    {
        var keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (keywords.Length == 0)
            return Array.Empty<DocumentChunk>();

        var chunks = await DbSet.AsNoTracking()
            .Include(c => c.Document)
            .Include(c => c.Chapter)
            .Where(c => c.SubjectId == subjectId)
            .ToListAsync();

        return chunks
            .Select(c => new
            {
                Chunk = c,
                Score = keywords.Count(k => c.Content.Contains(k, StringComparison.OrdinalIgnoreCase))
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Select(x => x.Chunk)
            .ToList();
    }
}
