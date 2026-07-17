using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Payment;

[Authorize]
public class ReturnModel : PageModel
{
    private readonly IPaymentService _paymentService;
    private readonly IVnPayService _vnpayService;

    public ReturnModel(IPaymentService paymentService, IVnPayService vnpayService)
    {
        _paymentService = paymentService;
        _vnpayService = vnpayService;
    }

    public bool IsSuccess { get; set; }
    public string? Message { get; set; }
    public string? TransactionId { get; set; }
    public string? ProviderTxnId { get; set; }
    public decimal Amount { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string PaymentProvider { get; set; } = "VNPAY";
    public string? BankCode { get; set; }
    public string? OrderInfo { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? SubscriptionStart { get; set; }
    public DateTime? SubscriptionEnd { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;

    public async Task OnGetAsync()
    {
        CustomerName = User.Identity?.Name ?? string.Empty;
        CustomerEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? string.Empty;

        var vnpayParams = ExtractVnPayParams();
        if (!vnpayParams.ContainsKey("vnp_SecureHash"))
        {
            Fail("Không tìm thấy thông tin xác thực giao dịch.");
            return;
        }

        var secureHash = vnpayParams["vnp_SecureHash"];
        var validateParams = vnpayParams
            .Where(p => p.Key != "vnp_SecureHash" && p.Key != "vnp_SecureHashType")
            .ToDictionary(p => p.Key, p => p.Value);

        if (!_vnpayService.ValidateSignature(validateParams, secureHash))
        {
            Fail("Chữ ký bảo mật không hợp lệ. Giao dịch bị nghi ngờ gian lận.");
            return;
        }

        TransactionId = vnpayParams.GetValueOrDefault("vnp_TxnRef");
        ProviderTxnId = vnpayParams.GetValueOrDefault("vnp_TransactionNo");
        BankCode = vnpayParams.GetValueOrDefault("vnp_BankCode");
        OrderInfo = vnpayParams.GetValueOrDefault("vnp_OrderInfo");
        var responseCode = vnpayParams.GetValueOrDefault("vnp_ResponseCode");
        PaidAt = ParseVnPayDate(vnpayParams.GetValueOrDefault("vnp_PayDate")) ?? DateTime.Now;

        if (string.IsNullOrEmpty(TransactionId))
        {
            Fail("Không tìm thấy mã giao dịch (vnp_TxnRef) để xử lý.");
            return;
        }

        if (decimal.TryParse(vnpayParams.GetValueOrDefault("vnp_Amount"), out var amountCents))
            Amount = amountCents / 100;

        var transaction = await _paymentService.GetTransactionByIdAsync(TransactionId);
        if (transaction == null)
        {
            Fail("Không tìm thấy giao dịch trong hệ thống.");
            return;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || !string.Equals(userId, transaction.UserId, StringComparison.Ordinal))
        {
            Fail("Giao dịch không thuộc tài khoản đang đăng nhập.");
            return;
        }

        if (Amount != transaction.Amount)
        {
            Fail("Số tiền VNPAY không khớp với đơn hàng. Giao dịch bị từ chối.");
            await _paymentService.CompleteTransactionAsync(TransactionId, ProviderTxnId ?? "VNPAY", TransactionStatus.Failed);
            await LoadInvoiceDetailsAsync(TransactionId);
            return;
        }

        if (responseCode == "00")
        {
            var completed = await _paymentService.CompleteTransactionAsync(
                TransactionId, ProviderTxnId ?? "VNPAY", TransactionStatus.Success);
            if (!completed)
            {
                Fail("Không thể kích hoạt gói cước (có thể do hạ cấp gói không hợp lệ).");
                await LoadInvoiceDetailsAsync(TransactionId);
                return;
            }

            IsSuccess = true;
            Message = "Giao dịch thanh toán thành công. Gói cước của bạn đã được kích hoạt.";
            await LoadInvoiceDetailsAsync(TransactionId);
            return;
        }

        Fail($"Giao dịch thất bại hoặc đã bị hủy. Mã lỗi: {responseCode}");
        await _paymentService.CompleteTransactionAsync(TransactionId, ProviderTxnId ?? "VNPAY", TransactionStatus.Failed);
        await LoadInvoiceDetailsAsync(TransactionId);
    }

    private Dictionary<string, string> ExtractVnPayParams()
    {
        var vnpayParams = new Dictionary<string, string>();
        foreach (var key in Request.Query.Keys)
        {
            if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                vnpayParams.Add(key, Request.Query[key].ToString());
        }

        return vnpayParams;
    }

    private void Fail(string message)
    {
        IsSuccess = false;
        Message = message;
    }

    private async Task LoadInvoiceDetailsAsync(string transactionId)
    {
        var transaction = await _paymentService.GetTransactionByIdAsync(transactionId);
        if (transaction == null)
            return;

        PackageId = transaction.PackageId;
        PaymentProvider = transaction.PaymentProvider;
        if (Amount <= 0)
            Amount = transaction.Amount;

        var package = await _paymentService.GetPackageByIdAsync(transaction.PackageId);
        PackageName = package?.Name ?? transaction.PackageId;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? transaction.UserId;
        var subscription = await _paymentService.GetActiveSubscriptionAsync(userId);
        if (subscription != null)
        {
            SubscriptionStart = subscription.StartDate;
            SubscriptionEnd = subscription.EndDate;
        }
    }

    private static DateTime? ParseVnPayDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParseExact(
                value,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var paidAt))
            return paidAt;

        return null;
    }
}
