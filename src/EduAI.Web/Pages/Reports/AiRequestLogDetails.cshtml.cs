using EduAI.Model.Constants;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using EduAI.Model.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace EduAI.Web.Pages.Reports;

[Authorize(Roles = Roles.Admin)]
public class AiRequestLogDetailsModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    // appsettings: "AiRuntime:UsdToVndRate" → quy đổi EstimatedCostUsd sang ₫ trên trang chi tiết log.
    private readonly AiRuntimeSettings _aiRuntime;

    public AiRequestLogDetailsModel(IUnitOfWork unitOfWork, IOptions<AiRuntimeSettings> aiRuntime)
    {
        _unitOfWork = unitOfWork;
        _aiRuntime = aiRuntime.Value;
    }

    public AiUsageLog? Log { get; set; }
    public decimal UsdToVndRate { get; set; } = 25_000m;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        UsdToVndRate = _aiRuntime.UsdToVndRate > 0 ? _aiRuntime.UsdToVndRate : 25_000m;
        var logs = await _unitOfWork.AiUsageLogs.GetRecentAsync(null, 90);
        Log = logs.FirstOrDefault(l => l.Id == id);
        if (Log == null)
            return NotFound();
        return Page();
    }
}
