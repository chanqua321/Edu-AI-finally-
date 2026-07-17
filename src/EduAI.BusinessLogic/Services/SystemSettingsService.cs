using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.Enums;
using EduAI.Model.IRepository;
using EduAI.Model.Settings;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace EduAI.BusinessLogic.Services;

public class SystemSettingsService : ISystemSettingsService
{
    private const string CacheKey = "system_settings_singleton";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private const int MinChunkSize = 100;
    private const int MaxChunkSize = 10000;
    private const int MinChunkOverlap = 0;
    private const int MaxChunkOverlap = 5000;
    private const int MinTopK = 1;
    private const int MaxTopK = 50;
    private const long MinUploadBytes = 1024;
    private const long MaxUploadBytes = 524_288_000;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly IMemoryCache _cache;
    // appsettings: "AiRuntime" → đồng bộ model/temperature từ config vào bảng SystemSettings trong DB.
    private readonly AiRuntimeSettings _aiRuntime;

    public SystemSettingsService(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        IMemoryCache cache,
        IOptions<AiRuntimeSettings> aiRuntime)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _cache = cache;
        _aiRuntime = aiRuntime.Value;
    }

    public async Task<SystemSettings> GetAsync()
    {
        if (_cache.TryGetValue(CacheKey, out SystemSettings? cached) && cached != null)
            return cached;

        var settings = await EnsureExistsAsync();
        _cache.Set(CacheKey, settings, CacheDuration);
        return settings;
    }

    public async Task<SystemSettingsDto> GetDtoAsync()
    {
        var settings = await GetAsync();
        return MapToDto(settings);
    }

    public async Task<SystemSettingsOperationResultDto> UpdateAsync(
        UpdateSystemSettingsDto dto, string adminId, string? ipAddress)
    {
        var validationError = Validate(dto);
        if (validationError != null)
            return new SystemSettingsOperationResultDto { Success = false, ErrorMessage = validationError };

        var settings = await EnsureExistsAsync();

        var oldValues = $"ChunkSize={settings.DefaultChunkSize}, ChunkOverlap={settings.DefaultChunkOverlap}, " +
                        $"TopK={settings.RetrievalTopK}, Citation={settings.EnableCitation}, " +
                        $"Provider={settings.GenerationProvider}, MaxUpload={settings.MaxUploadFileSizeBytes}";

        // Admin-tunable RAG + generation provider
        settings.DefaultChunkSize = dto.DefaultChunkSize;
        settings.DefaultChunkOverlap = dto.DefaultChunkOverlap;
        settings.RetrievalTopK = dto.RetrievalTopK;
        settings.EnableCitation = dto.EnableCitation;
        settings.GenerationProvider = AiProviderIds.Normalize(dto.GenerationProvider);
        settings.EnableBenchmarkLogging = dto.EnableBenchmarkLogging;
        settings.MaxUploadFileSizeBytes = dto.MaxUploadFileSizeBytes;
        settings.AllowedFileExtensions = dto.AllowedFileExtensions.Trim();
        settings.DefaultTimezone = dto.DefaultTimezone.Trim();
        settings.DailyQuotaResetHour = dto.DailyQuotaResetHour;
        settings.CountFailedRequestsAgainstQuota = dto.CountFailedRequestsAgainstQuota;
        settings.InputTokenPricePerMillion = dto.InputTokenPricePerMillion;
        settings.OutputTokenPricePerMillion = dto.OutputTokenPricePerMillion;
        settings.EmbeddingPricePerMillion = dto.EmbeddingPricePerMillion;
        settings.EnableLatencyLogging = dto.EnableLatencyLogging;
        settings.EnableTokenLogging = dto.EnableTokenLogging;
        settings.EnableCostLogging = dto.EnableCostLogging;

        // Keep fixed LLM fields in sync with application config (not admin-editable)
        ApplyFixedAiRuntime(settings);

        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByUserId = string.IsNullOrEmpty(adminId) ? null : adminId;

        _unitOfWork.SystemSettings.Update(settings);
        await _unitOfWork.SaveChangesAsync();

        _cache.Set(CacheKey, settings, CacheDuration);

        var newValues = $"ChunkSize={settings.DefaultChunkSize}, ChunkOverlap={settings.DefaultChunkOverlap}, " +
                        $"TopK={settings.RetrievalTopK}, Citation={settings.EnableCitation}, " +
                        $"Provider={settings.GenerationProvider}, MaxUpload={settings.MaxUploadFileSizeBytes}";

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.UpdateSystemSettings,
            IpAddress = ipAddress,
            Details = $"Cập nhật cấu hình hệ thống. Cũ: [{oldValues}] → Mới: [{newValues}]"
        });

        return new SystemSettingsOperationResultDto { Success = true, Settings = MapToDto(settings) };
    }

    public Task RefreshCacheAsync()
    {
        _cache.Remove(CacheKey);
        return Task.CompletedTask;
    }

    public async Task SeedDefaultAsync(string? embeddingModel = null, string? generationModel = null)
    {
        var existing = await _unitOfWork.SystemSettings.GetAsync();
        if (existing != null)
            return;

        var defaults = new SystemSettings
        {
            DefaultChunkSize = 800,
            DefaultChunkOverlap = 120,
            RetrievalTopK = 5,
            EnableCitation = true,
            GenerationProvider = AiProviderIds.Gemini,
            EnableBenchmarkLogging = true,
            MaxUploadFileSizeBytes = 52_428_800,
            AllowedFileExtensions = ".pdf,.docx,.pptx,.txt",
            DefaultTimezone = "UTC",
            DailyQuotaResetHour = 0,
            CountFailedRequestsAgainstQuota = false,
            InputTokenPricePerMillion = 0.075m,
            OutputTokenPricePerMillion = 0.30m,
            EmbeddingPricePerMillion = 0.01m,
            EnableLatencyLogging = true,
            EnableTokenLogging = true,
            EnableCostLogging = true,
            UpdatedAt = DateTime.UtcNow
        };
        ApplyFixedAiRuntime(defaults);
        if (!string.IsNullOrWhiteSpace(embeddingModel))
            defaults.DefaultEmbeddingModel = embeddingModel.Trim();
        if (!string.IsNullOrWhiteSpace(generationModel))
            defaults.DefaultGenerationModel = generationModel.Trim();

        await _unitOfWork.SystemSettings.AddAsync(defaults);
        await _unitOfWork.SaveChangesAsync();
    }

    // appsettings: "AiRuntime" → ghi đè Temperature/MaxChatHistory/GenerationModel vào SystemSettings khi admin lưu.
    private void ApplyFixedAiRuntime(SystemSettings settings)
    {
        settings.DefaultChunkMode = ChunkMode.Character;
        settings.Temperature = _aiRuntime.Temperature;
        settings.MaxChatHistory = _aiRuntime.MaxChatHistory;
        settings.MaxOutputTokens = _aiRuntime.MaxOutputTokens;
        settings.DefaultEmbeddingModel = string.IsNullOrWhiteSpace(_aiRuntime.EmbeddingModel)
            ? "gemini-embedding-001"
            : _aiRuntime.EmbeddingModel.Trim();
        settings.DefaultGenerationModel = string.IsNullOrWhiteSpace(_aiRuntime.GenerationModel)
            ? "gemini-3-flash-preview"
            : _aiRuntime.GenerationModel.Trim();
    }

    private async Task<SystemSettings> EnsureExistsAsync()
    {
        var existing = await _unitOfWork.SystemSettings.GetAsync();
        if (existing != null)
            return existing;

        await SeedDefaultAsync();
        return (await _unitOfWork.SystemSettings.GetAsync())!;
    }

    private static SystemSettingsDto MapToDto(SystemSettings s) => new()
    {
        DefaultChunkSize = s.DefaultChunkSize,
        DefaultChunkOverlap = s.DefaultChunkOverlap,
        RetrievalTopK = s.RetrievalTopK,
        EnableCitation = s.EnableCitation,
        GenerationProvider = AiProviderIds.Normalize(s.GenerationProvider),
        EnableBenchmarkLogging = s.EnableBenchmarkLogging,
        MaxUploadFileSizeBytes = s.MaxUploadFileSizeBytes,
        AllowedFileExtensions = s.AllowedFileExtensions,
        DefaultTimezone = s.DefaultTimezone,
        DailyQuotaResetHour = s.DailyQuotaResetHour,
        CountFailedRequestsAgainstQuota = s.CountFailedRequestsAgainstQuota,
        InputTokenPricePerMillion = s.InputTokenPricePerMillion,
        OutputTokenPricePerMillion = s.OutputTokenPricePerMillion,
        EmbeddingPricePerMillion = s.EmbeddingPricePerMillion,
        EnableLatencyLogging = s.EnableLatencyLogging,
        EnableTokenLogging = s.EnableTokenLogging,
        EnableCostLogging = s.EnableCostLogging,
        UpdatedAt = s.UpdatedAt,
        UpdatedByName = s.UpdatedBy?.FullName ?? s.UpdatedBy?.Email
    };

    private static string? Validate(UpdateSystemSettingsDto dto)
    {
        if (dto.DefaultChunkSize < MinChunkSize || dto.DefaultChunkSize > MaxChunkSize)
            return $"Kích thước chunk phải từ {MinChunkSize} đến {MaxChunkSize}.";

        if (dto.DefaultChunkOverlap < MinChunkOverlap || dto.DefaultChunkOverlap > MaxChunkOverlap)
            return $"Độ chồng lấp phải từ {MinChunkOverlap} đến {MaxChunkOverlap}.";

        if (dto.DefaultChunkOverlap >= dto.DefaultChunkSize)
            return "Độ chồng lấp phải nhỏ hơn kích thước chunk.";

        if (dto.RetrievalTopK < MinTopK || dto.RetrievalTopK > MaxTopK)
            return $"RetrievalTopK phải từ {MinTopK} đến {MaxTopK}.";

        if (!AiProviderIds.IsValid(dto.GenerationProvider) && !string.IsNullOrWhiteSpace(dto.GenerationProvider))
            return "Generation Provider không hợp lệ (Gemini hoặc Ollama).";

        if (dto.MaxUploadFileSizeBytes < MinUploadBytes || dto.MaxUploadFileSizeBytes > MaxUploadBytes)
            return "Kích thước upload tối đa phải từ 1 KB đến 500 MB.";

        if (string.IsNullOrWhiteSpace(dto.AllowedFileExtensions))
            return "Danh sách đuôi file không được để trống.";

        if (dto.InputTokenPricePerMillion < 0)
            return "Giá token input không được âm.";

        if (dto.OutputTokenPricePerMillion < 0)
            return "Giá token output không được âm.";

        if (dto.EmbeddingPricePerMillion < 0)
            return "Giá token embedding không được âm.";

        if (dto.DailyQuotaResetHour < 0 || dto.DailyQuotaResetHour > 23)
            return "Giờ reset quota phải từ 0 đến 23.";

        return null;
    }
}
