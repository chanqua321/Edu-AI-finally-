namespace EduAI.Model.Entities;

public class Lesson : BaseEntity
{
    public int ChapterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OrderNumber { get; set; }

    public Chapter Chapter { get; set; } = null!;
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
