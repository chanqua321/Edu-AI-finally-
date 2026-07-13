using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface IEmbeddingService
{
    Task<IReadOnlyList<EmbeddingDto>> GetBySubjectAsync(int subjectId, string userId, string role);
    Task<EmbeddingDto?> GetByIdAsync(int id, string userId, string role);
    Task<EmbeddingDto?> GetByChunkIdAsync(int chunkId, string userId, string role);
    Task<EmbeddingOperationResultDto> CreateForChunkAsync(int chunkId, string userId, string role);
    Task<EmbeddingOperationResultDto> RegenerateForChunkAsync(int chunkId, string userId, string role);
    Task<bool> DeleteAsync(int id, string userId, string role);
}
