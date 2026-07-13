namespace EduAI.Model.Enums;

public enum DocumentIndexStatus
{
    Pending = 0,
    Processing = 1,
    Indexed = 2,
    Failed = 3
}

public static class DocumentIndexStatusExtensions
{
    public static string ToVietnameseLabel(this DocumentIndexStatus status) => status switch
    {
        DocumentIndexStatus.Processing => "Đang xử lý",
        DocumentIndexStatus.Indexed => "Đã tải nội dung thành công",
        DocumentIndexStatus.Failed => "Lỗi xử lý",
        _ => "Chờ xử lý"
    };
}
