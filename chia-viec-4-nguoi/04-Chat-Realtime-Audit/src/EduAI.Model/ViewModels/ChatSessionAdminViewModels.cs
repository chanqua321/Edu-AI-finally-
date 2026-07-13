namespace EduAI.Model.ViewModels;

public class ChatSessionAdminIndexViewModel
{
    public IReadOnlyList<DTOs.ChatSessionDto> Sessions { get; set; } = Array.Empty<DTOs.ChatSessionDto>();
}

public class ChatSessionAdminEditViewModel
{
    public int SessionId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
