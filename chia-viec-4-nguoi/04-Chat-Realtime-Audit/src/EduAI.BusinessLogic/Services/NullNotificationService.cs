using EduAI.BusinessLogic.IService;
using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.Services;

public class NullNotificationService : INotificationService
{
    public Task NotifyAsync(RealtimeEventDto evt) => Task.CompletedTask;
}
