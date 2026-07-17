using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduAI.Web.Pages.Users;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly IUserManagementService _userService;

    public CreateModel(IUserManagementService userService)
    {
        _userService = userService;
    }

    [BindProperty]
    public UserCreateViewModel Input { get; set; } = new();

    public SelectList RoleOptions { get; set; } = null!;

    public void OnGet()
    {
        RoleOptions = new SelectList(new[] { Roles.Teacher, Roles.Student });
    }

    public async Task<IActionResult> OnPostAsync()
    {
        RoleOptions = new SelectList(new[] { Roles.Teacher, Roles.Student });
        if (!ModelState.IsValid)
            return Page();

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _userService.CreateUserAsync(new CreateUserDto
        {
            FullName = Input.FullName,
            Email = Input.Email,
            UserName = Input.Email.Trim(),
            Role = Input.Role,
            Password = Input.Password
        }, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to create user.");
            return Page();
        }

        return RedirectToPage("Details", new { id = result.UserId, tempPassword = result.TemporaryPassword, emailSent = result.EmailSent });
    }
}
