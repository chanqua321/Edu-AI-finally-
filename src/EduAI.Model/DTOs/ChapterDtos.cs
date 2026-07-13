namespace EduAI.Model.DTOs;

public class ChapterDto
{
    public int Id { get; set; }
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
    public int DocumentCount { get; set; }
    public int LessonCount { get; set; }
    public int ChunkCount { get; set; }
}

public class CreateChapterDto
{
    public int SubjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
}

public class UpdateChapterDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
}
