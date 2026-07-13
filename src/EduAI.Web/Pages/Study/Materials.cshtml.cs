using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Study;

[Authorize(Policy = "StudentOnly")]
public class MaterialsModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly ISubjectService _subjectService;
    private readonly IChapterService _chapterService;
    private readonly ILessonService _lessonService;

    public MaterialsModel(
        IDocumentService documentService,
        ISubjectService subjectService,
        IChapterService chapterService,
        ILessonService lessonService)
    {
        _documentService = documentService;
        _subjectService = subjectService;
        _chapterService = chapterService;
        _lessonService = lessonService;
    }

    public DocumentIndexViewModel ViewModel { get; set; } = new();
    public IReadOnlyList<ChapterDto> Chapters { get; set; } = Array.Empty<ChapterDto>();
    public IReadOnlyList<LessonDto> Lessons { get; set; } = Array.Empty<LessonDto>();

    public async Task<IActionResult> OnGetAsync(int subjectId)
    {
        var studentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var subject = await _subjectService.GetByIdAsync(subjectId, studentId, Roles.Student);
        if (subject == null)
            return NotFound();

        ViewModel.SubjectId = subjectId;
        ViewModel.SubjectName = subject.Name;
        ViewModel.Documents = await _documentService.GetBySubjectAsync(subjectId, studentId, Roles.Student);
        Chapters = await _chapterService.GetBySubjectAsync(subjectId, studentId, Roles.Student);
        Lessons = await _lessonService.GetBySubjectAsync(subjectId, studentId, Roles.Student);
        return Page();
    }
}
