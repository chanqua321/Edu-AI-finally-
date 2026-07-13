using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Enums;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduAI.Web.Pages.Documents;

[Authorize(Policy = "TeacherOnly")]
public class EditModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly IChapterService _chapterService;
    private readonly ILessonService _lessonService;

    public EditModel(IDocumentService documentService, IChapterService chapterService, ILessonService lessonService)
    {
        _documentService = documentService;
        _chapterService = chapterService;
        _lessonService = lessonService;
    }

    [BindProperty]
    public DocumentEditViewModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? ReplacementFile { get; set; }

    public SelectList LessonOptions { get; set; } = null!;
    public SelectList CategoryOptions { get; set; } = null!;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var doc = await _documentService.GetByIdAsync(id, userId, role);
        if (doc == null) return NotFound();

        Input = new DocumentEditViewModel
        {
            Id = doc.Id,
            SubjectId = doc.SubjectId,
            SubjectName = doc.SubjectName,
            FileName = doc.FileName,
            ChapterId = doc.ChapterId,
            LessonId = doc.LessonId,
            Category = doc.Category
        };

        await LoadOptionsAsync(userId, role);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync(userId, role);
            return Page();
        }

        var updateDto = new UpdateDocumentDto
        {
            Id = Input.Id,
            FileName = Input.FileName,
            ChapterId = Input.ChapterId,
            LessonId = Input.LessonId,
            Category = Input.Category
        };

        if (ReplacementFile is { Length: > 0 })
        {
            await using var uploadStream = ReplacementFile.OpenReadStream();
            updateDto.NewFileStream = uploadStream;
            updateDto.NewFileOriginalName = ReplacementFile.FileName;
            updateDto.NewFileSizeBytes = ReplacementFile.Length;

            var result = await _documentService.UpdateAsync(updateDto, userId, role, IpAddressHelper.GetClientIp(HttpContext));
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Cập nhật thất bại.");
                await LoadOptionsAsync(userId, role);
                return Page();
            }

            return RedirectToPage("Details", new { id = Input.Id, showChunks = true });
        }

        var updateResult = await _documentService.UpdateAsync(updateDto, userId, role, IpAddressHelper.GetClientIp(HttpContext));
        if (!updateResult.Success)
        {
            ModelState.AddModelError(string.Empty, updateResult.ErrorMessage ?? "Cập nhật thất bại.");
            await LoadOptionsAsync(userId, role);
            return Page();
        }

        return RedirectToPage("Details", new { id = Input.Id });
    }

    private async Task LoadOptionsAsync(string userId, string role)
    {
        var lessons = await _lessonService.GetBySubjectAsync(Input.SubjectId, userId, role);
        LessonOptions = new SelectList(
            lessons.Select(l => new { l.Id, Label = $"{l.ChapterName} / {l.Name}" }),
            "Id", "Label", Input.LessonId);
        CategoryOptions = new SelectList(
            Enum.GetValues<DocumentCategory>().Select(c => new { Value = (int)c, Text = c.ToVietnameseLabel() }),
            "Value", "Text", (int)Input.Category);
    }
}
