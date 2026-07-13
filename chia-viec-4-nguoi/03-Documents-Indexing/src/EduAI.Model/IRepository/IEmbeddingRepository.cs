using EduAI.Model.Entities;

namespace EduAI.Model.IRepository;

public interface IEmbeddingRepository : IGenericRepository<DocumentEmbedding>
{
    Task<IReadOnlyList<DocumentEmbedding>> GetBySubjectIdAsync(int subjectId);
    Task<DocumentEmbedding?> GetByChunkIdAsync(int chunkId);
}
