using System.ComponentModel.DataAnnotations;

namespace EduAI.Model.ViewModels;

public class ChatIndexViewModel
{
    public IReadOnlyList<DTOs.SubjectDto> Subjects { get; set; } = Array.Empty<DTOs.SubjectDto>();
    public IReadOnlyList<DTOs.ChatSessionDto> Sessions { get; set; } = Array.Empty<DTOs.ChatSessionDto>();
}

public class ChatSessionViewModel
{
    public int SessionId { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public IReadOnlyList<DTOs.ChatMessageDto> Messages { get; set; } = Array.Empty<DTOs.ChatMessageDto>();

    [Required, StringLength(2000)]
    public string Question { get; set; } = string.Empty;
}

public class ChatCreateSessionViewModel
{
    [Required]
    public int SubjectId { get; set; }
}

public class ChatEditSessionViewModel
{
    public int SessionId { get; set; }
    public string SubjectName { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;
}
