namespace EduAI.Model.DTOs;

public class SubjectRealtimeEventDto
{
    public string Action { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public SubjectDto? Subject { get; set; }
    public string? PreviousTeacherId { get; set; }
}
