namespace EduAI.Model.Enums;

public enum DocumentCategory
{
    Lecture = 0,
    Exercise = 1,
    Exam = 2,
    Reference = 3,
    Other = 4
}

public static class DocumentCategoryExtensions
{
    public static string ToVietnameseLabel(this DocumentCategory category) => category switch
    {
        DocumentCategory.Lecture => "Bài giảng",
        DocumentCategory.Exercise => "Bài tập",
        DocumentCategory.Exam => "Đề thi",
        DocumentCategory.Reference => "Tham khảo",
        _ => "Khác"
    };
}
