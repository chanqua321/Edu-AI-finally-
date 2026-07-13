using System.Text;
using EduAI.Model.Entities;
using EduAI.Model.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace EduAI.Web.Pages.Account;

[AllowAnonymous]
public class ConfirmEmailModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ConfirmEmailModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public bool Succeeded { get; set; }
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync(string? userId, string? code)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
        {
            Message = "Liên kết xác thực không hợp lệ.";
            return Page();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            Message = "Không tìm thấy tài khoản.";
            return Page();
        }

        if (user.EmailConfirmed)
        {
            Succeeded = true;
            Message = "Email đã được xác thực trước đó. Bạn có thể đăng nhập.";
            return Page();
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, decoded);
            if (result.Succeeded)
            {
                Succeeded = true;
                Message = "Xác thực email thành công!";

                // Redirect by role after confirmation:
                // - Student -> Home
                // - Teacher -> Upload
                // - Admin -> Users
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains(Roles.Student))
                    return RedirectToPage("/Index");
                if (roles.Contains(Roles.Teacher))
                    return RedirectToPage("/Documents/Create");
                if (roles.Contains(Roles.Admin))
                    return RedirectToPage("/Users/Index");

                return RedirectToPage("/Index");
            }
            else
            {
                Message = "Liên kết xác thực không hợp lệ hoặc đã hết hạn.";
            }
        }
        catch
        {
            Message = "Liên kết xác thực không hợp lệ.";
        }

        return Page();
    }
}
