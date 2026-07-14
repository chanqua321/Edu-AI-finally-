using EduAI.BusinessLogic.IService;
using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.Services;

public class NullUserNotificationService : IUserNotificationService
{
    public Task NotifyAccountChangedAsync(UserRealtimeEventDto evt) => Task.CompletedTask;
}
