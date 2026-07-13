using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Chapters;

[Authorize(Policy = "AdminOrTeacher")]
public class DetailsModel : PageModel
{
    private readonly IChapterService _chapterService;
    private readonly ILessonService _lessonService;

    public DetailsModel(IChapterService chapterService, ILessonService lessonService)
    {
        _chapterService = chapterService;
        _lessonService = lessonService;
    }

    public ChapterDetailsViewModel ViewModel { get; set; } = new();
    public IReadOnlyList<LessonDto> Lessons { get; set; } = Array.Empty<LessonDto>();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        ViewModel.Chapter = await _chapterService.GetByIdAsync(id, userId, role);
        if (ViewModel.Chapter == null) return NotFound();
        Lessons = await _lessonService.GetByChapterAsync(id, userId, role);
        return Page();
    }
}
