using System;

namespace EduAI.Model.Entities;

public class PaymentTransaction
{
    public string Id { get; set; } = string.Empty; // TxnRef (e.g. GUID or custom string)
    public string UserId { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Success, Failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string PaymentProvider { get; set; } = "VNPAY";
    public string? ProviderTransactionId { get; set; } // VNPAY transaction number

    public ApplicationUser User { get; set; } = null!;
    public PaymentPackage Package { get; set; } = null!;
}
