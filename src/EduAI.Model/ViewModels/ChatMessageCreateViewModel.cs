using System.ComponentModel.DataAnnotations;

namespace EduAI.Model.ViewModels;

public class ChatMessageCreateViewModel
{
    [Required]
    public int SessionId { get; set; }

    [Required]
    public string Role { get; set; } = "user";

    [Required, StringLength(4000)]
    public string Content { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Citations { get; set; }
}
