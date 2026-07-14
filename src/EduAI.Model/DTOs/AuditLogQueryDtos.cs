namespace EduAI.Model.DTOs;

public class AuditLogQueryDto
{
    public string? Action { get; set; }
    public string? UserId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? DetailsContains { get; set; }
    public int MaxResults { get; set; } = 200;
}

