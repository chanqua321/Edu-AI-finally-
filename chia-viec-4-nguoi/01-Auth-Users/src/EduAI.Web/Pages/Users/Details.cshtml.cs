using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Users;

[Authorize(Policy = "AdminOnly")]
public class DetailsModel : PageModel
{
    private readonly IUserManagementService _userService;
    private readonly ISubjectService _subjectService;

    public DetailsModel(IUserManagementService userService, ISubjectService subjectService)
    {
        _userService = userService;
        _subjectService = subjectService;
    }

    public UserDetailsViewModel ViewModel { get; set; } = new();
    public bool PasswordReset { get; set; }
    public bool ConfirmationResent { get; set; }
    public bool ConfirmationResendFailed { get; set; }
    public bool AccountEmailResent { get; set; }
    public bool AccountEmailResendFailed { get; set; }
    public bool LockFailed { get; set; }

    public async Task<IActionResult> OnGetAsync(
        string id,
        string? tempPassword,
        bool? emailSent,
        bool? passwordReset,
        bool? confirmationResent,
        bool? confirmationResendFailed,
        bool? accountEmailResent,
        bool? accountEmailResendFailed,
        bool? lockFailed)
    {
        ViewModel.User = await _userService.GetUserByIdAsync(id);
        if (ViewModel.User == null)
            return NotFound();

        if (ViewModel.User.Role == Roles.Teacher)
            ViewModel.AssignedSubjects = await _subjectService.GetAllAsync(id, Roles.Teacher);

        ViewModel.TemporaryPassword = tempPassword;
        ViewModel.EmailSent = emailSent ?? false;
        PasswordReset = passwordReset ?? false;
        ConfirmationResent = confirmationResent ?? false;
        ConfirmationResendFailed = confirmationResendFailed ?? false;
        AccountEmailResent = accountEmailResent ?? false;
        AccountEmailResendFailed = accountEmailResendFailed ?? false;
        LockFailed = lockFailed ?? false;
        return Page();
    }

    public async Task<IActionResult> OnPostResendConfirmationAsync(string id)
    {
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _userService.ResendTeacherEmailConfirmationAsync(
            id, adminId, IpAddressHelper.GetClientIp(HttpContext));

        return RedirectToPage(new
        {
            id,
            confirmationResent = result.Success,
            confirmationResendFailed = !result.Success,
            tempPassword = result.TemporaryPassword
        });
    }

    public async Task<IActionResult> OnPostResendAccountEmailAsync(string id)
    {
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _userService.ResendAccountEmailAsync(id, adminId, IpAddressHelper.GetClientIp(HttpContext));

        return RedirectToPage(new
        {
            id,
            accountEmailResent = result.Success,
            accountEmailResendFailed = !result.Success,
            tempPassword = result.TemporaryPassword,
            emailSent = result.EmailSent
        });
    }

    public async Task<IActionResult> OnPostLockAsync(string id)
    {
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _userService.DeleteUserAsync(id, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
            return RedirectToPage(new { id, lockFailed = true });

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUnlockAsync(string id)
    {
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var unlocked = await _userService.ActivateAccountAsync(id, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (!unlocked)
            return RedirectToPage(new { id, lockFailed = true });

        return RedirectToPage(new { id });
    }
}
