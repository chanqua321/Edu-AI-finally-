namespace EduAI.Model.Entities;

public class PaymentPackage
{
    public string Id { get; set; } = string.Empty; // e.g., "Free", "Premium", "Enterprise"
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Số câu hỏi AI tối đa mỗi ngày. 0 = không giới hạn.</summary>
    public int MaxDailyQuestions { get; set; }

    /// <summary>Số lượt Gemini tối đa mỗi tháng. 0 = không được dùng.</summary>
    public int MonthlyGeminiQuestions { get; set; }

    /// <summary>Số lượt Ollama tối đa mỗi ngày. 0 = không được dùng.</summary>
    public int DailyOllamaQuestions { get; set; }

    /// <summary>Thời hạn gói tính bằng ngày. Free dùng 99999 (vĩnh viễn).</summary>
    public int DurationDays { get; set; } = 30;

    /// <summary>Thứ tự hiển thị trên trang chọn gói cước.</summary>
    public int DisplayOrder { get; set; } = 0;

    /// <summary>Đánh dấu gói được khuyến nghị (hiện badge "Phổ biến nhất").</summary>
    public bool IsRecommended { get; set; } = false;

    /// <summary>Gói có được hiển thị cho người dùng không.</summary>
    public bool IsActive { get; set; } = true;
}
