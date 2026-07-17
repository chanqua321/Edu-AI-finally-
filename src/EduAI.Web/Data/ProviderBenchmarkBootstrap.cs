using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using Microsoft.AspNetCore.Identity;

namespace EduAI.Web.Data;

/// <summary>
/// Chạy cùng bộ câu hỏi RAG với Gemini rồi Ollama để có dữ liệu so sánh trên Benchmarks.
/// Usage: dotnet run -- --benchmark-providers
/// </summary>
public static class ProviderBenchmarkBootstrap
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var settingsService = sp.GetRequiredService<ISystemSettingsService>();
        var reports = sp.GetRequiredService<IReportService>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();

        var admin = await users.FindByEmailAsync("admin@gmail.com");
        var teacher = await users.FindByEmailAsync("teacher@gmail.com");
        var actorId = admin?.Id ?? teacher?.Id ?? string.Empty;
        if (string.IsNullOrEmpty(actorId))
        {
            Console.WriteLine("[benchmark-providers] Không tìm thấy admin/teacher để chạy.");
            return;
        }

        var original = await settingsService.GetDtoAsync();
        var providers = new[] { AiProviderIds.Gemini, AiProviderIds.Ollama };

        Console.WriteLine("[benchmark-providers] Bắt đầu tạo dữ liệu so sánh Gemini vs Ollama...");

        foreach (var provider in providers)
        {
            Console.WriteLine($"\n=== Provider: {provider} ===");
            var switchResult = await SetProviderAsync(settingsService, original, provider, actorId);
            if (!switchResult.Success)
            {
                Console.WriteLine($"[benchmark-providers] Không đổi được provider sang {provider}: {switchResult.ErrorMessage}");
                continue;
            }

            var (sessions, questions, error) = await reports.RunRealChatWarmupAsync(
                actorId,
                Roles.Admin,
                maxSubjects: 2,
                questionsPerSubject: 3);

            if (error != null)
                Console.WriteLine($"[benchmark-providers] {provider} FAIL: {error}");
            else if (questions == 0)
                Console.WriteLine($"[benchmark-providers] {provider} WARN: {sessions} sessions but 0 successful questions (check quota/API).");
            else
                Console.WriteLine($"[benchmark-providers] {provider} OK: {sessions} sessions, {questions} questions");
        }

        // Khôi phục provider cũ
        await SetProviderAsync(settingsService, original, original.GenerationProvider, actorId);
        Console.WriteLine($"\n[benchmark-providers] Xong. Provider đã trả về: {original.GenerationProvider}");
        Console.WriteLine("[benchmark-providers] Mở /Reports/Benchmarks để xem so sánh.");
    }

    private static Task<SystemSettingsOperationResultDto> SetProviderAsync(
        ISystemSettingsService settingsService,
        SystemSettingsDto current,
        string provider,
        string adminId)
    {
        var dto = new UpdateSystemSettingsDto
        {
            DefaultChunkSize = current.DefaultChunkSize,
            DefaultChunkOverlap = current.DefaultChunkOverlap,
            RetrievalTopK = current.RetrievalTopK,
            EnableCitation = current.EnableCitation,
            GenerationProvider = provider,
            EnableBenchmarkLogging = true,
            MaxUploadFileSizeBytes = current.MaxUploadFileSizeBytes,
            AllowedFileExtensions = current.AllowedFileExtensions,
            DefaultTimezone = current.DefaultTimezone,
            DailyQuotaResetHour = current.DailyQuotaResetHour,
            CountFailedRequestsAgainstQuota = current.CountFailedRequestsAgainstQuota,
            InputTokenPricePerMillion = current.InputTokenPricePerMillion,
            OutputTokenPricePerMillion = current.OutputTokenPricePerMillion,
            EmbeddingPricePerMillion = current.EmbeddingPricePerMillion,
            EnableLatencyLogging = true,
            EnableTokenLogging = true,
            EnableCostLogging = true
        };

        return settingsService.UpdateAsync(dto, adminId, null);
    }
}
