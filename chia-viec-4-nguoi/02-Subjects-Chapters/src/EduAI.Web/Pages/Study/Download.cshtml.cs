using EduAI.Model.Constants;
using EduAI.BusinessLogic.IService;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Study;

[Authorize(Policy = "AuthenticatedUser")]
public class DownloadModel : PageModel
{
    private readonly IDocumentService _documentService;

    public DownloadModel(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin
            : User.IsInRole(Roles.Teacher) ? Roles.Teacher
            : Roles.Student;

        var result = await _documentService.GetDownloadFileAsync(
            id, userId, role, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
            return NotFound();

        return PhysicalFile(result.FilePath, result.ContentType, result.FileName);
    }
}
