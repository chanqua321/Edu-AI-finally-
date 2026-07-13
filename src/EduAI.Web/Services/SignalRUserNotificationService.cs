using EduAI.BusinessLogic.IService;
using EduAI.Model.DTOs;
using EduAI.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EduAI.Web.Services;

public class SignalRUserNotificationService : IUserNotificationService
{
    private readonly IHubContext<UserHub> _hubContext;

    public SignalRUserNotificationService(IHubContext<UserHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyAccountChangedAsync(UserRealtimeEventDto evt) =>
        _hubContext.Clients.Group(UserHub.GetUserGroup(evt.UserId)).SendAsync("AccountChanged", evt);
}
