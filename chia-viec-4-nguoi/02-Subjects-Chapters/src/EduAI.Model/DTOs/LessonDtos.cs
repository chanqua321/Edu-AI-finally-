namespace EduAI.Model.DTOs;

public class LessonDto
{
    public int Id { get; set; }
    public int ChapterId { get; set; }
    public string ChapterName { get; set; } = string.Empty;
    public int SubjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
    public int DocumentCount { get; set; }
}

public class CreateLessonDto
{
    public int ChapterId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
}

public class UpdateLessonDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
}
