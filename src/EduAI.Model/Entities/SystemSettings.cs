using EduAI.Model.Enums;

namespace EduAI.Model.Entities;

/// <summary>
/// Singleton row — toàn bộ cấu hình nghiệp vụ toàn hệ thống.
/// Thay thế và mở rộng IndexingSettings cũ.
/// Admin chỉnh sửa tại /Settings/System.
/// </summary>
public class SystemSettings
{
    public int Id { get; set; }

    // ─── AI & RAG ─────────────────────────────────────────────────────────────
    /// <summary>Chiến lược chia chunk toàn hệ thống.</summary>
    public ChunkMode DefaultChunkMode { get; set; } = ChunkMode.Character;

    /// <summary>Kích thước chunk (số ký tự/từ/đoạn).</summary>
    public int DefaultChunkSize { get; set; } = 800;

    /// <summary>Độ chồng lấp chunk.</summary>
    public int DefaultChunkOverlap { get; set; } = 120;

    /// <summary>Số chunk liên quan tối đa lấy ra mỗi lần tìm kiếm RAG.</summary>
    public int RetrievalTopK { get; set; } = 5;

    /// <summary>Số tin nhắn lịch sử chat tối đa đưa vào ngữ cảnh Gemini.</summary>
    public int MaxChatHistory { get; set; } = 10;

    /// <summary>Có hiển thị trích dẫn nguồn (Citations) trong câu trả lời AI không.</summary>
    public bool EnableCitation { get; set; } = true;

    /// <summary>Generation provider: Gemini | Ollama.</summary>
    public string GenerationProvider { get; set; } = "Gemini";

    /// <summary>Bật/tắt toàn bộ benchmark logging (phủ tất cả cờ phía dưới).</summary>
    public bool EnableBenchmarkLogging { get; set; } = true;

    /// <summary>Model embedding mặc định (ví dụ: text-embedding-004).</summary>
    public string DefaultEmbeddingModel { get; set; } = string.Empty;

    /// <summary>Model generation mặc định (ví dụ: gemini-2.5-flash).</summary>
    public string DefaultGenerationModel { get; set; } = string.Empty;

    /// <summary>Temperature cho Gemini generation (0.0 – 2.0).</summary>
    public double Temperature { get; set; } = 0.7;

    /// <summary>Số token đầu ra tối đa.</summary>
    public int MaxOutputTokens { get; set; } = 8192;

    // ─── Upload ───────────────────────────────────────────────────────────────
    /// <summary>Kích thước file upload tối đa (bytes). Mặc định 50 MB.</summary>
    public long MaxUploadFileSizeBytes { get; set; } = 52_428_800;

    /// <summary>Danh sách đuôi file cho phép, phân tách bằng dấu phẩy (ví dụ: .pdf,.docx,.pptx,.txt).</summary>
    public string AllowedFileExtensions { get; set; } = ".pdf,.docx,.pptx,.txt";

    // ─── Chat & Quota ─────────────────────────────────────────────────────────
    /// <summary>Múi giờ mặc định khi tính quota hàng ngày (ví dụ: UTC, SE Asia Standard Time).</summary>
    public string DefaultTimezone { get; set; } = "UTC";

    /// <summary>Giờ UTC reset quota mỗi ngày (0 = nửa đêm UTC).</summary>
    public int DailyQuotaResetHour { get; set; } = 0;

    /// <summary>Có tính các request thất bại vào quota hàng ngày không.</summary>
    public bool CountFailedRequestsAgainstQuota { get; set; } = false;

    // ─── AI Token Pricing ────────────────────────────────────────────────────
    /// <summary>Giá token input (USD / triệu token).</summary>
    public decimal InputTokenPricePerMillion { get; set; } = 0.075m;

    /// <summary>Giá token output (USD / triệu token).</summary>
    public decimal OutputTokenPricePerMillion { get; set; } = 0.30m;

    /// <summary>Giá token embedding (USD / triệu token).</summary>
    public decimal EmbeddingPricePerMillion { get; set; } = 0.01m;

    // ─── Benchmark / Logging ─────────────────────────────────────────────────
    /// <summary>Ghi log thời gian xử lý (EmbeddingTime, RetrievalTime, GenerationTime).</summary>
    public bool EnableLatencyLogging { get; set; } = true;

    /// <summary>Ghi log số token tiêu thụ.</summary>
    public bool EnableTokenLogging { get; set; } = true;

    /// <summary>Ghi log ước tính chi phí API.</summary>
    public bool EnableCostLogging { get; set; } = true;

    // ─── Metadata ────────────────────────────────────────────────────────────
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedByUserId { get; set; }
    public ApplicationUser? UpdatedBy { get; set; }
}
