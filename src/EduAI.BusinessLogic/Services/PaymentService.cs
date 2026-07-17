using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EduAI.BusinessLogic.Helpers;
using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;

namespace EduAI.BusinessLogic.Services;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly ISystemSettingsService _systemSettingsService;

    public PaymentService(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        ISystemSettingsService systemSettingsService)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _systemSettingsService = systemSettingsService;
    }

    public async Task<IReadOnlyList<PaymentPackage>> GetPackagesAsync()
    {
        var all = await _unitOfWork.PaymentPackages.GetAllAsync();
        return all.Where(p => p.IsActive).OrderBy(p => p.DisplayOrder).ToList();
    }

    public async Task<PaymentPackage?> GetPackageByIdAsync(string id)
    {
        var packages = await _unitOfWork.PaymentPackages.FindAsync(p => p.Id == id);
        return packages.FirstOrDefault();
    }

    public async Task<UserSubscription?> GetActiveSubscriptionAsync(string userId)
    {
        await ExpireStaleSubscriptionsAsync(userId);

        var subs = await _unitOfWork.UserSubscriptions.FindAsync(
            s => s.UserId == userId && s.Status == SubscriptionStatus.Active);
        return subs.FirstOrDefault(s => s.EndDate > DateTime.UtcNow);
    }

    /// <summary>
    /// Cập nhật trạng thái "Expired" cho các subscription hết hạn của user.
    /// Nên gọi trong background job hoặc khi cần clean-up, không phải trong mọi request.
    /// </summary>
    public async Task ExpireStaleSubscriptionsAsync(string userId)
    {
        var subs = await _unitOfWork.UserSubscriptions.FindAsync(
            s => s.UserId == userId && s.Status == SubscriptionStatus.Active);
        var expiredSubs = subs.Where(s => s.EndDate <= DateTime.UtcNow).ToList();
        if (expiredSubs.Count == 0) return;

        foreach (var sub in expiredSubs)
        {
            sub.Status = SubscriptionStatus.Expired;
            _unitOfWork.UserSubscriptions.Update(sub);
        }
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<ChatLimitResult> GetChatLimitResultAsync(string userId)
    {
        var overview = await GetAiProviderQuotaOverviewAsync(userId);
        var available = overview.Providers.Where(p => p.IsAvailable).ToList();
        if (available.Count > 0)
        {
            var preferred = available.FirstOrDefault(p => p.IsDefaultChoice) ?? available[0];
            return new ChatLimitResult
            {
                CanChat = true,
                UsedQuestions = preferred.UsedCount,
                RemainingQuestions = preferred.RemainingCount,
                MaxQuestions = preferred.LimitCount,
                PackageName = overview.PackageName,
                Message = string.Empty
            };
        }

        var first = overview.Providers.FirstOrDefault();
        return new ChatLimitResult
        {
            CanChat = false,
            UsedQuestions = first?.UsedCount ?? 0,
            RemainingQuestions = 0,
            MaxQuestions = first?.LimitCount ?? 0,
            PackageName = overview.PackageName,
            Message = first?.StatusText ?? "Bạn đã hết lượt sử dụng AI trong gói hiện tại."
        };
    }

    public async Task<ProviderQuotaCheckResult> CheckProviderQuotaAsync(string userId, string providerId)
    {
        var sub = await GetActiveSubscriptionAsync(userId);
        var packageId = sub?.PackageId ?? PaymentPackageIds.Free;
        var package = await GetPackageByIdAsync(packageId);
        var packageName = package?.Name ?? packageId;

        var normalizedProvider = AiProviderIds.Normalize(providerId);
        var policy = ResolveQuotaPolicy(packageId, package);
        var systemSettings = await _systemSettingsService.GetAsync();
        var countFailures = systemSettings.CountFailedRequestsAgainstQuota;

        var (limit, windowStart, windowLabel) = normalizedProvider == AiProviderIds.Gemini
            ? (policy.GeminiMonthly, QuotaWindowHelper.GetMonthWindowStartUtc(systemSettings), "tháng này")
            : (policy.OllamaDaily, QuotaWindowHelper.GetDayWindowStartUtc(systemSettings), "hôm nay");

        if (limit <= 0)
        {
            return new ProviderQuotaCheckResult
            {
                CanUse = false,
                ProviderId = normalizedProvider,
                ProviderDisplayName = GetProviderDisplayName(normalizedProvider),
                WindowLabel = windowLabel,
                PackageName = packageName,
                LimitCount = 0,
                RemainingCount = 0,
                Message = $"Gói {packageName} không hỗ trợ {GetProviderDisplayName(normalizedProvider)}."
            };
        }

        var logs = await _unitOfWork.AiUsageLogs.FindAsync(l =>
            l.UserId == userId
            && l.Provider == normalizedProvider
            && l.CreatedAt >= windowStart
            && (countFailures || l.IsSuccess));

        var used = logs.Count(l => AiUsageOperations.CountsAgainstQuota(l.Operation));
        var remaining = Math.Max(0, limit - used);
        var canUse = used < limit;

        return new ProviderQuotaCheckResult
        {
            CanUse = canUse,
            ProviderId = normalizedProvider,
            ProviderDisplayName = GetProviderDisplayName(normalizedProvider),
            WindowLabel = windowLabel,
            UsedCount = used,
            RemainingCount = remaining,
            LimitCount = limit,
            PackageName = packageName,
            Message = canUse
                ? string.Empty
                : $"Bạn đã dùng hết {limit} lượt {GetProviderDisplayName(normalizedProvider)} trong {windowLabel} (gói {packageName})."
        };
    }

    public async Task<AiProviderQuotaOverviewDto> GetAiProviderQuotaOverviewAsync(string userId)
    {
        var sub = await GetActiveSubscriptionAsync(userId);
        var packageId = sub?.PackageId ?? PaymentPackageIds.Free;
        var package = await GetPackageByIdAsync(packageId);
        var packageName = package?.Name ?? packageId;

        var gemini = await CheckProviderQuotaAsync(userId, AiProviderIds.Gemini);
        var ollama = await CheckProviderQuotaAsync(userId, AiProviderIds.Ollama);
        var defaultProvider = ResolveDefaultProvider(packageId, gemini, ollama);

        return new AiProviderQuotaOverviewDto
        {
            PackageId = packageId,
            PackageName = packageName,
            Providers =
            [
                MapQuotaItem(gemini, defaultProvider),
                MapQuotaItem(ollama, defaultProvider)
            ]
        };
    }

    public async Task<bool> CheckChatLimitAsync(string userId)
    {
        var result = await GetChatLimitResultAsync(userId);
        return result.CanChat;
    }

    public async Task<int> GetRemainingChatsAsync(string userId)
    {
        var result = await GetChatLimitResultAsync(userId);
        return result.RemainingQuestions;
    }

    private static (int GeminiMonthly, int OllamaDaily) ResolveQuotaPolicy(string packageId, PaymentPackage? package)
    {
        // Always trust DB values when package exists (0 = disabled for that provider).
        if (package != null)
            return (package.MonthlyGeminiQuestions, package.DailyOllamaQuestions);

        return packageId switch
        {
            PaymentPackageIds.Premium => (40, 5),
            PaymentPackageIds.Enterprise => (150, 20),
            _ => (0, 1)
        };
    }

    private static string GetProviderDisplayName(string providerId) =>
        providerId == AiProviderIds.Gemini ? "Gemini 3 Flash" : "Ollama (Local)";

    private static string ResolveDefaultProvider(
        string packageId,
        ProviderQuotaCheckResult gemini,
        ProviderQuotaCheckResult ollama)
    {
        if (packageId == PaymentPackageIds.Free)
            return AiProviderIds.Ollama;

        if (gemini.CanUse)
            return AiProviderIds.Gemini;

        return ollama.CanUse ? AiProviderIds.Ollama : AiProviderIds.Gemini;
    }

    private static AiProviderQuotaItemDto MapQuotaItem(ProviderQuotaCheckResult quota, string defaultProviderId) =>
        new()
        {
            ProviderId = quota.ProviderId,
            DisplayName = quota.ProviderDisplayName,
            WindowLabel = quota.WindowLabel,
            UsedCount = quota.UsedCount,
            RemainingCount = quota.RemainingCount,
            LimitCount = quota.LimitCount,
            IsAvailable = quota.CanUse,
            IsDefaultChoice = quota.ProviderId == defaultProviderId,
            StatusText = quota.CanUse
                ? $"Còn {quota.RemainingCount}/{quota.LimitCount} lượt {quota.WindowLabel}"
                : quota.Message
        };

    public async Task<PaymentTransaction> CreateTransactionAsync(string userId, string packageId, decimal amount)
    {
        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = userId,
            PackageId = packageId,
            Amount = amount,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            PaymentProvider = "VNPAY"
        };

        await _unitOfWork.PaymentTransactions.AddAsync(transaction);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = userId,
            Action = "CreatePaymentTransaction",
            Details = $"Tạo yêu cầu mua gói {packageId} - Số tiền: {amount:N0} VND. Mã giao dịch: {transaction.Id}"
        });

        return transaction;
    }

    public async Task<bool> CompleteTransactionAsync(string transactionId, string providerTxnId, string status)
    {
        var transactions = await _unitOfWork.PaymentTransactions.FindAsync(t => t.Id == transactionId);
        var transaction = transactions.FirstOrDefault();
        if (transaction == null)
            return false;

        if (transaction.Status != TransactionStatus.Pending)
            return true;

        if (status == TransactionStatus.Success)
        {
            var package = await GetPackageByIdAsync(transaction.PackageId);
            if (package == null)
                return false;

            var subs = await _unitOfWork.UserSubscriptions.FindAsync(s => s.UserId == transaction.UserId);
            var activeSub = subs.FirstOrDefault(
                s => s.Status == SubscriptionStatus.Active && s.EndDate > DateTime.UtcNow);

            if (activeSub != null
                && activeSub.PackageId != transaction.PackageId)
            {
                var activePackage = await GetPackageByIdAsync(activeSub.PackageId);
                if (activePackage != null
                    && PaymentPackageRanks.ComparePackages(
                        package.Id, package.Price,
                        activePackage.Id, activePackage.Price) < 0)
                {
                    transaction.Status = TransactionStatus.Failed;
                    transaction.ProviderTransactionId = providerTxnId;
                    _unitOfWork.PaymentTransactions.Update(transaction);
                    await _auditLogService.LogAsync(new CreateAuditLogDto
                    {
                        UserId = transaction.UserId,
                        Action = "FailedPaymentTransaction",
                        Details = $"Từ chối hạ cấp gói từ {activeSub.PackageId} xuống {transaction.PackageId}. Mã giao dịch: {transactionId}"
                    });
                    await _unitOfWork.SaveChangesAsync();
                    return false;
                }
            }

            transaction.Status = status;
            transaction.ProviderTransactionId = providerTxnId;
            _unitOfWork.PaymentTransactions.Update(transaction);

            var durationDays = package.DurationDays;

            if (activeSub != null)
            {
                if (activeSub.PackageId == transaction.PackageId)
                {
                    activeSub.EndDate = activeSub.EndDate.AddDays(durationDays);
                    _unitOfWork.UserSubscriptions.Update(activeSub);
                }
                else
                {
                    activeSub.Status = SubscriptionStatus.Expired;
                    _unitOfWork.UserSubscriptions.Update(activeSub);

                    var newSub = new UserSubscription
                    {
                        UserId = transaction.UserId,
                        PackageId = transaction.PackageId,
                        StartDate = DateTime.UtcNow,
                        EndDate = DateTime.UtcNow.AddDays(durationDays),
                        Status = SubscriptionStatus.Active
                    };
                    await _unitOfWork.UserSubscriptions.AddAsync(newSub);
                }
            }
            else
            {
                var newSub = new UserSubscription
                {
                    UserId = transaction.UserId,
                    PackageId = transaction.PackageId,
                    StartDate = DateTime.UtcNow,
                    EndDate = DateTime.UtcNow.AddDays(durationDays),
                    Status = SubscriptionStatus.Active
                };
                await _unitOfWork.UserSubscriptions.AddAsync(newSub);
            }

            await _auditLogService.LogAsync(new CreateAuditLogDto
            {
                UserId = transaction.UserId,
                Action = "CompletePaymentTransaction",
                Details = $"Thanh toán thành công gói {transaction.PackageId} ({durationDays} ngày) - Số tiền: {transaction.Amount:N0} VND. Mã VNPAY: {providerTxnId}"
            });
        }
        else
        {
            transaction.Status = status;
            transaction.ProviderTransactionId = providerTxnId;
            _unitOfWork.PaymentTransactions.Update(transaction);

            await _auditLogService.LogAsync(new CreateAuditLogDto
            {
                UserId = transaction.UserId,
                Action = "FailedPaymentTransaction",
                Details = $"Thanh toán thất bại gói {transaction.PackageId}. Mã giao dịch: {transactionId}"
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<PaymentTransaction?> GetTransactionByIdAsync(string transactionId)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
            return null;

        var transactions = await _unitOfWork.PaymentTransactions.FindAsync(t => t.Id == transactionId);
        return transactions.FirstOrDefault();
    }
}
