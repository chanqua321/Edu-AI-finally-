using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Users;

[Authorize(Policy = "AdminOnly")]
public class ImportModel : PageModel
{
    private readonly IUserManagementService _userService;

    public ImportModel(IUserManagementService userService)
    {
        _userService = userService;
    }

    [BindProperty]
    public IFormFile? UploadFile { get; set; }

    public BulkUserImportResultDto? ImportResult { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (UploadFile == null || UploadFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng chọn file Excel.");
            return Page();
        }

        var extension = Path.GetExtension(UploadFile.FileName).ToLowerInvariant();
        if (extension != ".xlsx" && extension != ".xls")
        {
            ModelState.AddModelError(string.Empty, "Chỉ hỗ trợ file Excel (.xlsx, .xls).");
            return Page();
        }

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        await using var stream = UploadFile.OpenReadStream();
        ImportResult = await _userService.BulkImportUsersAsync(stream, adminId, IpAddressHelper.GetClientIp(HttpContext));

        return Page();
    }
}
