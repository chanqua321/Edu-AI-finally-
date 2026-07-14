namespace EduAI.Model.Entities;

public class ChatMessage : BaseEntity
{
    public int ChatSessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Citations { get; set; }

    public ChatSession ChatSession { get; set; } = null!;
}
