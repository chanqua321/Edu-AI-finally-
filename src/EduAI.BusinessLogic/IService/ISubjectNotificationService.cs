using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface ISubjectNotificationService
{
    Task NotifySubjectChangedAsync(SubjectRealtimeEventDto evt);
}
