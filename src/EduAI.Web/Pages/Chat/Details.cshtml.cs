using EduAI.BusinessLogic.IService;
using EduAI.Model.DTOs;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Chat;

[Authorize(Policy = "StudentOnly")]
public class DetailsModel : PageModel
{
    private readonly IChatService _chatService;

    public DetailsModel(IChatService chatService)
    {
        _chatService = chatService;
    }

    public ChatSessionViewModel ViewModel { get; set; } = new();

    [BindProperty]
    public string Question { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        return await LoadPageAsync(id);
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var studentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var session = await _chatService.GetSessionAsync(id, studentId);
        if (session == null) return NotFound();

        if (string.IsNullOrWhiteSpace(Question))
        {
            ErrorMessage = "Vui lòng nhập câu hỏi.";
            return await LoadPageAsync(id);
        }

        var response = await _chatService.SendMessageAsync(new SendChatMessageDto
        {
            SessionId = id,
            StudentId = studentId,
            SubjectId = session.SubjectId,
            Question = Question.Trim()
        }, IpAddressHelper.GetClientIp(HttpContext));

        if (!response.Success)
        {
            ErrorMessage = response.ErrorMessage ?? "Không thể gửi tin nhắn.";
            return await LoadPageAsync(id);
        }

        return RedirectToPage(new { id });
    }

    private async Task<IActionResult> LoadPageAsync(int id)
    {
        var studentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var session = await _chatService.GetSessionAsync(id, studentId);
        if (session == null) return NotFound();

        ViewModel.SessionId = session.Id;
        ViewModel.SubjectId = session.SubjectId;
        ViewModel.SubjectName = session.SubjectName;
        ViewModel.Title = session.Title;
        ViewModel.Messages = await _chatService.GetMessagesAsync(id, studentId);
        return Page();
    }
}
