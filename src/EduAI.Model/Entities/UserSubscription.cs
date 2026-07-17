using System;

namespace EduAI.Model.Entities;

public class UserSubscription
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = "Active"; // Active, Expired

    public ApplicationUser User { get; set; } = null!;
    public PaymentPackage Package { get; set; } = null!;
}
