namespace EduAI.Model.ViewModels;

public class AuditLogIndexViewModel
{
    public IReadOnlyList<DTOs.AuditLogDto> Logs { get; set; } = Array.Empty<DTOs.AuditLogDto>();
}

public class AuditLogDetailsViewModel
{
    public DTOs.AuditLogDto? Log { get; set; }
}
