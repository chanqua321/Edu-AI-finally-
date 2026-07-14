using EduAI.Model.Constants;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Chat;

[Authorize(Policy = "StudentOnly")]
public class IndexModel : PageModel
{
    private readonly IChatService _chatService;
    private readonly ISubjectService _subjectService;

    public IndexModel(IChatService chatService, ISubjectService subjectService)
    {
        _chatService = chatService;
        _subjectService = subjectService;
    }

    public ChatIndexViewModel ViewModel { get; set; } = new();

    public async Task OnGetAsync()
    {
        var studentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        ViewModel.Sessions = await _chatService.GetSessionsAsync(studentId);
        ViewModel.Subjects = await _subjectService.GetAllAsync(studentId, Roles.Student, studentIndexedOnly: true);
    }
}
