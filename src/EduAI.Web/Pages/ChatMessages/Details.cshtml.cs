using EduAI.Model.Constants;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.ChatMessages;

[Authorize(Policy = "AdminOnly")]
public class DetailsModel : PageModel
{
    private readonly IChatService _chatService;

    public DetailsModel(IChatService chatService)
    {
        _chatService = chatService;
    }

    public ChatMessageFormViewModel ViewModel { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? SessionId { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var message = await _chatService.GetMessageByIdAsync(id, userId, Roles.Admin);
        if (message == null) return NotFound();

        ViewModel = new ChatMessageFormViewModel
        {
            Id = message.Id,
            SessionId = message.SessionId,
            Role = message.Role,
            Content = message.Content,
            Citations = message.Citations
        };

        SessionId ??= message.SessionId;

        return Page();
    }
}
