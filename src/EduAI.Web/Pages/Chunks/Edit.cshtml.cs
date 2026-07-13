using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Chunks;

[Authorize(Policy = "TeacherOnly")]
public class EditModel : PageModel
{
    private readonly IChunkService _chunkService;

    public EditModel(IChunkService chunkService)
    {
        _chunkService = chunkService;
    }

    [BindProperty]
    public ChunkFormViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var chunk = await _chunkService.GetByIdAsync(id, userId, role);
        if (chunk == null) return NotFound();

        Input = new ChunkFormViewModel
        {
            Id = chunk.Id,
            SubjectId = chunk.SubjectId,
            SubjectName = chunk.SubjectName,
            ChapterId = chunk.ChapterId,
            DocumentId = chunk.DocumentId,
            ChunkIndex = chunk.ChunkIndex,
            Content = chunk.Content
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var result = await _chunkService.UpdateAsync(new UpdateChunkDto
        {
            Id = Input.Id,
            Content = Input.Content
        }, userId, role);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Cannot update chunk.");
            return Page();
        }

        return RedirectToPage("Details", new { id = Input.Id });
    }
}
