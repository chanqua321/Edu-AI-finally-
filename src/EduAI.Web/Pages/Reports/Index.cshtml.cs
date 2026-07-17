using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Reports;

[Authorize(Roles = Roles.Admin)]
public class IndexModel : PageModel
{
    private readonly IReportService _reportService;

    public IndexModel(IReportService reportService)
    {
        _reportService = reportService;
    }

    public ReportDashboardDto Dashboard { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string Period { get; set; } = ReportPeriods.Month;

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? To { get; set; }

    public string? StatusMessage { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsAdmin => User.IsInRole(Roles.Admin);

    public async Task OnGetAsync()
    {
        Period = ReportPeriods.Normalize(Period);
        if (Period == ReportPeriods.Custom && From.HasValue && To.HasValue)
        {
            // keep custom
        }
        else if (Period != ReportPeriods.Custom)
        {
            From = null;
            To = null;
        }

        Dashboard = await LoadDashboardAsync();
    }

    public async Task<IActionResult> OnPostWarmupAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var (sessions, questions, error) = await _reportService.RunRealChatWarmupAsync(userId, role);
        if (error != null)
            TempData["ReportError"] = error;
        else
            TempData["ReportSuccess"] = $"Đã chạy chat RAG thật: {sessions} phiên, {questions} câu hỏi.";

        return RedirectToPage(new { Period, From, To });
    }

    private async Task<ReportDashboardDto> LoadDashboardAsync()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        DateTime? fromUtc = null;
        DateTime? toUtc = null;
        if (Period == ReportPeriods.Custom)
        {
            if (From.HasValue)
                fromUtc = DateTime.SpecifyKind(From.Value.Date, DateTimeKind.Local).ToUniversalTime();
            if (To.HasValue)
                toUtc = DateTime.SpecifyKind(To.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local).ToUniversalTime();
            if (!fromUtc.HasValue || !toUtc.HasValue)
                Period = ReportPeriods.Month;
        }

        return await _reportService.GetDashboardAsync(userId, role, new ReportFilterDto
        {
            Period = Period,
            FromUtc = fromUtc,
            ToUtc = toUtc
        });
    }
}
