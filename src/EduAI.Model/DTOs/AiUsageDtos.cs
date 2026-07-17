namespace EduAI.Model.DTOs;

public class AiTokenUsageDto
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

public class GenerateAnswerResultDto
{
    public string Answer { get; set; } = string.Empty;
    public AiTokenUsageDto? Usage { get; set; }
}

public static class ReportPeriods
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";
    public const string Custom = "custom";

    public static int ToDays(string? period) => period?.ToLowerInvariant() switch
    {
        Day => 1,
        Week => 7,
        Month => 30,
        _ => 30
    };

    public static string Normalize(string? period) => period?.ToLowerInvariant() switch
    {
        Day => Day,
        Week => Week,
        Custom => Custom,
        _ => Month
    };
}

public class ReportFilterDto
{
    public string Period { get; set; } = ReportPeriods.Month;
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public class ReportNamedCountDto
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public long? ExtraLong { get; set; }
}

public class ReportTokenBySubjectDto
{
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public int RequestCount { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}

public class ReportCostByProviderDto
{
    public string Provider { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
}

public class ReportRevenueByPackageDto
{
    public string PackageId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public int PaymentCount { get; set; }
    public decimal TotalAmountVnd { get; set; }
}

public class ReportTokenByDayDto
{
    public DateOnly? Date { get; set; }
    public string Label { get; set; } = string.Empty;
    public int TotalTokens { get; set; }
    public int RequestCount { get; set; }
}

public class ReportChatActivityBySubjectDto
{
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public int QuestionCount { get; set; }
}

public class ReportTopCitedChunkDto
{
    public int ChunkId { get; set; }
    public int DocumentId { get; set; }
    public string DocumentFileName { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int CitationCount { get; set; }
    public string Preview { get; set; } = string.Empty;
}

public class ReportDocumentBySubjectDto
{
    public int SubjectId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public int DocumentCount { get; set; }
    public int IndexedCount { get; set; }
    public long TotalBytes { get; set; }
    public int ChunkCount { get; set; }
}

public class ReportDashboardDto
{
    public bool IsAdminView { get; set; }
    public string Period { get; set; } = ReportPeriods.Month;
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }

    // Overview cards
    public int TotalUsers { get; set; }
    public int TotalTeachers { get; set; }
    public int TotalStudents { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int TotalSubjects { get; set; }
    public int TotalDocuments { get; set; }
    public int IndexedDocuments { get; set; }
    public long StorageBytes { get; set; }
    public int TotalChunks { get; set; }
    public int TotalEmbeddings { get; set; }
    public int TotalTokens { get; set; }
    public int TotalQuestions { get; set; }
    public int TotalChatSessions { get; set; }
    public int LoginCount { get; set; }
    public int DownloadCount { get; set; }
    public int ErrorAuditCount { get; set; }

    /// <summary>Tổng chi phí AI ước tính (USD) trong khoảng lọc.</summary>
    public decimal TotalEstimatedCostUsd { get; set; }

    /// <summary>Tỷ giá quy đổi USD → VND để cân đối với doanh thu.</summary>
    public decimal UsdToVndRate { get; set; } = 25_000m;

    public decimal TotalEstimatedCostVnd => TotalEstimatedCostUsd * UsdToVndRate;

    /// <summary>Tổng tiền người dùng đã mua gói thành công (VND).</summary>
    public decimal TotalRevenueVnd { get; set; }

    public int SuccessfulPaymentCount { get; set; }

    /// <summary>Doanh thu − chi phí AI (cùng đơn vị VND).</summary>
    public decimal CostBalanceVnd => TotalRevenueVnd - TotalEstimatedCostVnd;

    public IReadOnlyList<ReportTokenBySubjectDto> TokensBySubject { get; set; } = Array.Empty<ReportTokenBySubjectDto>();
    public IReadOnlyList<ReportTokenByDayDto> TokensByDay { get; set; } = Array.Empty<ReportTokenByDayDto>();
    public IReadOnlyList<ReportChatActivityBySubjectDto> ChatActivityBySubject { get; set; } = Array.Empty<ReportChatActivityBySubjectDto>();
    public IReadOnlyList<ReportTopCitedChunkDto> TopCitedChunks { get; set; } = Array.Empty<ReportTopCitedChunkDto>();
    public IReadOnlyList<ReportDocumentBySubjectDto> DocumentsBySubject { get; set; } = Array.Empty<ReportDocumentBySubjectDto>();
    public IReadOnlyList<ReportNamedCountDto> DocumentsByTeacher { get; set; } = Array.Empty<ReportNamedCountDto>();
    public IReadOnlyList<ReportNamedCountDto> MostActiveStudents { get; set; } = Array.Empty<ReportNamedCountDto>();
    public IReadOnlyList<ReportNamedCountDto> MostActiveTeachers { get; set; } = Array.Empty<ReportNamedCountDto>();
    public IReadOnlyList<ReportNamedCountDto> TopDownloadedDocuments { get; set; } = Array.Empty<ReportNamedCountDto>();
    public IReadOnlyList<ReportTokenByDayDto> ActivityTrend { get; set; } = Array.Empty<ReportTokenByDayDto>();
    public IReadOnlyList<ReportCostByProviderDto> CostByProvider { get; set; } = Array.Empty<ReportCostByProviderDto>();
    public IReadOnlyList<ReportRevenueByPackageDto> RevenueByPackage { get; set; } = Array.Empty<ReportRevenueByPackageDto>();

    public bool HasAiActivity => TotalTokens > 0 || TotalQuestions > 0;
    public bool HasDocuments => TotalDocuments > 0;
    public bool HasFinanceData => TotalEstimatedCostUsd > 0 || TotalRevenueVnd > 0 || SuccessfulPaymentCount > 0;
}
