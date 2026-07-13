namespace EduAI.Model.DTOs;

public class SubjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasMaterials { get; set; }
    public int DocumentCount { get; set; }
    public int ChunkCount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateSubjectDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class UpdateSubjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AssignTeacherDto
{
    public int SubjectId { get; set; }
    public string? TeacherId { get; set; }
}

public class SubjectOperationResultDto
{
    public bool Success { get; set; }
    public SubjectDto? Subject { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AssignTeacherResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SubjectAssignmentDto
{
    public int Id { get; set; }
    public string TeacherId { get; set; } = string.Empty;
    public string? TeacherName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
}
