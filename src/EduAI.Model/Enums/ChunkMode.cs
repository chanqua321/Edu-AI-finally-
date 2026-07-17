namespace EduAI.Model.Enums;

/// <summary>Chiến lược chia chunk do Admin cấu hình toàn hệ thống.</summary>
public enum ChunkMode
{
    /// <summary>Gom theo ký tự (mặc định).</summary>
    Character = 0,

    /// <summary>Gom theo số đoạn văn (paragraph).</summary>
    Paragraph = 1,

    /// <summary>Gom theo số từ (word).</summary>
    Word = 2
}

public static class ChunkModeExtensions
{
    public static string ToVietnameseLabel(this ChunkMode mode) => mode switch
    {
        ChunkMode.Character => "Theo ký tự (mặc định)",
        ChunkMode.Paragraph => "Theo đoạn văn",
        ChunkMode.Word => "Theo từ",
        _ => mode.ToString()
    };
}
