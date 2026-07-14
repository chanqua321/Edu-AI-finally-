using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.Repositories;

public class EmbeddingRepository : GenericRepository<DocumentEmbedding>, IEmbeddingRepository
{
    public EmbeddingRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<DocumentEmbedding>> GetBySubjectIdAsync(int subjectId) =>
        await DbSet.AsNoTracking().Where(e => e.SubjectId == subjectId).ToListAsync();

    public async Task<DocumentEmbedding?> GetByChunkIdAsync(int chunkId) =>
        await DbSet.FirstOrDefaultAsync(e => e.ChunkId == chunkId);
}
