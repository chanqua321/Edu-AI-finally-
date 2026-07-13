namespace EduAI.Model.DTOs;

public class IndexingSettingsDto
{
    public int ChunkSize { get; set; }
    public int ChunkOverlap { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByName { get; set; }
}

public class UpdateIndexingSettingsDto
{
    public int ChunkSize { get; set; }
    public int ChunkOverlap { get; set; }
}

public class IndexingSettingsOperationResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IndexingSettingsDto? Settings { get; set; }
}
