namespace EduAI.Model.DTOs;

/// <summary>
/// Kết quả kiểm tra giới hạn chat hàng ngày của Student.
/// Thay thế kiểu trả về bool đơn giản của CheckChatLimitAsync().
/// </summary>
public class ChatLimitResult
{
    /// <summary>Student có thể gửi thêm câu hỏi hôm nay không.</summary>
    public bool CanChat { get; set; }

    /// <summary>Số câu đã dùng hôm nay.</summary>
    public int UsedQuestions { get; set; }

    /// <summary>Số câu còn lại hôm nay (-1 = không giới hạn).</summary>
    public int RemainingQuestions { get; set; }

    /// <summary>Giới hạn tối đa mỗi ngày (0 = không giới hạn).</summary>
    public int MaxQuestions { get; set; }

    /// <summary>Tên gói cước hiện tại.</summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>Thông báo cho người dùng (lý do chặn hoặc trống nếu được phép).</summary>
    public string Message { get; set; } = string.Empty;
}

public class ProviderQuotaCheckResult
{
    public bool CanUse { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderDisplayName { get; set; } = string.Empty;
    public string WindowLabel { get; set; } = string.Empty;
    public int UsedCount { get; set; }
    public int RemainingCount { get; set; }
    public int LimitCount { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class AiProviderQuotaItemDto
{
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string WindowLabel { get; set; } = string.Empty;
    public int UsedCount { get; set; }
    public int RemainingCount { get; set; }
    public int LimitCount { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsDefaultChoice { get; set; }
    public string StatusText { get; set; } = string.Empty;
}

public class AiProviderQuotaOverviewDto
{
    public string PackageId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public IReadOnlyList<AiProviderQuotaItemDto> Providers { get; set; } = Array.Empty<AiProviderQuotaItemDto>();
}
