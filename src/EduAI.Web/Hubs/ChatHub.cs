using System.Security.Claims;
using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace EduAI.Web.Hubs;

[Authorize(Roles = Roles.Student)]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ChatHub(IChatService chatService, UserManager<ApplicationUser> userManager)
    {
        _chatService = chatService;
        _userManager = userManager;
    }

    public async Task JoinSession(int sessionId)
    {
        var studentId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(studentId))
            throw new HubException("Chưa đăng nhập.");

        if (await MustChangePasswordAsync())
            throw new HubException("Bạn cần đổi mật khẩu trước khi sử dụng chat.");

        var session = await _chatService.GetSessionAsync(sessionId, studentId);
        if (session == null)
            throw new HubException("Phiên chat không hợp lệ.");

        await Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(sessionId));
    }

    public async Task SendQuestion(int sessionId, int subjectId, string question, string? providerId = null, int? documentId = null)
    {
        var studentId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(studentId))
        {
            await Clients.Caller.SendAsync("ReceiveError", new { message = "Chưa đăng nhập." });
            return;
        }

        if (await MustChangePasswordAsync())
        {
            await Clients.Caller.SendAsync("ReceiveError", new { message = "Bạn cần đổi mật khẩu trước khi sử dụng chat." });
            return;
        }

        if (string.IsNullOrWhiteSpace(question))
        {
            await Clients.Caller.SendAsync("ReceiveError", new { message = "Vui lòng nhập câu hỏi." });
            return;
        }

        var session = await _chatService.GetSessionAsync(sessionId, studentId);
        if (session == null || session.SubjectId != subjectId)
        {
            await Clients.Caller.SendAsync("ReceiveError", new { message = "Phiên chat không hợp lệ." });
            return;
        }

        await Clients.Caller.SendAsync("ReceiveUserMessage", new
        {
            role = "user",
            content = question.Trim()
        });

        var ip = Context.GetHttpContext()?.Connection.RemoteIpAddress?.ToString();
        var response = await _chatService.SendMessageAsync(new SendChatMessageDto
        {
            SessionId = sessionId,
            StudentId = studentId,
            SubjectId = subjectId,
            Question = question.Trim(),
            ProviderId = providerId,
            DocumentId = documentId
        }, ip);

        if (!response.Success)
        {
            await Clients.Caller.SendAsync("ReceiveError", new
            {
                message = response.ErrorMessage ?? "Không thể gửi tin nhắn.",
                quota = response.Quota,
                fallbackProviderId = response.FallbackProviderId,
                fallbackProviderName = response.FallbackProviderName,
                fallbackQuota = response.FallbackQuota
            });
            return;
        }

        await Clients.Caller.SendAsync("ReceiveAssistantMessage", new
        {
            role = "assistant",
            content = response.Answer,
            citations = response.Citations,
            usedChunks = response.UsedChunks,
            promptTokens = response.PromptTokens,
            completionTokens = response.CompletionTokens,
            totalTokens = response.TotalTokens,
            providerId = response.ProviderId,
            providerName = response.ProviderName,
            quota = response.Quota
        });
    }

    private async Task<bool> MustChangePasswordAsync()
    {
        if (Context.User == null)
            return false;

        var user = await _userManager.GetUserAsync(Context.User);
        return user is { MustChangePassword: true };
    }

    private static string SessionGroup(int sessionId) => $"chat-session-{sessionId}";
}
