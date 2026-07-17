using EduAI.Model.DTOs;

namespace EduAI.Model.DTOs;

public class SystemSettingsDto
{
    // Admin-tunable RAG
    public int DefaultChunkSize { get; set; }
    public int DefaultChunkOverlap { get; set; }
    public int RetrievalTopK { get; set; }
    public bool EnableCitation { get; set; }
    public string GenerationProvider { get; set; } = "Gemini";

    // Benchmark
    public bool EnableBenchmarkLogging { get; set; }

    // Upload
    public long MaxUploadFileSizeBytes { get; set; }
    public string AllowedFileExtensions { get; set; } = string.Empty;

    // Chat
    public string DefaultTimezone { get; set; } = string.Empty;
    public int DailyQuotaResetHour { get; set; }
    public bool CountFailedRequestsAgainstQuota { get; set; }

    // AI Pricing
    public decimal InputTokenPricePerMillion { get; set; }
    public decimal OutputTokenPricePerMillion { get; set; }
    public decimal EmbeddingPricePerMillion { get; set; }

    // Logging
    public bool EnableLatencyLogging { get; set; }
    public bool EnableTokenLogging { get; set; }
    public bool EnableCostLogging { get; set; }

    // Metadata
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedByName { get; set; }
}

public class UpdateSystemSettingsDto
{
    // Admin-tunable RAG
    public int DefaultChunkSize { get; set; }
    public int DefaultChunkOverlap { get; set; }
    public int RetrievalTopK { get; set; }
    public bool EnableCitation { get; set; }
    public string GenerationProvider { get; set; } = "Gemini";

    public bool EnableBenchmarkLogging { get; set; }

    // Upload
    public long MaxUploadFileSizeBytes { get; set; }
    public string AllowedFileExtensions { get; set; } = string.Empty;

    // Chat
    public string DefaultTimezone { get; set; } = string.Empty;
    public int DailyQuotaResetHour { get; set; }
    public bool CountFailedRequestsAgainstQuota { get; set; }

    // AI Pricing
    public decimal InputTokenPricePerMillion { get; set; }
    public decimal OutputTokenPricePerMillion { get; set; }
    public decimal EmbeddingPricePerMillion { get; set; }

    // Logging
    public bool EnableLatencyLogging { get; set; }
    public bool EnableTokenLogging { get; set; }
    public bool EnableCostLogging { get; set; }
}

public class SystemSettingsOperationResultDto
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public SystemSettingsDto? Settings { get; set; }
}
