namespace EduAI.Model.Constants;

public static class PaymentPackageRanks
{
    /// <summary>
    /// Rank dùng khi không có giá. Gói tùy chỉnh mặc định ngang Premium
    /// để Free có thể nâng cấp lên, Enterprise không bị hạ cấp nhầm.
    /// Ưu tiên so sánh theo <see cref="ComparePackages"/> khi có entity.
    /// </summary>
    public static int GetRank(string? packageId) => packageId switch
    {
        PaymentPackageIds.Enterprise => 3,
        PaymentPackageIds.Premium => 2,
        PaymentPackageIds.Free => 1,
        _ => 2
    };

    /// <summary>
    /// So sánh hạng gói theo giá (ưu tiên) rồi tới rank cố định.
    /// Trả về &gt; 0 nếu left cao hơn right.
    /// </summary>
    public static int ComparePackages(
        string? leftPackageId,
        decimal leftPrice,
        string? rightPackageId,
        decimal rightPrice)
    {
        var byPrice = leftPrice.CompareTo(rightPrice);
        if (byPrice != 0)
            return byPrice;

        return GetRank(leftPackageId).CompareTo(GetRank(rightPackageId));
    }
}

public static class AiUsageOperations
{
    public const string GenerateAnswer = "GenerateAnswer";
    public const string DemoSeed = "DemoSeed";
    public const string Warmup = "Warmup";

    public static bool CountsAgainstQuota(string? operation) =>
        !string.Equals(operation, DemoSeed, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(operation, Warmup, StringComparison.OrdinalIgnoreCase);
}
