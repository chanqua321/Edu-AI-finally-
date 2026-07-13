using EduAI.BusinessLogic.IService;
using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.Services;

public class NullSubjectNotificationService : ISubjectNotificationService
{
    public Task NotifySubjectChangedAsync(SubjectRealtimeEventDto evt) => Task.CompletedTask;
}
