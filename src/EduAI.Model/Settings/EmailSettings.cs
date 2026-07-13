using System.ComponentModel.DataAnnotations;

namespace EduAI.Model.Settings;

public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public bool Enabled { get; set; }

    [Required]
    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    [Required, EmailAddress]
    public string SenderEmail { get; set; } = string.Empty;

    public string SenderName { get; set; } = "EduAI";

    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
