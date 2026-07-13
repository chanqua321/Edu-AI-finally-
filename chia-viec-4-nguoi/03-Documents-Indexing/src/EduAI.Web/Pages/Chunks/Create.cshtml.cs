using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduAI.Web.Pages.Chunks;

[Authorize(Policy = "TeacherOnly")]
public class CreateModel : PageModel
{
    private readonly IChunkService _chunkService;
    private readonly ISubjectService _subjectService;
    private readonly IChapterService _chapterService;
    private readonly IDocumentService _documentService;

    public CreateModel(
        IChunkService chunkService,
        ISubjectService subjectService,
        IChapterService chapterService,
        IDocumentService documentService)
    {
        _chunkService = chunkService;
        _subjectService = subjectService;
        _chapterService = chapterService;
        _documentService = documentService;
    }

    [BindProperty]
    public ChunkFormViewModel Input { get; set; } = new();

    public SelectList ChapterOptions { get; set; } = null!;
    public SelectList DocumentOptions { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int subjectId, int? chapterId, int? documentId)
    {
        if (!await LoadFormAsync(subjectId, chapterId, documentId))
            return NotFound();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid || !await LoadFormAsync(Input.SubjectId, Input.ChapterId, Input.DocumentId))
            return Page();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var result = await _chunkService.CreateAsync(new CreateChunkDto
        {
            SubjectId = Input.SubjectId,
            ChapterId = Input.ChapterId,
            DocumentId = Input.DocumentId,
            Content = Input.Content,
            ChunkIndex = Input.ChunkIndex > 0 ? Input.ChunkIndex : null
        }, userId, role);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Cannot create chunk.");
            return Page();
        }

        return RedirectToPage("Details", new { id = result.Chunk!.Id });
    }

    private async Task<bool> LoadFormAsync(int subjectId, int? chapterId, int? documentId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var subject = await _subjectService.GetByIdAsync(subjectId, userId, role);
        if (subject == null) return false;

        Input.SubjectId = subjectId;
        Input.SubjectName = subject.Name;

        var chapters = await _chapterService.GetBySubjectAsync(subjectId, userId, role);
        Input.ChapterId = chapterId ?? chapters.FirstOrDefault()?.Id ?? 0;

        ChapterOptions = new SelectList(chapters, "Id", "Name", Input.ChapterId);

        var documents = Input.ChapterId > 0
            ? await _documentService.GetBySubjectAsync(subjectId, userId, role)
            : Array.Empty<DocumentDto>();

        var chapterDocuments = documents.Where(d => d.ChapterId == Input.ChapterId).ToList();
        Input.DocumentId = documentId ?? chapterDocuments.FirstOrDefault()?.Id ?? 0;
        DocumentOptions = new SelectList(chapterDocuments, "Id", "FileName", Input.DocumentId);

        return true;
    }
}
