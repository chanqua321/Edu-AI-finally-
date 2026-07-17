using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace EduAI.Web.Pages.Payment;

[Authorize(Roles = Roles.Student)]
public class PackagesModel : PageModel
{
    private readonly IPaymentService _paymentService;
    private readonly IVnPayService _vnpayService;
    // appsettings: "AppSettings:AppBaseUrl" → dựng returnUrl sau khi VNPay redirect về /Payment/Return.
    private readonly AppSettings _appSettings;

    public PackagesModel(
        IPaymentService paymentService,
        IVnPayService vnpayService,
        IOptions<AppSettings> appSettings)
    {
        _paymentService = paymentService;
        _vnpayService = vnpayService;
        _appSettings = appSettings.Value;
    }

    public IReadOnlyList<PaymentPackage> Packages { get; set; } = Array.Empty<PaymentPackage>();
    public UserSubscription? CurrentSubscription { get; set; }
    public int RemainingChats { get; set; }
    public AiProviderQuotaOverviewDto ProviderQuotaOverview { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(string? success)
    {
        if (!string.IsNullOrWhiteSpace(success))
            SuccessMessage = success;
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostBuyAsync(string packageId)
    {
        var package = await _paymentService.GetPackageByIdAsync(packageId);
        if (package == null)
        {
            ErrorMessage = "Gói cước không hợp lệ.";
            await LoadDataAsync();
            return Page();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var current = await _paymentService.GetActiveSubscriptionAsync(userId);
        var currentPackage = current == null
            ? null
            : await _paymentService.GetPackageByIdAsync(current.PackageId);

        if (current != null && current.PackageId == package.Id)
        {
            ErrorMessage = "Bạn đang dùng gói này rồi.";
            await LoadDataAsync();
            return Page();
        }

        if (currentPackage != null
            && PaymentPackageRanks.ComparePackages(
                currentPackage.Id, currentPackage.Price,
                package.Id, package.Price) > 0)
        {
            ErrorMessage = "Bạn đang dùng gói cao hơn nên không thể kích hoạt gói thấp hơn.";
            await LoadDataAsync();
            return Page();
        }

        if (package.Price <= 0)
        {
            var txn = await _paymentService.CreateTransactionAsync(userId, packageId, 0);
            await _paymentService.CompleteTransactionAsync(txn.Id, "FREE_ACTIVATION", "Success");
            return RedirectToPage("/Chat/Index");
        }

        var transaction = await _paymentService.CreateTransactionAsync(userId, packageId, package.Price);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        // appsettings: "AppSettings:AppBaseUrl" → VNPay redirect user về đây sau thanh toán.
        var returnUrl = $"{_appSettings.AppBaseUrl.TrimEnd('/')}/Payment/Return";

        var paymentUrl = _vnpayService.CreatePaymentUrl(
            transaction.Id,
            transaction.Amount,
            $"Thanh toan mua goi cuyen {package.Name} tren EduAI",
            ipAddress,
            returnUrl);

        return Redirect(paymentUrl);
    }

    private async Task LoadDataAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        Packages = await _paymentService.GetPackagesAsync();
        CurrentSubscription = await _paymentService.GetActiveSubscriptionAsync(userId);
        RemainingChats = await _paymentService.GetRemainingChatsAsync(userId);
        ProviderQuotaOverview = await _paymentService.GetAiProviderQuotaOverviewAsync(userId);
    }
}
