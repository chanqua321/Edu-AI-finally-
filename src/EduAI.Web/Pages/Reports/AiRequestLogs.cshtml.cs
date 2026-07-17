using System.Globalization;
using System.Text;
using EduAI.Model.Constants;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduAI.Web.Pages.Reports;

[Authorize(Roles = Roles.Admin)]
public class AiRequestLogsModel : PageModel
{
    private const int PageSize = 25;
    private readonly IUnitOfWork _unitOfWork;

    public AiRequestLogsModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Provider { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SubjectId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Sort { get; set; } = "newest";

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public IReadOnlyList<AiUsageLog> Logs { get; set; } = Array.Empty<AiUsageLog>();
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<SelectListItem> ProviderOptions { get; set; } = [];
    public List<SelectListItem> SubjectOptions { get; set; } = [];

    public async Task OnGetAsync()
    {
        var all = await LoadFilteredAsync();
        TotalCount = all.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
        Logs = all.Skip((PageNumber - 1) * PageSize).Take(PageSize).ToList();
        await FillFilterOptionsAsync();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var all = await LoadFilteredAsync();
        var sb = new StringBuilder();
        sb.AppendLine("Id,CreatedAt,Provider,Model,Subject,UserId,Operation,PromptTokens,CompletionTokens,TotalTokens,RetrievalMs,GenerationMs,TotalMs,IsSuccess,EstimatedCostUsd,ErrorMessage");
        foreach (var log in all)
        {
            sb.Append(Csv(log.Id.ToString())).Append(',')
                .Append(Csv(log.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))).Append(',')
                .Append(Csv(log.Provider)).Append(',')
                .Append(Csv(log.Model)).Append(',')
                .Append(Csv(log.Subject?.Name)).Append(',')
                .Append(Csv(log.UserId)).Append(',')
                .Append(Csv(log.Operation)).Append(',')
                .Append(log.PromptTokens).Append(',')
                .Append(log.CompletionTokens).Append(',')
                .Append(log.TotalTokens).Append(',')
                .Append(log.RetrievalTimeMs).Append(',')
                .Append(log.GenerationTimeMs).Append(',')
                .Append(log.TotalTimeMs).Append(',')
                .Append(log.IsSuccess ? "OK" : "Error").Append(',')
                .Append(log.EstimatedCostUsd.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Csv(log.ErrorMessage))
                .AppendLine();
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", $"ai-request-logs-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    private async Task<List<AiUsageLog>> LoadFilteredAsync()
    {
        var logs = (await _unitOfWork.AiUsageLogs.GetRecentAsync(null, 90))
            .Where(l => l.Operation != AiUsageOperations.DemoSeed
                && l.Operation != AiUsageOperations.Warmup)
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(Provider))
            logs = logs.Where(l => string.Equals(
                string.IsNullOrWhiteSpace(l.Provider) ? AiProviderIds.Gemini : l.Provider,
                Provider.Trim(),
                StringComparison.OrdinalIgnoreCase));

        if (SubjectId is int sid)
            logs = logs.Where(l => l.SubjectId == sid);

        if (!string.IsNullOrWhiteSpace(UserId))
            logs = logs.Where(l => !string.IsNullOrWhiteSpace(l.UserId)
                && l.UserId.Contains(UserId.Trim(), StringComparison.OrdinalIgnoreCase));

        if (string.Equals(Status, "ok", StringComparison.OrdinalIgnoreCase))
            logs = logs.Where(l => l.IsSuccess);
        else if (string.Equals(Status, "error", StringComparison.OrdinalIgnoreCase))
            logs = logs.Where(l => !l.IsSuccess);

        if (From.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(From.Value.Date, DateTimeKind.Local).ToUniversalTime();
            logs = logs.Where(l => l.CreatedAt >= fromUtc);
        }

        if (To.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(To.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            logs = logs.Where(l => l.CreatedAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(Q))
        {
            var q = Q.Trim();
            logs = logs.Where(l =>
                (l.ErrorMessage?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (l.Model?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (l.Provider?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (l.Subject?.Name?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (l.UserId?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || l.Id.ToString().Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        IEnumerable<AiUsageLog> ordered = string.Equals(Sort, "slowest", StringComparison.OrdinalIgnoreCase)
            ? logs.OrderByDescending(l => l.TotalTimeMs)
            : logs.OrderByDescending(l => l.CreatedAt);

        return ordered.ToList();
    }

    private async Task FillFilterOptionsAsync()
    {
        var recent = await _unitOfWork.AiUsageLogs.GetRecentAsync(null, 90);
        ProviderOptions = recent
            .Select(l => string.IsNullOrWhiteSpace(l.Provider) ? AiProviderIds.Gemini : l.Provider!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .Select(x => new SelectListItem(x, x, string.Equals(x, Provider, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        ProviderOptions.Insert(0, new SelectListItem("Tất cả provider", "", string.IsNullOrWhiteSpace(Provider)));

        SubjectOptions = (await _unitOfWork.Subjects.GetAllAsync())
            .OrderBy(s => s.Name)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString(), SubjectId == s.Id))
            .ToList();
        SubjectOptions.Insert(0, new SelectListItem("Tất cả môn", "", !SubjectId.HasValue));
    }

    private static string Csv(string? value)
    {
        value ??= string.Empty;
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
