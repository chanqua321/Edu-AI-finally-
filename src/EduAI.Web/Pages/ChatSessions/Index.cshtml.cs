using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.ChatSessions;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IChatService _chatService;

    public IndexModel(IChatService chatService)
    {
        _chatService = chatService;
    }

    public ChatSessionAdminIndexViewModel ViewModel { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? SessionId { get; set; }

    public ChatSessionDto? SelectedSession { get; set; }

    public IReadOnlyList<ChatMessageDto> Messages { get; set; } = Array.Empty<ChatMessageDto>();

    public async Task OnGetAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        ViewModel.Sessions = await _chatService.GetAllSessionsAsync(userId, Roles.Admin);

        if (SessionId.HasValue)
        {
            SelectedSession = ViewModel.Sessions.FirstOrDefault(s => s.Id == SessionId.Value)
                ?? await _chatService.GetSessionForAdminAsync(SessionId.Value);
            Messages = await _chatService.GetAllMessagesAsync(SessionId, userId, Roles.Admin);
        }
    }
}
