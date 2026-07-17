using EduAI.Model.DTOs;
using EduAI.Model.Entities;

namespace EduAI.BusinessLogic.IService;

public interface IPaymentService
{
    Task<IReadOnlyList<PaymentPackage>> GetPackagesAsync();
    Task<PaymentPackage?> GetPackageByIdAsync(string id);
    Task<UserSubscription?> GetActiveSubscriptionAsync(string userId);
    Task<ChatLimitResult> GetChatLimitResultAsync(string userId);
    Task<ProviderQuotaCheckResult> CheckProviderQuotaAsync(string userId, string providerId);
    Task<AiProviderQuotaOverviewDto> GetAiProviderQuotaOverviewAsync(string userId);
    Task<bool> CheckChatLimitAsync(string userId);
    Task<int> GetRemainingChatsAsync(string userId);
    Task<PaymentTransaction> CreateTransactionAsync(string userId, string packageId, decimal amount);
    Task<bool> CompleteTransactionAsync(string transactionId, string providerTxnId, string status);
    Task<PaymentTransaction?> GetTransactionByIdAsync(string transactionId);
}
