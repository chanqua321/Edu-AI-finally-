namespace EduAI.Model.Entities;

public class Chapter : BaseEntity
{
    public int SubjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OrderNumber { get; set; }

    public Subject Subject { get; set; } = null!;
    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
