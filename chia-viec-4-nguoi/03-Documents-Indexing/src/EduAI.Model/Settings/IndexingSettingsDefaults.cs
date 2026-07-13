namespace EduAI.Model.Settings;

public class IndexingSettingsDefaults
{
    public const string SectionName = "Indexing";

    public int ChunkSize { get; set; } = 800;

    public int ChunkOverlap { get; set; } = 120;
}
