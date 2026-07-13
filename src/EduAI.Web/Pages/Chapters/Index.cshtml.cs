using EduAI.Model.Constants;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Chapters;

[Authorize(Policy = "AdminOrTeacher")]
public class IndexModel : PageModel
{
    private readonly IChapterService _chapterService;
    private readonly ISubjectService _subjectService;

    public IndexModel(IChapterService chapterService, ISubjectService subjectService)
    {
        _chapterService = chapterService;
        _subjectService = subjectService;
    }

    public ChapterIndexViewModel ViewModel { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int subjectId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var subject = await _subjectService.GetByIdAsync(subjectId, userId, role);
        if (subject == null) return NotFound();

        ViewModel.SubjectId = subjectId;
        ViewModel.SubjectName = subject.Name;
        ViewModel.Chapters = await _chapterService.GetBySubjectAsync(subjectId, userId, role);
        return Page();
    }
}
