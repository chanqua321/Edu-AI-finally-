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
    public DTOs.AiProviderQuotaOverviewDto ProviderQuotaOverview { get; set; } = new();
    public string DefaultProviderId { get; set; } = string.Empty;
    /// <summary>Tài liệu đã index — dùng để lọc RAG theo 1 file.</summary>
    public IReadOnlyList<ChatDocumentOptionViewModel> Documents { get; set; } = Array.Empty<ChatDocumentOptionViewModel>();

    [Required, StringLength(2000)]
    public string Question { get; set; } = string.Empty;
}

public class ChatDocumentOptionViewModel
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
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
