using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Account;

[Authorize]
public class ProfileModel : PageModel
{
    private readonly IUserManagementService _userService;
    private readonly IPaymentService _paymentService;

    public ProfileModel(IUserManagementService userService, IPaymentService paymentService)
    {
        _userService = userService;
        _paymentService = paymentService;
    }

    [BindProperty]
    public UserProfileViewModel Input { get; set; } = new();

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ActiveSection { get; set; }
    public AiProviderQuotaOverviewDto ProviderQuotaOverview { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string? saved, string? passwordChanged, string? section, string? force)
    {
        ActiveSection = section;
        var loaded = await LoadProfileAsync();
        if (loaded != null) return loaded;

        if (force == "1")
        {
            ActiveSection = "password";
            ErrorMessage = "Bạn cần đổi mật khẩu ở lần đăng nhập đầu tiên để tiếp tục sử dụng hệ thống.";
        }

        if (saved == "1") SuccessMessage = "Đã cập nhật thông tin tài khoản.";
        if (passwordChanged == "1") SuccessMessage = "Đã đổi mật khẩu. Vui lòng đăng nhập lại.";
        return Page();
    }

    public async Task<IActionResult> OnPostSaveProfileAsync()
    {
        ActiveSection = "profile";
        var loaded = await LoadProfileForPostAsync(preserveProfileInputs: true);
        if (loaded != null) return loaded;

        if (Input.IsAdmin)
            return Page();

        if (!ModelState.IsValid)
            return Page();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _userService.UpdateOwnProfileAsync(new UpdateUserDto
        {
            Id = Input.Id,
            FullName = Input.FullName,
            Email = Input.Email
        }, userId, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage ?? "Không thể cập nhật.";
            return Page();
        }

        return RedirectToPage(new { saved = 1 });
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        ActiveSection = "password";
        // Keep the user's entered fields; do not overwrite on validation errors.
        var loaded = await LoadProfileForPostAsync(preserveProfileInputs: true);
        if (loaded != null) return loaded;

        if (Input.IsAdmin)
            return Page();

        if (string.IsNullOrWhiteSpace(Input.CurrentPassword) ||
            string.IsNullOrWhiteSpace(Input.NewPassword) ||
            string.IsNullOrWhiteSpace(Input.ConfirmNewPassword))
        {
            ErrorMessage = "Vui lòng nhập đầy đủ mật khẩu.";
            return Page();
        }

        if (Input.NewPassword != Input.ConfirmNewPassword)
        {
            ErrorMessage = "Mật khẩu xác nhận không khớp.";
            return Page();
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _userService.ChangeOwnPasswordAsync(
            userId, Input.CurrentPassword, Input.NewPassword);

        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage ?? "Không thể đổi mật khẩu.";
            // Clear only the password fields after a failure (keep other inputs intact).
            Input.CurrentPassword = string.Empty;
            Input.NewPassword = string.Empty;
            Input.ConfirmNewPassword = string.Empty;
            return Page();
        }

        return RedirectToPage("/Account/Logout");
    }

    private async Task<IActionResult?> LoadProfileAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null) return NotFound();

        Input.Id = user.Id;
        Input.FullName = user.FullName;
        Input.Email = user.Email;
        Input.Role = user.Role;
        Input.IsAdmin = user.Role == Roles.Admin;
        if (user.Role == Roles.Student)
            ProviderQuotaOverview = await _paymentService.GetAiProviderQuotaOverviewAsync(userId);

        return null;
    }

    private async Task<IActionResult?> LoadProfileForPostAsync(bool preserveProfileInputs)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null) return NotFound();

        // Always trust server-side identity for immutable fields.
        Input.Id = user.Id;
        Input.Role = user.Role;
        Input.IsAdmin = user.Role == Roles.Admin;
        if (user.Role == Roles.Student)
            ProviderQuotaOverview = await _paymentService.GetAiProviderQuotaOverviewAsync(userId);

        // Preserve what user typed on POST when validation fails.
        if (!preserveProfileInputs)
        {
            Input.FullName = user.FullName;
            Input.Email = user.Email;
        }

        return null;
    }
}
