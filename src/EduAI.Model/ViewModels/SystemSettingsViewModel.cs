using System.ComponentModel.DataAnnotations;

namespace EduAI.Model.ViewModels;

public class SystemSettingsViewModel
{
    [Display(Name = "Kích thước chunk")]
    [Range(100, 10000)]
    public int DefaultChunkSize { get; set; } = 800;

    [Display(Name = "Độ chồng lấp")]
    [Range(0, 5000)]
    public int DefaultChunkOverlap { get; set; } = 120;

    [Display(Name = "Retrieval Top K")]
    [Range(1, 50)]
    public int RetrievalTopK { get; set; } = 5;

    [Display(Name = "Trích dẫn nguồn")]
    public bool EnableCitation { get; set; } = true;

    [Display(Name = "Generation Provider")]
    public string GenerationProvider { get; set; } = "Gemini";

    [Display(Name = "Bật benchmark logging")]
    public bool EnableBenchmarkLogging { get; set; } = true;

    [Display(Name = "Kích thước upload tối đa (MB)")]
    public long MaxUploadFileSizeMb { get; set; } = 50;

    [Display(Name = "Đuôi file cho phép")]
    public string AllowedFileExtensions { get; set; } = ".pdf,.docx,.pptx,.txt";

    [Display(Name = "Múi giờ")]
    public string DefaultTimezone { get; set; } = "UTC";

    [Display(Name = "Giờ reset quota (0–23)")]
    [Range(0, 23)]
    public int DailyQuotaResetHour { get; set; }

    [Display(Name = "Tính request thất bại vào quota")]
    public bool CountFailedRequestsAgainstQuota { get; set; }

    [Display(Name = "Giá input token ($/triệu)")]
    public decimal InputTokenPricePerMillion { get; set; }

    [Display(Name = "Giá output token ($/triệu)")]
    public decimal OutputTokenPricePerMillion { get; set; }

    [Display(Name = "Giá embedding ($/triệu)")]
    public decimal EmbeddingPricePerMillion { get; set; }

    [Display(Name = "Ghi log latency")]
    public bool EnableLatencyLogging { get; set; } = true;

    [Display(Name = "Ghi log token")]
    public bool EnableTokenLogging { get; set; } = true;

    [Display(Name = "Ghi log chi phí")]
    public bool EnableCostLogging { get; set; } = true;

    public DateTime UpdatedAt { get; set; }
    public string? UpdatedByName { get; set; }
}
