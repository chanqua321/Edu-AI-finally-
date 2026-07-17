namespace EduAI.Model.Constants;

/// <summary>Trạng thái gói đăng ký của người dùng — dùng thay magic string.</summary>
public static class SubscriptionStatus
{
    public const string Active = "Active";
    public const string Expired = "Expired";
}

/// <summary>Trạng thái giao dịch thanh toán — dùng thay magic string.</summary>
public static class TransactionStatus
{
    public const string Pending = "Pending";
    public const string Success = "Success";
    public const string Failed = "Failed";
}
