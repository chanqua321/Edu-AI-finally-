using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface INotificationService
{
    Task NotifyAsync(RealtimeEventDto evt);
}
