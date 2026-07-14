using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using EduAI.BusinessLogic.IService;

namespace EduAI.BusinessLogic.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationService _notificationService;

    public AuditLogService(IUnitOfWork unitOfWork, INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task LogAsync(CreateAuditLogDto dto)
    {
        var log = new AuditLog
        {
            UserId = dto.UserId,
            Action = dto.Action,
            IpAddress = dto.IpAddress,
            Details = dto.Details,
            Timestamp = DateTime.UtcNow
        };

        await _unitOfWork.AuditLogs.AddAsync(log);
        await _unitOfWork.SaveChangesAsync();

        await _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "AuditLog",
            Action = dto.Action,
            EntityId = log.Id,
            Message = dto.Details ?? dto.Action
        });
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetRecentAsync(int count = 100)
    {
        var logs = await _unitOfWork.AuditLogs.GetRecentAsync(count);
        return logs.Select(l => new AuditLogDto
        {
            Id = l.Id,
            UserId = l.UserId,
            UserName = l.User?.FullName ?? l.User?.UserName,
            Action = l.Action,
            Timestamp = l.Timestamp,
            IpAddress = l.IpAddress,
            Details = l.Details
        }).ToList();
    }

    public async Task<AuditLogDto?> GetByIdAsync(int id)
    {
        var log = await _unitOfWork.AuditLogs.GetDetailByIdAsync(id);
        if (log == null)
            return null;

        return new AuditLogDto
        {
            Id = log.Id,
            UserId = log.UserId,
            UserName = log.User?.FullName ?? log.User?.UserName,
            Action = log.Action,
            Timestamp = log.Timestamp,
            IpAddress = log.IpAddress,
            Details = log.Details
        };
    }

    public async Task<IReadOnlyList<AuditLogDto>> SearchAsync(AuditLogQueryDto query)
    {
        var logs = await _unitOfWork.AuditLogs.SearchAsync(
            query.Action,
            query.UserId,
            query.IpAddress,
            query.From,
            query.To,
            query.DetailsContains,
            query.MaxResults);

        return logs.Select(l => new AuditLogDto
        {
            Id = l.Id,
            UserId = l.UserId,
            UserName = l.User?.FullName ?? l.User?.UserName,
            Action = l.Action,
            Timestamp = l.Timestamp,
            IpAddress = l.IpAddress,
            Details = l.Details
        }).ToList();
    }
}
