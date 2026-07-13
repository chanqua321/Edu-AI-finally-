using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Documents;

[Authorize(Policy = "AdminOrTeacher")]
public class IndexModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly ISubjectService _subjectService;
    private readonly IChunkService _chunkService;

    public IndexModel(
        IDocumentService documentService,
        ISubjectService subjectService,
        IChunkService chunkService)
    {
        _documentService = documentService;
        _subjectService = subjectService;
        _chunkService = chunkService;
    }

    public DocumentIndexViewModel ViewModel { get; set; } = new();
    public int? ExpandedDocumentId { get; set; }
    public IReadOnlyList<ChunkDto> ExpandedChunks { get; set; } = Array.Empty<ChunkDto>();
    public bool ShowEmbedding { get; set; }

    public bool CanUpload { get; set; }

    public async Task<IActionResult> OnGetAsync(int subjectId, int? showChunks)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;
        ShowEmbedding = role == Roles.Admin;

        var subject = await _subjectService.GetByIdAsync(subjectId, userId, role);
        if (subject == null) return NotFound();

        CanUpload = role == Roles.Teacher &&
            await _subjectService.IsTeacherAssignedToSubjectAsync(userId, subjectId);

        ViewModel.SubjectId = subjectId;
        ViewModel.SubjectName = subject.Name;
        ViewModel.Documents = await _documentService.GetBySubjectAsync(subjectId, userId, role);

        if (showChunks.HasValue)
        {
            ExpandedDocumentId = showChunks;
            ExpandedChunks = await _chunkService.GetByDocumentAsync(showChunks.Value, userId, role);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostReindexAsync(int documentId)
    {
        if (!User.IsInRole(Roles.Teacher))
            return Forbid();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = Roles.Teacher;

        var doc = await _documentService.GetByIdAsync(documentId, userId, role);
        if (doc == null) return NotFound();

        var result = await _documentService.ReindexAsync(documentId, userId, role, IpAddressHelper.GetClientIp(HttpContext));
        if (!result.Success)
            TempData["Error"] = result.ErrorMessage;

        return RedirectToPage("Index", new { subjectId = doc.SubjectId });
    }
}
