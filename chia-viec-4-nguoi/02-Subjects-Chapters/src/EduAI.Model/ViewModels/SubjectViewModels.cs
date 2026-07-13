using System.ComponentModel.DataAnnotations;

namespace EduAI.Model.ViewModels;

public class SubjectIndexViewModel
{
    public IReadOnlyList<DTOs.SubjectDto> Subjects { get; set; } = Array.Empty<DTOs.SubjectDto>();
}

public class SubjectFormViewModel
{
    public int Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public string? TeacherId { get; set; }
    public IReadOnlyList<DTOs.UserDto> Teachers { get; set; } = Array.Empty<DTOs.UserDto>();
}

public class SubjectDetailsViewModel
{
    public DTOs.SubjectDto? Subject { get; set; }
}
