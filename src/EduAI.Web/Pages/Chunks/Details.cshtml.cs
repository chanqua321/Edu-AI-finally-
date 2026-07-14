using EduAI.Model.Constants;

using EduAI.BusinessLogic.IService;

using EduAI.Model.ViewModels;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Mvc.RazorPages;



namespace EduAI.Web.Pages.Chunks;



[Authorize(Policy = "AdminOrTeacher")]

public class DetailsModel : PageModel

{

    private readonly IChunkService _chunkService;



    public DetailsModel(IChunkService chunkService)

    {

        _chunkService = chunkService;

    }



    public ChunkDetailsViewModel ViewModel { get; set; } = new();



    public async Task<IActionResult> OnGetAsync(int id)

    {

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;



        ViewModel.Chunk = await _chunkService.GetByIdAsync(id, userId, role);

        if (ViewModel.Chunk == null)

            return NotFound();



        return Page();

    }



    public async Task<IActionResult> OnPostDeleteAsync(int id)

    {

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;



        var chunk = await _chunkService.GetByIdAsync(id, userId, role);

        if (chunk == null) return NotFound();



        await _chunkService.DeleteAsync(id, userId, role);

        return RedirectToPage("Index", new { subjectId = chunk.SubjectId });

    }

}

