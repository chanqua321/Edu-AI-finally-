using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Account;

[AllowAnonymous]
public class ResendEmailConfirmationModel : PageModel
{
    private readonly IUserManagementService _userService;

    public ResendEmailConfirmationModel(IUserManagementService userService)
    {
        _userService = userService;
    }

    [BindProperty]
    public ResendEmailConfirmationViewModel Input { get; set; } = new();

    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet(string? email)
    {
        if (!string.IsNullOrWhiteSpace(email))
            Input.Email = email;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var sent = await _userService.ResendTeacherEmailConfirmationByEmailAsync(Input.Email);
        if (sent)
        {
            StatusMessage = "Đã gửi lại email xác thực. Vui lòng kiểm tra hộp thư (cả thư rác).";
            return Page();
        }

        ErrorMessage = "Không gửi được email. Kiểm tra địa chỉ email giáo viên hoặc tài khoản đã xác thực.";
        return Page();
    }
}
