using EduAI.Model.DTOs;
using EduAI.Model.Entities;

namespace EduAI.BusinessLogic.IService;

public interface ISystemSettingsService
{
    /// <summary>Lấy SystemSettings từ cache (hoặc DB nếu cache chưa có).</summary>
    Task<SystemSettings> GetAsync();

    /// <summary>Lấy DTO để hiển thị trên UI.</summary>
    Task<SystemSettingsDto> GetDtoAsync();

    /// <summary>Admin cập nhật cấu hình hệ thống, tự động refresh cache.</summary>
    Task<SystemSettingsOperationResultDto> UpdateAsync(UpdateSystemSettingsDto dto, string adminId, string? ipAddress);

    /// <summary>Xóa cache, buộc đọc lại từ DB lần tiếp theo.</summary>
    Task RefreshCacheAsync();

    /// <summary>Tạo row mặc định nếu chưa có (gọi khi seeding).</summary>
    Task SeedDefaultAsync(string? embeddingModel = null, string? generationModel = null);
}
