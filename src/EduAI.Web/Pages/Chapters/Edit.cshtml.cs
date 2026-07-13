using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Chapters;

[Authorize(Policy = "TeacherOnly")]
public class EditModel : PageModel
{
    private readonly IChapterService _chapterService;

    public EditModel(IChapterService chapterService)
    {
        _chapterService = chapterService;
    }

    [BindProperty]
    public ChapterFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var chapter = await _chapterService.GetByIdAsync(id, userId, role);
        if (chapter == null) return NotFound();

        Input = new ChapterFormViewModel
        {
            Id = chapter.Id,
            SubjectId = chapter.SubjectId,
            SubjectName = chapter.SubjectName,
            Name = chapter.Name,
            OrderNumber = chapter.OrderNumber
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        await _chapterService.UpdateAsync(new UpdateChapterDto
        {
            Id = Input.Id,
            Name = Input.Name,
            OrderNumber = Input.OrderNumber
        }, userId, role);

        return RedirectToPage("Details", new { id = Input.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;
        await _chapterService.DeleteAsync(Input.Id, userId, role, IpAddressHelper.GetClientIp(HttpContext));
        return RedirectToPage("Index", new { subjectId = Input.SubjectId });
    }
}
