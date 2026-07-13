using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface IUserNotificationService
{
    Task NotifyAccountChangedAsync(UserRealtimeEventDto evt);
}
