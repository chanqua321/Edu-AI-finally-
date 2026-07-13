using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogService _auditLogService;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditLogService auditLogService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditLogService = auditLogService;
    }

    [BindProperty]
    public LoginViewModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public bool ShowResendLink { get; set; }
    public string? ResendEmail { get; set; }
    public bool AccountLocked { get; set; }

    public void OnGet(bool locked = false)
    {
        AccountLocked = locked;
        if (locked)
            ErrorMessage = "Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var loginId = Input.UserName.Trim();
        var user = await _userManager.FindByEmailAsync(loginId)
            ?? await _userManager.FindByNameAsync(loginId);
        if (user == null || !user.IsActive)
        {
            ErrorMessage = "Email hoặc mật khẩu không đúng, hoặc tài khoản đã bị khóa.";
            return Page();
        }

        var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, Input.Password, lockoutOnFailure: false);
        if (!passwordCheck.Succeeded)
        {
            ErrorMessage = "Email hoặc mật khẩu không đúng.";
            return Page();
        }

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(Roles.Teacher) && !user.EmailConfirmed)
        {
            ErrorMessage = "Tài khoản giáo viên chưa xác thực email. Kiểm tra hộp thư và bấm link xác thực trước khi đăng nhập.";
            ShowResendLink = true;
            ResendEmail = user.Email;
            return Page();
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = user.Id,
            Action = AuditActions.Login,
            IpAddress = IpAddressHelper.GetClientIp(HttpContext),
            Details = $"User {user.UserName} logged in"
        });

        if (user.MustChangePassword)
            return RedirectToPage("/Account/Profile", new { section = "password", force = "1" });

        if (roles.Contains(Roles.Admin))
            return RedirectToPage("/Users/Index");
        if (roles.Contains(Roles.Teacher))
            return RedirectToPage("/Subjects/Index");
        return RedirectToPage("/Chat/Index");
    }
}
