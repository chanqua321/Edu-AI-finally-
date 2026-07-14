using EduAI.Model.Constants;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Documents;

[Authorize(Policy = "AdminOrTeacher")]
public class DetailsModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly IChunkService _chunkService;
    private readonly ISubjectService _subjectService;

    public DetailsModel(
        IDocumentService documentService,
        IChunkService chunkService,
        ISubjectService subjectService)
    {
        _documentService = documentService;
        _chunkService = chunkService;
        _subjectService = subjectService;
    }

    public DocumentDetailsViewModel ViewModel { get; set; } = new();
    public bool CanManageDocuments { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, bool showChunks = false, int? chunkId = null)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        ViewModel.IsAdmin = role == Roles.Admin;
        ViewModel.ShowChunks = showChunks || chunkId.HasValue;
        ViewModel.SelectedChunkId = chunkId;
        ViewModel.Document = await _documentService.GetDetailsByIdAsync(id, userId, role);
        if (ViewModel.Document == null)
            return NotFound();

        CanManageDocuments = role == Roles.Teacher &&
            await _subjectService.IsTeacherAssignedToSubjectAsync(userId, ViewModel.Document.SubjectId);

        return Page();
    }

    public async Task<IActionResult> OnGetChunksAsync(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var doc = await _documentService.GetByIdAsync(id, userId, role);
        if (doc == null) return NotFound();

        var chunks = await _chunkService.GetByDocumentAsync(id, userId, role);
        return new JsonResult(new
        {
            count = chunks.Count,
            chunks = chunks.Select(c => new
            {
                c.Id,
                c.ChunkIndex,
                c.Content,
                c.HasEmbedding
            })
        });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!User.IsInRole(Roles.Teacher))
            return Forbid();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = Roles.Teacher;

        var doc = await _documentService.GetByIdAsync(id, userId, role);
        if (doc == null)
            return NotFound();

        await _documentService.DeleteAsync(id, userId, role, IpAddressHelper.GetClientIp(HttpContext));
        return RedirectToPage("Index", new { subjectId = doc.SubjectId });
    }
}
