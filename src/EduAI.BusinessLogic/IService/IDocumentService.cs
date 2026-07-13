using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentDto>> GetBySubjectAsync(int subjectId, string userId, string role);
    Task<DocumentDto?> GetByIdAsync(int id, string userId, string role);
    Task<DocumentDetailsDto?> GetDetailsByIdAsync(int id, string userId, string role);
    Task<UploadDocumentResultDto> UploadAsync(UploadDocumentDto dto, string? ipAddress);
    Task<DocumentOperationResultDto> UpdateAsync(UpdateDocumentDto dto, string userId, string role, string? ipAddress);
    Task<bool> DeleteAsync(int id, string userId, string role, string? ipAddress);
    Task<DocumentDownloadResultDto> GetDownloadFileAsync(int documentId, string userId, string role, string? ipAddress);
    Task<DocumentOperationResultDto> ReindexAsync(int documentId, string userId, string role, string? ipAddress);
}
