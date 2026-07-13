using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EduAI.Model.Entities;

namespace EduAI.Web.Pages.Account;

[Authorize]
public class LogoutModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IAuditLogService _auditLogService;

    public LogoutModel(SignInManager<ApplicationUser> signInManager, IAuditLogService auditLogService)
    {
        _signInManager = signInManager;
        _auditLogService = auditLogService;
    }

    public Task<IActionResult> OnPostAsync() => SignOutAndRedirectAsync(locked: false);

    public Task<IActionResult> OnGetForceAsync() => SignOutAndRedirectAsync(locked: true);

    private async Task<IActionResult> SignOutAndRedirectAsync(bool locked)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = _signInManager.UserManager.GetUserId(User);
            await _signInManager.SignOutAsync();

            await _auditLogService.LogAsync(new CreateAuditLogDto
            {
                UserId = userId,
                Action = AuditActions.Logout,
                IpAddress = IpAddressHelper.GetClientIp(HttpContext),
                Details = locked ? "Forced logout (account locked)" : "User logged out"
            });
        }

        return locked
            ? RedirectToPage("/Account/Login", new { locked = 1 })
            : RedirectToPage("/Account/Login");
    }
}
