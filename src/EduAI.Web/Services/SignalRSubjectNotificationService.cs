using EduAI.BusinessLogic.IService;
using EduAI.Model.DTOs;
using EduAI.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EduAI.Web.Services;

public class SignalRSubjectNotificationService : ISubjectNotificationService
{
    private readonly IHubContext<SubjectHub> _hubContext;

    public SignalRSubjectNotificationService(IHubContext<SubjectHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifySubjectChangedAsync(SubjectRealtimeEventDto evt) =>
        _hubContext.Clients.Group("subjects").SendAsync("SubjectChanged", evt);
}
