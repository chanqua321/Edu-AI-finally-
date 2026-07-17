using System.Text;
using ClosedXML.Excel;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.BusinessLogic.IService;
using EduAI.BusinessLogic.Helpers;
using Microsoft.AspNetCore.Identity;
using EduAI.Model.Settings;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace EduAI.BusinessLogic.Services;

public class UserManagementService : IUserManagementService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IEmailService _emailService;
    private readonly IAuditLogService _auditLogService;
    private readonly INotificationService _notificationService;
    private readonly IUserNotificationService _userNotificationService;
    private readonly IPaymentService _paymentService;
    // appsettings: "AppSettings:LoginUrl" / "AppBaseUrl" → link đăng nhập gửi trong email tạo tài khoản.
    private readonly AppSettings _appSettings;

    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IEmailService emailService,
        IAuditLogService auditLogService,
        INotificationService notificationService,
        IUserNotificationService userNotificationService,
        IPaymentService paymentService,
        IOptions<AppSettings> appSettings)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _emailService = emailService;
        _auditLogService = auditLogService;
        _notificationService = notificationService;
        _userNotificationService = userNotificationService;
        _paymentService = paymentService;
        _appSettings = appSettings.Value;
    }

    public async Task<IReadOnlyList<UserDto>> GetAllUsersAsync()
    {
        var users = _userManager.Users.OrderBy(u => u.FullName).ToList();
        return await MapUsersAsync(users);
    }

    public async Task<IReadOnlyList<UserDto>> GetTeachersAsync()
    {
        var teachers = await _userManager.GetUsersInRoleAsync(Roles.Teacher);
        return await MapUsersAsync(teachers.Where(t => t.IsActive && t.EmailConfirmed).OrderBy(u => u.FullName).ToList());
    }

    public async Task<UserDto?> GetUserByIdAsync(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null) return null;
        var mapped = await MapUsersAsync(new List<ApplicationUser> { user });
        return mapped.FirstOrDefault();
    }

    public async Task<CreateUserResultDto> CreateUserAsync(CreateUserDto dto, string adminId, string? ipAddress)
    {
        if (dto.Role is not (Roles.Teacher or Roles.Student))
        {
            return new CreateUserResultDto
            {
                Success = false,
                ErrorMessage = "Chỉ có thể tạo tài khoản Giáo viên hoặc Học sinh."
            };
        }

        var existingEmail = await _userManager.FindByEmailAsync(dto.Email);
        if (existingEmail != null)
        {
            return new CreateUserResultDto { Success = false, ErrorMessage = "Email đã tồn tại." };
        }

        var email = dto.Email.Trim();
        var userName = string.IsNullOrWhiteSpace(dto.UserName) ? email : dto.UserName.Trim();

        var existingUserName = await _userManager.FindByNameAsync(userName);
        if (existingUserName != null)
        {
            return new CreateUserResultDto { Success = false, ErrorMessage = "Tên đăng nhập đã tồn tại." };
        }

        var password = string.IsNullOrWhiteSpace(dto.Password)
            ? PasswordHelper.GenerateTemporaryPassword()
            : dto.Password.Trim();
        var passwordWasGenerated = string.IsNullOrWhiteSpace(dto.Password);
        var isTeacher = dto.Role == Roles.Teacher;
        var isStudent = dto.Role == Roles.Student;

        var user = new ApplicationUser
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            UserName = userName,
            EmailConfirmed = !isTeacher || EmailService.DisabledForTesting,
            IsActive = true,
            MustChangePassword = true
        };

        // Temporary password should not be blocked by Identity password policy.
        // Users are forced to change it on first login (MustChangePassword = true),
        // and the new password will be validated by the policy.
        user.PasswordHash = _userManager.PasswordHasher.HashPassword(user, password);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");

        var result = await _userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            return new CreateUserResultDto
            {
                Success = false,
                ErrorMessage = string.Join("; ", result.Errors.Select(e => e.Description))
            };
        }

        await _userManager.AddToRoleAsync(user, dto.Role);

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.CreateAccount,
            IpAddress = ipAddress,
            Details = $"Created {dto.Role} account: {user.UserName} ({user.Email})"
        });

        var emailSent = false;
        if (!EmailService.DisabledForTesting)
        {
            var loginUrl = ResolveLoginUrl();

            if (isTeacher)
            {
                var confirmationUrl = await BuildEmailConfirmationUrlAsync(user);
                emailSent = await _emailService.SendTeacherAccountEmailAsync(
                    user.Email!, user.FullName, user.Email!, password, loginUrl, confirmationUrl);

                if (!emailSent)
                {
                    await _auditLogService.LogAsync(new CreateAuditLogDto
                    {
                        UserId = adminId,
                        Action = AuditActions.EmailSendFailed,
                        IpAddress = ipAddress,
                        Details = $"Failed to send teacher welcome/confirmation email to {user.Email}"
                    });
                }
            }
            else if (isStudent)
            {
                emailSent = await _emailService.SendStudentAccountEmailAsync(
                    user.Email!, user.FullName, user.Email!, password, loginUrl);

                if (!emailSent)
                {
                    await _auditLogService.LogAsync(new CreateAuditLogDto
                    {
                        UserId = adminId,
                        Action = AuditActions.EmailSendFailed,
                        IpAddress = ipAddress,
                        Details = $"Failed to send student welcome email to {user.Email}"
                    });
                }
            }
        }

        if (isStudent)
        {
            var activeSub = await _paymentService.GetActiveSubscriptionAsync(user.Id);
            if (activeSub == null)
            {
                var txn = await _paymentService.CreateTransactionAsync(user.Id, PaymentPackageIds.Free, 0);
                await _paymentService.CompleteTransactionAsync(txn.Id, "FREE_ADMIN_CREATE", TransactionStatus.Success);
            }
        }

        return new CreateUserResultDto
        {
            Success = true,
            UserId = user.Id,
            TemporaryPassword = passwordWasGenerated ? password : null,
            EmailSent = emailSent
        };
    }

    public async Task<BulkUserImportResultDto> BulkImportUsersAsync(Stream excelStream, string adminId, string? ipAddress)
    {
        var result = new BulkUserImportResultDto();

        using var workbook = new XLWorkbook(excelStream);
        var worksheet = workbook.Worksheet(1);
        var range = worksheet.RangeUsed();

        if (range == null)
        {
            result.TotalRows = 0;
            return result;
        }

        var rows = range.RowsUsed().Skip(1); // Skip header row
        var rowsList = rows.ToList();
        result.TotalRows = rowsList.Count;

        foreach (var row in rowsList)
        {
            var rowNumber = row.RowNumber();
            var fullName = row.Cell(1).GetString().Trim();
            var email = row.Cell(2).GetString().Trim();
            var role = row.Cell(3).GetString().Trim();

            var rowResult = new BulkUserImportRowResultDto
            {
                RowNumber = rowNumber,
                FullName = fullName,
                Email = email,
                Role = role
            };

            if (string.IsNullOrWhiteSpace(fullName))
            {
                rowResult.ErrorMessage = "Full name is required.";
                result.Results.Add(rowResult);
                result.FailCount++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                rowResult.ErrorMessage = "Email is required.";
                result.Results.Add(rowResult);
                result.FailCount++;
                continue;
            }

            if (role is not (Roles.Teacher or Roles.Student))
            {
                rowResult.ErrorMessage = $"Invalid role '{role}'. Must be '{Roles.Teacher}' or '{Roles.Student}'.";
                result.Results.Add(rowResult);
                result.FailCount++;
                continue;
            }

            var createResult = await CreateUserAsync(new CreateUserDto
            {
                FullName = fullName,
                Email = email,
                UserName = email,
                Role = role
            }, adminId, ipAddress);

            if (createResult.Success)
            {
                rowResult.Success = true;
                rowResult.TemporaryPassword = createResult.TemporaryPassword;
                result.SuccessCount++;
            }
            else
            {
                rowResult.ErrorMessage = createResult.ErrorMessage;
                result.FailCount++;
            }

            result.Results.Add(rowResult);
        }

        return result;
    }

    public async Task<bool> ResendTeacherEmailConfirmationByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user == null)
            return false;

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains(Roles.Teacher) || user.EmailConfirmed || !user.IsActive)
            return false;

        var confirmationUrl = await BuildEmailConfirmationUrlAsync(user);
        var loginUrl = ResolveLoginUrl();

        // When resending confirmation, create a new temporary password because the old one isn't stored.
        var tempPassword = PasswordHelper.GenerateTemporaryPassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, token, tempPassword);
        if (!resetResult.Succeeded)
            return false;

        user.MustChangePassword = true;
        await _userManager.UpdateAsync(user);

        return await _emailService.SendTeacherEmailConfirmationAsync(
            user.Email!, user.FullName, confirmationUrl, loginUrl, tempPassword);
    }

    public async Task<ResendTeacherConfirmationResultDto> ResendTeacherEmailConfirmationAsync(
        string userId, string adminId, string? ipAddress)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return new ResendTeacherConfirmationResultDto { Success = false };

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains(Roles.Teacher) || user.EmailConfirmed)
            return new ResendTeacherConfirmationResultDto { Success = false };

        if (!user.IsActive)
            return new ResendTeacherConfirmationResultDto { Success = false };

        var confirmationUrl = await BuildEmailConfirmationUrlAsync(user);
        var loginUrl = ResolveLoginUrl();

        var tempPassword = PasswordHelper.GenerateTemporaryPassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, token, tempPassword);
        if (!resetResult.Succeeded)
            return new ResendTeacherConfirmationResultDto { Success = false };

        user.MustChangePassword = true;
        await _userManager.UpdateAsync(user);

        var sent = await _emailService.SendTeacherEmailConfirmationAsync(
            user.Email!, user.FullName, confirmationUrl, loginUrl, tempPassword);

        if (!sent)
        {
            await _auditLogService.LogAsync(new CreateAuditLogDto
            {
                UserId = adminId,
                Action = AuditActions.EmailSendFailed,
                IpAddress = ipAddress,
                Details = $"Failed to resend email confirmation to teacher {user.Email}"
            });
            return new ResendTeacherConfirmationResultDto { Success = false };
        }

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.EmailConfirmationResent,
            IpAddress = ipAddress,
            Details = $"Resent email confirmation to teacher {user.Email}"
        });

        return new ResendTeacherConfirmationResultDto
        {
            Success = true,
            TemporaryPassword = tempPassword
        };
    }

    public async Task<ResendAccountEmailResultDto> ResendAccountEmailAsync(string userId, string adminId, string? ipAddress)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new ResendAccountEmailResultDto
            {
                Success = false,
                ErrorMessage = "Không tìm thấy người dùng."
            };
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(Roles.Admin))
        {
            return new ResendAccountEmailResultDto
            {
                Success = false,
                ErrorMessage = "Không hỗ trợ gửi lại mật khẩu cho tài khoản Admin."
            };
        }

        var isTeacher = roles.Contains(Roles.Teacher);
        var isStudent = roles.Contains(Roles.Student);
        if (!isTeacher && !isStudent)
        {
            return new ResendAccountEmailResultDto
            {
                Success = false,
                ErrorMessage = "Vai trò tài khoản không hợp lệ."
            };
        }

        if (!user.IsActive)
        {
            return new ResendAccountEmailResultDto
            {
                Success = false,
                ErrorMessage = "Tài khoản đã bị khóa. Không thể gửi lại email."
            };
        }

        var tempPassword = PasswordHelper.GenerateTemporaryPassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, token, tempPassword);
        if (!resetResult.Succeeded)
        {
            return new ResendAccountEmailResultDto
            {
                Success = false,
                ErrorMessage = string.Join("; ", resetResult.Errors.Select(e => e.Description))
            };
        }

        user.MustChangePassword = true;
        await _userManager.UpdateAsync(user);

        var loginUrl = ResolveLoginUrl();
        bool sent;

        if (isTeacher)
        {
            if (user.EmailConfirmed)
            {
                sent = await _emailService.SendTeacherCredentialsEmailAsync(
                    user.Email!, user.FullName, user.Email!, tempPassword, loginUrl);
            }
            else
            {
                var confirmationUrl = await BuildEmailConfirmationUrlAsync(user);
                sent = await _emailService.SendTeacherAccountEmailAsync(
                    user.Email!, user.FullName, user.Email!, tempPassword, loginUrl, confirmationUrl);
            }
        }
        else
        {
            sent = await _emailService.SendStudentAccountEmailAsync(
                user.Email!, user.FullName, user.Email!, tempPassword, loginUrl);
        }

        if (!sent)
        {
            await _auditLogService.LogAsync(new CreateAuditLogDto
            {
                UserId = adminId,
                Action = AuditActions.EmailSendFailed,
                IpAddress = ipAddress,
                Details = $"Failed to resend account email to {user.Email}"
            });
            return new ResendAccountEmailResultDto
            {
                Success = false,
                EmailSent = false,
                TemporaryPassword = tempPassword,
                ErrorMessage = "Không gửi được email. Vui lòng kiểm tra cấu hình SMTP."
            };
        }

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.EmailConfirmationResent,
            IpAddress = ipAddress,
            Details = $"Resent account email to {user.Email}"
        });

        return new ResendAccountEmailResultDto
        {
            Success = true,
            EmailSent = true,
            TemporaryPassword = tempPassword
        };
    }

    public async Task<bool> ResetPasswordAsync(string userId, string newPassword, string adminId, string? ipAddress)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        if (await IsAdminAccountAsync(user))
            return false;

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
            return false;

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.ResetPassword,
            IpAddress = ipAddress,
            Details = $"Reset password for account: {user.UserName}"
        });

        return true;
    }

    public async Task<bool> ActivateAccountAsync(string userId, string adminId, string? ipAddress)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        if (await IsAdminAccountAsync(user))
            return false;

        user.IsActive = true;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return false;

        await _userManager.UpdateSecurityStampAsync(user);

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.ActivateAccount,
            IpAddress = ipAddress,
            Details = $"Activated account: {user.UserName}"
        });

        await NotifySessionInvalidatedAsync(user, "Tài khoản đã được mở khóa. Vui lòng đăng nhập lại.");

        await _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "User",
            Action = "Activated",
            Message = $"User {user.FullName} unlocked"
        });

        return true;
    }

    public async Task<UserOperationResultDto> UpdateUserAsync(UpdateUserDto dto, string adminId, string? ipAddress)
    {
        var user = await _userManager.FindByIdAsync(dto.Id);
        if (user == null)
            return UserFail("Không tìm thấy người dùng.");

        if (await IsAdminAccountAsync(user))
            return UserFail("Không thể chỉnh sửa tài khoản Admin.");

        return await UpdateUserCoreAsync(user, dto, adminId, ipAddress, AuditActions.UpdateUser);
    }

    private async Task<UserOperationResultDto> UpdateUserCoreAsync(
        ApplicationUser user,
        UpdateUserDto dto,
        string actorId,
        string? ipAddress,
        string auditAction)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Email))
            return UserFail("Vui lòng nhập họ tên và email.");

        var email = dto.Email.Trim();
        var existingEmail = await _userManager.FindByEmailAsync(email);
        if (existingEmail != null && existingEmail.Id != user.Id)
            return UserFail("Email đã được sử dụng bởi tài khoản khác.");

        user.FullName = dto.FullName.Trim();
        var emailResult = await _userManager.SetEmailAsync(user, email);
        if (!emailResult.Succeeded)
            return UserFail(string.Join("; ", emailResult.Errors.Select(e => e.Description)));

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return UserFail(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = actorId,
            Action = auditAction,
            IpAddress = ipAddress,
            Details = $"Updated profile for {user.UserName}"
        });

        await _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "User",
            Action = "Updated",
            Message = $"User {user.FullName} updated"
        });

        return new UserOperationResultDto { Success = true };
    }

    public async Task<UserOperationResultDto> UpdateOwnProfileAsync(UpdateUserDto dto, string userId, string? ipAddress)
    {
        if (dto.Id != userId)
            return UserFail("Bạn chỉ có thể cập nhật hồ sơ của chính mình.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return UserFail("Không tìm thấy người dùng.");

        if (await IsAdminAccountAsync(user))
            return UserFail("Không thể chỉnh sửa tài khoản Admin.");

        return await UpdateUserCoreAsync(user, dto, userId, ipAddress, AuditActions.UpdateUser);
    }

    public async Task<UserOperationResultDto> ChangeOwnPasswordAsync(
        string userId, string currentPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return UserFail("Không tìm thấy người dùng.");

        if (await IsAdminAccountAsync(user))
            return UserFail("Không thể chỉnh sửa tài khoản Admin.");

        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            return UserFail("Vui lòng nhập mật khẩu hiện tại và mật khẩu mới.");

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            return UserFail(string.Join("; ", result.Errors.Select(e => e.Description)));

        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);
        await _userManager.UpdateSecurityStampAsync(user);

        return new UserOperationResultDto { Success = true };
    }

    public async Task<UserOperationResultDto> DeleteUserAsync(string userId, string adminId, string? ipAddress)
    {
        if (userId == adminId)
            return UserFail("Bạn không thể khóa chính tài khoản của mình.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return UserFail("Không tìm thấy người dùng.");

        if (await IsAdminAccountAsync(user))
            return UserFail("Không thể khóa tài khoản Admin.");

        if (!user.IsActive)
            return UserFail("Tài khoản đã bị khóa trước đó.");

        user.IsActive = false;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return UserFail(string.Join("; ", result.Errors.Select(e => e.Description)));

        await _userManager.UpdateSecurityStampAsync(user);

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.DisableAccount,
            IpAddress = ipAddress,
            Details = $"Locked account (soft delete): {user.UserName}"
        });

        await NotifyAccountLockedAsync(user);

        await _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "User",
            Action = "Deactivated",
            Message = $"User {user.FullName} locked"
        });

        return new UserOperationResultDto { Success = true };
    }

    private Task NotifyAccountLockedAsync(ApplicationUser user) =>
        _userNotificationService.NotifyAccountChangedAsync(new UserRealtimeEventDto
        {
            Action = "Locked",
            UserId = user.Id,
            Message = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên."
        });

    private Task NotifySessionInvalidatedAsync(ApplicationUser user, string message) =>
        _userNotificationService.NotifyAccountChangedAsync(new UserRealtimeEventDto
        {
            Action = "SessionInvalidated",
            UserId = user.Id,
            Message = message
        });

    private async Task<string> BuildEmailConfirmationUrlAsync(ApplicationUser user)
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var baseUrl = ResolveAppBaseUrl();
        return $"{baseUrl}/Account/ConfirmEmail?userId={user.Id}&code={encodedToken}";
    }

    private string ResolveAppBaseUrl()
    {
        // appsettings: "AppSettings:AppBaseUrl" → domain gốc dùng dựng link xác thực email / đăng nhập.
        if (!string.IsNullOrWhiteSpace(_appSettings.AppBaseUrl))
            return _appSettings.AppBaseUrl.TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(_appSettings.LoginUrl) &&
            Uri.TryCreate(_appSettings.LoginUrl, UriKind.Absolute, out var loginUri))
            return $"{loginUri.Scheme}://{loginUri.Authority}";

        return "https://localhost:7014";
    }

    private string ResolveLoginUrl()
    {
        if (!string.IsNullOrWhiteSpace(_appSettings.LoginUrl))
            return _appSettings.LoginUrl;

        return $"{ResolveAppBaseUrl()}/Account/Login";
    }

    private static UserOperationResultDto UserFail(string message) =>
        new() { Success = false, ErrorMessage = message };

    private async Task<bool> IsAdminAccountAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return roles.Contains(Roles.Admin);
    }

    private async Task<IReadOnlyList<UserDto>> MapUsersAsync(IReadOnlyList<ApplicationUser> users)
    {
        var result = new List<UserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                Role = roles.FirstOrDefault() ?? string.Empty,
                IsActive = user.IsActive,
                EmailConfirmed = user.EmailConfirmed,
                CreatedAt = user.CreatedAt
            });
        }
        return result;
    }
}
