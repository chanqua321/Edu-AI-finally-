using EduAI.Model.Enums;

namespace EduAI.Model.Entities;

public class SubjectAssignment : BaseEntity
{
    public int SubjectId { get; set; }
    public string TeacherId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Current;

    public Subject Subject { get; set; } = null!;
    public ApplicationUser Teacher { get; set; } = null!;
}
