using System.ComponentModel.DataAnnotations;

namespace EduAI.Model.ViewModels;

public class ChapterIndexViewModel
{
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public IReadOnlyList<DTOs.ChapterDto> Chapters { get; set; } = Array.Empty<DTOs.ChapterDto>();
}

public class ChapterFormViewModel
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, Range(1, 999)]
    public int OrderNumber { get; set; } = 1;
}

public class ChapterDetailsViewModel
{
    public DTOs.ChapterDto? Chapter { get; set; }
}
