namespace EduAI.Model.Settings;

public class AppSettings
{
    public const string SectionName = "AppSettings";

    public string LoginUrl { get; set; } = string.Empty;
    public string AppBaseUrl { get; set; } = string.Empty;
    public string UploadPath { get; set; } = "uploads";
    public long MaxUploadBytes { get; set; } = 52_428_800;
}
