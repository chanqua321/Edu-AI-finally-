using EduAI.BusinessLogic.IService;
using EduAI.Model.DTOs;
using EduAI.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EduAI.Web.Services;

public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public SignalRNotificationService(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyAsync(RealtimeEventDto evt)
    {
        foreach (var group in GetTargetGroups(evt.EntityType))
            await _hubContext.Clients.Group(group).SendAsync("EntityChanged", evt);
    }

    private static IEnumerable<string> GetTargetGroups(string entityType) => entityType switch
    {
        "User" or "AuditLog" or "ChatMessage" or "ChatSession" =>
            new[] { NotificationHub.AdminGroup },
        "Chunk" or "Document" or "Embedding" or "DocumentIndex" =>
            new[] { NotificationHub.AdminGroup, NotificationHub.TeacherGroup },
        _ => new[]
        {
            NotificationHub.AdminGroup,
            NotificationHub.TeacherGroup,
            NotificationHub.StudentGroup
        }
    };
}
