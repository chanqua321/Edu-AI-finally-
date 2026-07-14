using EduAI.Model.Entities;

namespace EduAI.Model.IRepository;

public interface IChunkRepository : IGenericRepository<DocumentChunk>
{
    Task<IReadOnlyList<DocumentChunk>> GetBySubjectIdAsync(int subjectId);
    Task<IReadOnlyList<DocumentChunk>> GetByDocumentIdAsync(int documentId);
    Task<IReadOnlyList<DocumentChunk>> SearchBySubjectAsync(int subjectId, string query, int topK = 5);
}
