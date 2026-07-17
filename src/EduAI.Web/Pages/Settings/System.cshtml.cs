using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Settings;

[Authorize(Roles = Roles.Admin)]
public class SystemModel : PageModel
{
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly IUnitOfWork _unitOfWork;

    public SystemModel(
        ISystemSettingsService systemSettingsService,
        IUnitOfWork unitOfWork)
    {
        _systemSettingsService = systemSettingsService;
        _unitOfWork = unitOfWork;
    }

    [BindProperty]
    public SystemSettingsViewModel Input { get; set; } = new();

    [BindProperty]
    public PaymentPackageEditViewModel PackageEdit { get; set; } = new();

    [BindProperty]
    public PaymentPackageCreateViewModel PackageCreate { get; set; } = new() { DurationDays = 30, IsActive = true, DisplayOrder = 10 };

    public IReadOnlyList<PaymentPackage> Packages { get; set; } = Array.Empty<PaymentPackage>();

    public string ActiveSection { get; set; } = "ai";

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync(string? section, string? saved)
    {
        ActiveSection = NormalizeSection(section);
        if (!string.IsNullOrEmpty(saved))
            SuccessMessage = saved;
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostSaveAiAsync(string? section)
    {
        ActiveSection = "ai";
        var update = await GetCurrentUpdateDtoAsync();
        update.DefaultChunkSize = Input.DefaultChunkSize;
        update.DefaultChunkOverlap = Input.DefaultChunkOverlap;
        update.RetrievalTopK = Input.RetrievalTopK;
        update.EnableCitation = Input.EnableCitation;
        update.GenerationProvider = Input.GenerationProvider;

        return await SaveAsync(update, "Đã lưu cấu hình AI & RAG.");
    }

    public async Task<IActionResult> OnPostSaveUploadAsync(string? section)
    {
        ActiveSection = "upload";
        var update = await GetCurrentUpdateDtoAsync();
        update.MaxUploadFileSizeBytes = Input.MaxUploadFileSizeMb * 1024 * 1024;
        update.AllowedFileExtensions = Input.AllowedFileExtensions;

        return await SaveAsync(update, "Đã lưu cấu hình upload.");
    }

    public async Task<IActionResult> OnPostSaveChatAsync(string? section)
    {
        ActiveSection = "chat";
        var update = await GetCurrentUpdateDtoAsync();
        update.DefaultTimezone = Input.DefaultTimezone;
        update.DailyQuotaResetHour = Input.DailyQuotaResetHour;
        update.CountFailedRequestsAgainstQuota = Input.CountFailedRequestsAgainstQuota;

        return await SaveAsync(update, "Đã lưu cấu hình chat & quota.");
    }

    public async Task<IActionResult> OnPostSaveBenchmarkAsync(string? section)
    {
        ActiveSection = "benchmark";
        var update = await GetCurrentUpdateDtoAsync();
        update.EnableBenchmarkLogging = Input.EnableBenchmarkLogging;
        update.EnableLatencyLogging = Input.EnableLatencyLogging;
        update.EnableTokenLogging = Input.EnableTokenLogging;
        update.EnableCostLogging = Input.EnableCostLogging;

        return await SaveAsync(update, "Đã lưu cấu hình benchmark & logging.");
    }

    public async Task<IActionResult> OnPostSaveGeneralAsync(string? section)
    {
        ActiveSection = "general";
        var update = await GetCurrentUpdateDtoAsync();
        update.InputTokenPricePerMillion = Input.InputTokenPricePerMillion;
        update.OutputTokenPricePerMillion = Input.OutputTokenPricePerMillion;
        update.EmbeddingPricePerMillion = Input.EmbeddingPricePerMillion;

        return await SaveAsync(update, "Đã lưu cấu hình chung.");
    }

    public async Task<IActionResult> OnPostUpdatePackageAsync(string? section)
    {
        ActiveSection = "payment";
        await LoadAsync();

        var packages = await _unitOfWork.PaymentPackages.FindAsync(p => p.Id == PackageEdit.Id);
        var package = packages.FirstOrDefault();
        if (package == null)
        {
            ErrorMessage = "Không tìm thấy gói cước.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(PackageEdit.Name))
        {
            ErrorMessage = "Tên gói cước không được để trống.";
            return Page();
        }

        if (PackageEdit.Price < 0 || PackageEdit.DurationDays < 0
            || PackageEdit.MonthlyGeminiQuestions < 0 || PackageEdit.DailyOllamaQuestions < 0)
        {
            ErrorMessage = "Giá trị gói cước không hợp lệ.";
            return Page();
        }

        package.Name = PackageEdit.Name.Trim();
        package.Description = PackageEdit.Description?.Trim() ?? string.Empty;
        package.Price = PackageEdit.Price;
        package.DurationDays = PackageEdit.DurationDays;
        package.MonthlyGeminiQuestions = PackageEdit.MonthlyGeminiQuestions;
        package.DailyOllamaQuestions = PackageEdit.DailyOllamaQuestions;
        // Legacy field kept in sync for old reports/UI; enforcement uses provider quotas.
        package.MaxDailyQuestions = PackageEdit.DailyOllamaQuestions;
        package.DisplayOrder = PackageEdit.DisplayOrder;
        package.IsRecommended = PackageEdit.IsRecommended;
        package.IsActive = PackageEdit.IsActive;

        _unitOfWork.PaymentPackages.Update(package);
        await _unitOfWork.SaveChangesAsync();

        return RedirectToPage(new { section = "payment", saved = $"Đã cập nhật gói {package.Name}." });
    }

    public async Task<IActionResult> OnPostCreatePackageAsync(string? section)
    {
        ActiveSection = "payment";
        await LoadAsync();

        if (string.IsNullOrWhiteSpace(PackageCreate.Name))
        {
            ErrorMessage = "Tên gói cước không được để trống khi tạo.";
            return Page();
        }

        if (PackageCreate.Price < 0 || PackageCreate.DurationDays < 0
            || PackageCreate.MonthlyGeminiQuestions < 0 || PackageCreate.DailyOllamaQuestions < 0)
        {
            ErrorMessage = "Giá trị gói cước không hợp lệ khi tạo.";
            return Page();
        }

        var id = string.IsNullOrWhiteSpace(PackageCreate.Id)
            ? BuildPackageIdFromName(PackageCreate.Name)
            : SanitizePackageId(PackageCreate.Id);

        if (string.IsNullOrWhiteSpace(id))
        {
            ErrorMessage = "Mã gói không hợp lệ. Dùng chữ/số/gạch ngang (VD: StudentPlus).";
            return Page();
        }

        var existing = await _unitOfWork.PaymentPackages.FindAsync(p => p.Id == id);
        if (existing.Count > 0)
        {
            ErrorMessage = $"Mã gói \"{id}\" đã tồn tại. Hãy chọn mã khác khi tạo.";
            return Page();
        }

        var duration = PackageCreate.DurationDays == 0 ? 99999 : PackageCreate.DurationDays;
        var package = new PaymentPackage
        {
            Id = id,
            Name = PackageCreate.Name.Trim(),
            Description = PackageCreate.Description?.Trim() ?? string.Empty,
            Price = PackageCreate.Price,
            DurationDays = duration,
            MonthlyGeminiQuestions = PackageCreate.MonthlyGeminiQuestions,
            DailyOllamaQuestions = PackageCreate.DailyOllamaQuestions,
            MaxDailyQuestions = PackageCreate.DailyOllamaQuestions,
            DisplayOrder = PackageCreate.DisplayOrder,
            IsRecommended = PackageCreate.IsRecommended,
            IsActive = PackageCreate.IsActive
        };

        await _unitOfWork.PaymentPackages.AddAsync(package);
        await _unitOfWork.SaveChangesAsync();

        return RedirectToPage(new { section = "payment", saved = $"Đã tạo gói {package.Name} ({package.Id})." });
    }

    private static string BuildPackageIdFromName(string name)
    {
        var ascii = new string(name.Normalize(System.Text.NormalizationForm.FormD)
            .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch)
                != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray())
            .Normalize(System.Text.NormalizationForm.FormC);

        var chars = ascii
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : (ch is ' ' or '-' or '_' ? '-' : '\0'))
            .Where(ch => ch != '\0')
            .ToArray();

        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);

        return SanitizePackageId(slug.Trim('-'));
    }

    private static string SanitizePackageId(string raw)
    {
        var cleaned = new string(raw.Trim()
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
            .ToArray());
        if (cleaned.Length > 64)
            cleaned = cleaned[..64];
        return cleaned;
    }

    private async Task<IActionResult> SaveAsync(UpdateSystemSettingsDto update, string successMessage)
    {
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _systemSettingsService.UpdateAsync(update, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage ?? "Không thể lưu cấu hình.";
            await LoadAsync();
            return Page();
        }

        return RedirectToPage(new { section = ActiveSection, saved = successMessage });
    }

    private async Task<UpdateSystemSettingsDto> GetCurrentUpdateDtoAsync()
    {
        var dto = await _systemSettingsService.GetDtoAsync();
        return new UpdateSystemSettingsDto
        {
            DefaultChunkSize = dto.DefaultChunkSize,
            DefaultChunkOverlap = dto.DefaultChunkOverlap,
            RetrievalTopK = dto.RetrievalTopK,
            EnableCitation = dto.EnableCitation,
            GenerationProvider = dto.GenerationProvider,
            EnableBenchmarkLogging = dto.EnableBenchmarkLogging,
            MaxUploadFileSizeBytes = dto.MaxUploadFileSizeBytes,
            AllowedFileExtensions = dto.AllowedFileExtensions,
            DefaultTimezone = dto.DefaultTimezone,
            DailyQuotaResetHour = dto.DailyQuotaResetHour,
            CountFailedRequestsAgainstQuota = dto.CountFailedRequestsAgainstQuota,
            InputTokenPricePerMillion = dto.InputTokenPricePerMillion,
            OutputTokenPricePerMillion = dto.OutputTokenPricePerMillion,
            EmbeddingPricePerMillion = dto.EmbeddingPricePerMillion,
            EnableLatencyLogging = dto.EnableLatencyLogging,
            EnableTokenLogging = dto.EnableTokenLogging,
            EnableCostLogging = dto.EnableCostLogging
        };
    }

    private async Task LoadAsync()
    {
        var dto = await _systemSettingsService.GetDtoAsync();
        Input = new SystemSettingsViewModel
        {
            DefaultChunkSize = dto.DefaultChunkSize,
            DefaultChunkOverlap = dto.DefaultChunkOverlap,
            RetrievalTopK = dto.RetrievalTopK,
            EnableCitation = dto.EnableCitation,
            GenerationProvider = dto.GenerationProvider,
            EnableBenchmarkLogging = dto.EnableBenchmarkLogging,
            MaxUploadFileSizeMb = dto.MaxUploadFileSizeBytes / (1024 * 1024),
            AllowedFileExtensions = dto.AllowedFileExtensions,
            DefaultTimezone = dto.DefaultTimezone,
            DailyQuotaResetHour = dto.DailyQuotaResetHour,
            CountFailedRequestsAgainstQuota = dto.CountFailedRequestsAgainstQuota,
            InputTokenPricePerMillion = dto.InputTokenPricePerMillion,
            OutputTokenPricePerMillion = dto.OutputTokenPricePerMillion,
            EmbeddingPricePerMillion = dto.EmbeddingPricePerMillion,
            EnableLatencyLogging = dto.EnableLatencyLogging,
            EnableTokenLogging = dto.EnableTokenLogging,
            EnableCostLogging = dto.EnableCostLogging,
            UpdatedAt = dto.UpdatedAt,
            UpdatedByName = dto.UpdatedByName
        };

        var allPackages = await _unitOfWork.PaymentPackages.GetAllAsync();
        Packages = allPackages.OrderBy(p => p.DisplayOrder).ToList();
    }

    private static string NormalizeSection(string? section) => section?.ToLowerInvariant() switch
    {
        "upload" => "upload",
        "chat" => "chat",
        "payment" => "payment",
        "benchmark" => "benchmark",
        "general" => "general",
        _ => "ai"
    };
}

public class PaymentPackageEditViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int MaxDailyQuestions { get; set; }
    public int MonthlyGeminiQuestions { get; set; }
    public int DailyOllamaQuestions { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsRecommended { get; set; }
    public bool IsActive { get; set; }
}

public class PaymentPackageCreateViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; } = 30;
    public int MonthlyGeminiQuestions { get; set; }
    public int DailyOllamaQuestions { get; set; }
    public int DisplayOrder { get; set; } = 10;
    public bool IsRecommended { get; set; }
    public bool IsActive { get; set; } = true;
}
