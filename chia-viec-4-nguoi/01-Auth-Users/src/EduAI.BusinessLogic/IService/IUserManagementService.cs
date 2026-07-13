using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserDto>> GetAllUsersAsync();
    Task<IReadOnlyList<UserDto>> GetTeachersAsync();
    Task<UserDto?> GetUserByIdAsync(string id);
    Task<CreateUserResultDto> CreateUserAsync(CreateUserDto dto, string adminId, string? ipAddress);
    Task<BulkUserImportResultDto> BulkImportUsersAsync(Stream excelStream, string adminId, string? ipAddress);
    Task<bool> ResetPasswordAsync(string userId, string newPassword, string adminId, string? ipAddress);
    Task<bool> ActivateAccountAsync(string userId, string adminId, string? ipAddress);
    Task<EduAI.Model.DTOs.ResendTeacherConfirmationResultDto> ResendTeacherEmailConfirmationAsync(string userId, string adminId, string? ipAddress);
    Task<bool> ResendTeacherEmailConfirmationByEmailAsync(string email);
    Task<ResendAccountEmailResultDto> ResendAccountEmailAsync(string userId, string adminId, string? ipAddress);
    Task<UserOperationResultDto> UpdateUserAsync(UpdateUserDto dto, string adminId, string? ipAddress);
    Task<UserOperationResultDto> UpdateOwnProfileAsync(UpdateUserDto dto, string userId, string? ipAddress);
    Task<UserOperationResultDto> ChangeOwnPasswordAsync(string userId, string currentPassword, string newPassword);
    Task<UserOperationResultDto> DeleteUserAsync(string userId, string adminId, string? ipAddress);
}
