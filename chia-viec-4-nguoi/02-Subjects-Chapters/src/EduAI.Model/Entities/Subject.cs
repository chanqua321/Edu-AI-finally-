namespace EduAI.Model.Entities;

public class Subject : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? TeacherId { get; set; }
    public bool IsActive { get; set; } = true;

    public ApplicationUser? Teacher { get; set; }
    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<SubjectAssignment> Assignments { get; set; } = new List<SubjectAssignment>();
}
