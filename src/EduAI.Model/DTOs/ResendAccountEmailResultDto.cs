namespace EduAI.Model.DTOs;

public class ResendAccountEmailResultDto
{
    public bool Success { get; set; }
    public bool EmailSent { get; set; }
    public string? TemporaryPassword { get; set; }
    public string? ErrorMessage { get; set; }
}

