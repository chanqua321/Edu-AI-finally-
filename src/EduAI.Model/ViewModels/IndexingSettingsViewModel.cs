using System.ComponentModel.DataAnnotations;

namespace EduAI.Model.ViewModels;

public class IndexingSettingsViewModel
{
    [Display(Name = "Kích thước chunk (ký tự)")]
    [Range(200, 8000, ErrorMessage = "Kích thước chunk phải từ 200 đến 8000 ký tự.")]
    public int ChunkSize { get; set; } = 800;

    [Display(Name = "Độ chồng lấp (ký tự)")]
    [Range(0, 2000, ErrorMessage = "Độ chồng lấp phải từ 0 đến 2000 ký tự.")]
    public int ChunkOverlap { get; set; } = 120;
}
