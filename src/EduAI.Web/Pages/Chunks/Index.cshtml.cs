using EduAI.Model.Constants;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Chunks;

[Authorize(Policy = "AdminOrTeacher")]
public class IndexModel : PageModel
{
    private readonly IChunkService _chunkService;
    private readonly ISubjectService _subjectService;

    public IndexModel(IChunkService chunkService, ISubjectService subjectService)
    {
        _chunkService = chunkService;
        _subjectService = subjectService;
    }

    public ChunkIndexViewModel ViewModel { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? SubjectId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        ViewModel.Subjects = await _subjectService.GetAllAsync(userId, role);
        ViewModel.SubjectId = SubjectId;
        ViewModel.Keyword = Keyword;

        if (SubjectId.HasValue)
        {
            ViewModel.Chunks = await _chunkService.GetBySubjectAsync(
                SubjectId.Value, userId ?? string.Empty, role, Keyword);
        }
    }
}
