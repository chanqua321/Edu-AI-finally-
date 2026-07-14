using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface IIndexingSettingsService
{
    Task<IndexingSettingsDto> GetAsync();
    Task<IndexingSettingsOperationResultDto> UpdateAsync(UpdateIndexingSettingsDto dto, string adminUserId, string? ipAddress);
}
