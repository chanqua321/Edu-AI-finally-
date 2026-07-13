using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Chapters;

[Authorize(Policy = "TeacherOnly")]
public class CreateModel : PageModel
{
    private readonly IChapterService _chapterService;
    private readonly ISubjectService _subjectService;

    public CreateModel(IChapterService chapterService, ISubjectService subjectService)
    {
        _chapterService = chapterService;
        _subjectService = subjectService;
    }

    [BindProperty]
    public ChapterFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int subjectId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;
        var subject = await _subjectService.GetByIdAsync(subjectId, userId, role);
        if (subject == null) return NotFound();

        Input.SubjectId = subjectId;
        Input.SubjectName = subject.Name;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var chapter = await _chapterService.CreateAsync(new CreateChapterDto
        {
            SubjectId = Input.SubjectId,
            Name = Input.Name,
            OrderNumber = Input.OrderNumber
        }, userId, role);

        return RedirectToPage("Details", new { id = chapter.Id });
    }
}
