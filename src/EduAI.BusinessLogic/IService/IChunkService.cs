using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface IChunkService
{
    Task<IReadOnlyList<ChunkDto>> GetBySubjectAsync(int subjectId, string userId, string role, string? keyword = null);
    Task<IReadOnlyList<ChunkDto>> GetByDocumentAsync(int documentId, string userId, string role);
    Task<ChunkDto?> GetByIdAsync(int id, string userId, string role);
    Task<ChunkOperationResultDto> CreateAsync(CreateChunkDto dto, string userId, string role);
    Task<ChunkOperationResultDto> UpdateAsync(UpdateChunkDto dto, string userId, string role);
    Task<bool> DeleteAsync(int id, string userId, string role);
}
