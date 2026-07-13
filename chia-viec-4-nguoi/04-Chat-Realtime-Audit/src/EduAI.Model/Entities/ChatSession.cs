namespace EduAI.Model.Entities;

public class ChatSession : BaseEntity
{
    public string StudentId { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;

    public ApplicationUser Student { get; set; } = null!;
    public Subject Subject { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
