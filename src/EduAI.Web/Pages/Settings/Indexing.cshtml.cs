using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Settings;

[Authorize(Policy = "AdminOnly")]
public class IndexingModel : PageModel
{
    private readonly IIndexingSettingsService _indexingSettingsService;

    public IndexingModel(IIndexingSettingsService indexingSettingsService)
    {
        _indexingSettingsService = indexingSettingsService;
    }

    [BindProperty]
    public IndexingSettingsViewModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string? LastUpdatedBy { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadMetadataAsync();
            return Page();
        }

        if (Input.ChunkOverlap >= Input.ChunkSize)
        {
            ModelState.AddModelError(nameof(Input.ChunkOverlap), "Độ chồng lấp phải nhỏ hơn kích thước chunk.");
            await LoadMetadataAsync();
            return Page();
        }

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _indexingSettingsService.UpdateAsync(new Model.DTOs.UpdateIndexingSettingsDto
        {
            ChunkSize = Input.ChunkSize,
            ChunkOverlap = Input.ChunkOverlap
        }, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage ?? "Không thể lưu cấu hình.";
            await LoadMetadataAsync();
            return Page();
        }

        SuccessMessage = "Đã lưu cấu hình chia chunk. Tài liệu đã index trước đó cần được xử lý lại (re-index) để áp dụng.";
        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var settings = await _indexingSettingsService.GetAsync();
        Input.ChunkSize = settings.ChunkSize;
        Input.ChunkOverlap = settings.ChunkOverlap;
        LastUpdatedAt = settings.UpdatedAt;
        LastUpdatedBy = settings.UpdatedByName;
    }

    private async Task LoadMetadataAsync()
    {
        var settings = await _indexingSettingsService.GetAsync();
        LastUpdatedAt = settings.UpdatedAt;
        LastUpdatedBy = settings.UpdatedByName;
    }
}
