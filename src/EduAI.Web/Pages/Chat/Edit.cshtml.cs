using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Chat;

[Authorize(Policy = "StudentOnly")]
public class EditModel : PageModel
{
    private readonly IChatService _chatService;

    public EditModel(IChatService chatService)
    {
        _chatService = chatService;
    }

    [BindProperty]
    public ChatEditSessionViewModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var studentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var session = await _chatService.GetSessionAsync(id, studentId);
        if (session == null)
            return NotFound();

        Input = new ChatEditSessionViewModel
        {
            SessionId = session.Id,
            SubjectName = session.SubjectName,
            Title = session.Title
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var studentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _chatService.UpdateSessionAsync(new UpdateChatSessionDto
        {
            SessionId = Input.SessionId,
            StudentId = studentId,
            Title = Input.Title
        }, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not update session.");
            return Page();
        }

        return RedirectToPage("Details", new { id = Input.SessionId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var studentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _chatService.DeleteSessionAsync(id, studentId, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            var session = await _chatService.GetSessionAsync(id, studentId);
            if (session == null)
                return NotFound();

            Input = new ChatEditSessionViewModel
            {
                SessionId = session.Id,
                SubjectName = session.SubjectName,
                Title = session.Title
            };
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not delete session.");
            return Page();
        }

        return RedirectToPage("Index");
    }
}
