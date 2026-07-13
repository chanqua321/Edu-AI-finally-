using EduAI.Model.Constants;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Users;

[Authorize(Policy = "AdminOnly")]
public class EditModel : PageModel
{
    private readonly IUserManagementService _userService;

    public EditModel(IUserManagementService userService)
    {
        _userService = userService;
    }

    [BindProperty]
    public UserEditViewModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? ActiveSection { get; set; }

    public async Task<IActionResult> OnGetAsync(string id, string? section)
    {
        ActiveSection = section;
        return await LoadPageAsync(id);
    }

    public async Task<IActionResult> OnPostSaveProfileAsync(string id)
    {
        ActiveSection = "profile";
        if (!ModelState.IsValid)
        {
            await LoadPageAsync(id);
            return Page();
        }

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _userService.UpdateUserAsync(new Model.DTOs.UpdateUserDto
        {
            Id = id,
            FullName = Input.FullName,
            Email = Input.Email
        }, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage ?? "Cập nhật thất bại.";
            await LoadPageAsync(id);
            return Page();
        }

        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostActivateAsync(string id)
    {
        ModelState.Clear();
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var success = await _userService.ActivateAccountAsync(id, adminId, IpAddressHelper.GetClientIp(HttpContext));
        if (!success)
        {
            ErrorMessage = "Không thể mở khóa tài khoản.";
            await LoadPageAsync(id);
            return Page();
        }

        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostDeactivateAsync(string id)
    {
        ModelState.Clear();
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _userService.DeleteUserAsync(id, adminId, IpAddressHelper.GetClientIp(HttpContext));
        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage ?? "Không thể khóa tài khoản.";
            await LoadPageAsync(id);
            return Page();
        }

        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string id)
    {
        ModelState.Clear();
        ActiveSection = "password";
        await LoadPageAsync(id);

        if (string.IsNullOrWhiteSpace(Input.NewPassword))
        {
            ModelState.AddModelError(nameof(Input.NewPassword), "New password is required.");
            return Page();
        }

        if (Input.NewPassword != Input.ConfirmNewPassword)
        {
            ModelState.AddModelError(nameof(Input.ConfirmNewPassword), "Password and confirmation do not match.");
            return Page();
        }

        if (!ModelState.IsValid)
            return Page();

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var success = await _userService.ResetPasswordAsync(
            id, Input.NewPassword, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (!success)
        {
            ErrorMessage = "Failed to reset password. Check password policy.";
            return Page();
        }

        return RedirectToPage("Details", new { id, passwordReset = true });
    }

    private async Task<IActionResult> LoadPageAsync(string id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null) return NotFound();

        if (user.Role == Roles.Admin)
            return RedirectToPage("Details", new { id });

        Input = new UserEditViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            UserName = user.UserName,
            Role = user.Role,
            IsActive = user.IsActive
        };
        return Page();
    }
}
