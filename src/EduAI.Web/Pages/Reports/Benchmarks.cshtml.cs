using System.Text.Json;
using EduAI.Model.Constants;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using EduAI.Model.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace EduAI.Web.Pages.Reports;

[Authorize(Roles = Roles.Admin)]
public class BenchmarksModel : PageModel
{
    private readonly IUnitOfWork _unitOfWork;
    // appsettings: "AiRuntime:UsdToVndRate" → quy đổi chi phí USD sang ₫ trên dashboard Benchmark.
    private readonly AiRuntimeSettings _aiRuntime;

    public BenchmarksModel(IUnitOfWork unitOfWork, IOptions<AiRuntimeSettings> aiRuntime)
    {
        _unitOfWork = unitOfWork;
        _aiRuntime = aiRuntime.Value;
    }

    public decimal UsdToVndRate { get; set; } = 25_000m;

    public int TotalQueries { get; set; }
    public int SuccessQueries { get; set; }
    public int FailedQueries { get; set; }
    public double SuccessRate { get; set; }

    public double AvgRetrievalMs { get; set; }
    public double AvgGenerationMs { get; set; }
    public double AvgTotalMs { get; set; }

    public List<ProviderBenchmarkRow> ProviderStats { get; set; } = [];
    public List<AiUsageLog> SlowestRequests { get; set; } = [];
    public List<AiUsageLog> LatestRequests { get; set; } = [];

    // Chart JSON payloads
    public string ProviderLabelsJson { get; set; } = "[]";
    public string ProviderAvgTotalJson { get; set; } = "[]";
    public string ProviderSuccessRateJson { get; set; } = "[]";
    public string ProviderRequestCountJson { get; set; } = "[]";
    public string ProviderErrorRateJson { get; set; } = "[]";

    public string DailyLabelsJson { get; set; } = "[]";
    public string DailyCostJson { get; set; } = "[]";
    public string DailySuccessRateJson { get; set; } = "[]";
    public string DailyAvgTotalJson { get; set; } = "[]";

    public string ErrorLabelsJson { get; set; } = "[]";
    public string ErrorValuesJson { get; set; } = "[]";

    public string RetrievalLabelsJson { get; set; } = "[]";
    public string RetrievalValuesJson { get; set; } = "[]";

    public string CostByCourseLabelsJson { get; set; } = "[]";
    public string CostByCourseValuesJson { get; set; } = "[]";

    public async Task OnGetAsync()
    {
        UsdToVndRate = _aiRuntime.UsdToVndRate > 0 ? _aiRuntime.UsdToVndRate : 25_000m;

        var logs = (await _unitOfWork.AiUsageLogs.GetRecentAsync(null, 30))
            .Where(l => l.Operation != AiUsageOperations.DemoSeed
                && l.Operation != AiUsageOperations.Warmup)
            .ToList();

        TotalQueries = logs.Count;
        SuccessQueries = logs.Count(l => l.IsSuccess);
        FailedQueries = logs.Count(l => !l.IsSuccess);
        SuccessRate = TotalQueries > 0 ? SuccessQueries * 100.0 / TotalQueries : 0;

        var successful = logs.Where(l => l.IsSuccess).ToList();
        if (successful.Count > 0)
        {
            AvgRetrievalMs = successful.Average(l => l.RetrievalTimeMs);
            AvgGenerationMs = successful.Average(l => l.GenerationTimeMs);
            AvgTotalMs = successful.Average(l => l.TotalTimeMs);
        }

        ProviderStats = logs
            .GroupBy(l => NormalizeProvider(l.Provider))
            .Select(g =>
            {
                var success = g.Where(x => x.IsSuccess).ToList();
                var model = g
                    .Where(x => !string.IsNullOrWhiteSpace(x.Model))
                    .GroupBy(x => x.Model!)
                    .OrderByDescending(m => m.Count())
                    .Select(m => m.Key)
                    .FirstOrDefault() ?? "—";

                return new ProviderBenchmarkRow
                {
                    Provider = g.Key,
                    Model = model,
                    TotalRequests = g.Count(),
                    SuccessCount = success.Count,
                    SuccessRate = g.Count() > 0 ? Math.Round(success.Count * 100.0 / g.Count(), 1) : 0,
                    ErrorRate = g.Count() > 0 ? Math.Round((g.Count() - success.Count) * 100.0 / g.Count(), 1) : 0,
                    AvgTotalMs = success.Count > 0 ? Math.Round(success.Average(x => x.TotalTimeMs), 1) : 0,
                    AvgGenerationMs = success.Count > 0 ? Math.Round(success.Average(x => x.GenerationTimeMs), 1) : 0,
                    AvgRetrievalMs = success.Count > 0 ? Math.Round(success.Average(x => x.RetrievalTimeMs), 1) : 0,
                    AvgTokens = g.Count() > 0 ? Math.Round(g.Average(x => (double)x.TotalTokens), 1) : 0,
                    EstimatedCostUsd = Math.Round(g.Sum(x => x.EstimatedCostUsd), 6)
                };
            })
            .OrderBy(p => p.Provider)
            .ToList();

        var daily = logs
            .GroupBy(l => l.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var ok = g.Where(x => x.IsSuccess).ToList();
                return new
                {
                    Label = g.Key.ToString("dd/MM"),
                    CostVnd = Math.Round(g.Sum(x => x.EstimatedCostUsd) * UsdToVndRate, 0),
                    SuccessRate = g.Count() > 0 ? Math.Round(ok.Count * 100.0 / g.Count(), 1) : 0,
                    AvgTotal = ok.Count > 0 ? Math.Round(ok.Average(x => x.TotalTimeMs), 1) : 0
                };
            })
            .TakeLast(14)
            .ToList();

        DailyLabelsJson = JsonSerializer.Serialize(daily.Select(d => d.Label));
        DailyCostJson = JsonSerializer.Serialize(daily.Select(d => d.CostVnd));
        DailySuccessRateJson = JsonSerializer.Serialize(daily.Select(d => d.SuccessRate));
        DailyAvgTotalJson = JsonSerializer.Serialize(daily.Select(d => d.AvgTotal));

        var errorBuckets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Timeout"] = 0,
            ["API Error"] = 0,
            ["No Context"] = 0,
            ["Unknown Error"] = 0
        };
        foreach (var fail in logs.Where(l => !l.IsSuccess))
        {
            var bucket = ClassifyError(fail.ErrorMessage);
            errorBuckets[bucket] = errorBuckets.GetValueOrDefault(bucket) + 1;
        }

        ErrorLabelsJson = JsonSerializer.Serialize(errorBuckets.Keys);
        ErrorValuesJson = JsonSerializer.Serialize(errorBuckets.Values);

        var retrievedOk = logs.Count(l => l.IsSuccess && l.RetrievalTimeMs > 0);
        var noMatch = logs.Count(l =>
            (!l.IsSuccess && IsNoContextError(l.ErrorMessage))
            || (l.IsSuccess && l.RetrievalTimeMs == 0 && l.GenerationTimeMs > 0));
        // Remaining successful with retrieval already counted; pad no-match if empty but we have docs issues
        if (retrievedOk == 0 && noMatch == 0 && logs.Count > 0)
        {
            retrievedOk = SuccessQueries;
            noMatch = FailedQueries;
        }

        RetrievalLabelsJson = JsonSerializer.Serialize(new[] { "Truy xuất thành công", "Không khớp / không ngữ liệu" });
        RetrievalValuesJson = JsonSerializer.Serialize(new[] { retrievedOk, noMatch });

        var costByCourse = logs
            .GroupBy(l => l.Subject?.Name ?? "N/A")
            .Select(g => new { Name = g.Key, CostVnd = Math.Round(g.Sum(x => x.EstimatedCostUsd) * UsdToVndRate, 0) })
            .Where(x => x.CostVnd > 0)
            .OrderByDescending(x => x.CostVnd)
            .Take(8)
            .ToList();
        if (costByCourse.Count == 0)
        {
            costByCourse = logs
                .GroupBy(l => l.Subject?.Name ?? "N/A")
                .Select(g => new { Name = g.Key, CostVnd = (decimal)g.Count() })
                .OrderByDescending(x => x.CostVnd)
                .Take(8)
                .ToList();
        }

        CostByCourseLabelsJson = JsonSerializer.Serialize(costByCourse.Select(x => x.Name));
        CostByCourseValuesJson = JsonSerializer.Serialize(costByCourse.Select(x => x.CostVnd));

        SlowestRequests = successful
            .OrderByDescending(l => l.TotalTimeMs)
            .Take(5)
            .ToList();

        LatestRequests = logs
            .OrderByDescending(l => l.CreatedAt)
            .Take(5)
            .ToList();

        ProviderLabelsJson = JsonSerializer.Serialize(ProviderStats.Select(p => p.Provider));
        ProviderAvgTotalJson = JsonSerializer.Serialize(ProviderStats.Select(p => p.AvgTotalMs));
        ProviderSuccessRateJson = JsonSerializer.Serialize(ProviderStats.Select(p => p.SuccessRate));
        ProviderRequestCountJson = JsonSerializer.Serialize(ProviderStats.Select(p => p.TotalRequests));
        ProviderErrorRateJson = JsonSerializer.Serialize(ProviderStats.Select(p => p.ErrorRate));
    }

    private static string NormalizeProvider(string? provider) =>
        string.IsNullOrWhiteSpace(provider) ? AiProviderIds.Gemini : provider.Trim();

    private static string ClassifyError(string? message)
    {
        var m = (message ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(m))
            return "Unknown Error";

        if (m.Contains("timeout") || m.Contains("timed out") || m.Contains("hết thời gian"))
            return "Timeout";

        if (IsNoContextError(message))
            return "No Context";

        if (m.Contains("api") || m.Contains("http") || m.Contains("429") || m.Contains("401")
            || m.Contains("403") || m.Contains("500") || m.Contains("gemini") || m.Contains("ollama")
            || m.Contains("thất bại") || m.Contains("failed") || m.Contains("quota")
            || m.Contains("resource_exhausted") || m.Contains("connection"))
            return "API Error";

        return "Unknown Error";
    }

    private static bool IsNoContextError(string? message)
    {
        var m = (message ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(m))
            return false;

        return m.Contains("không tìm thấy")
            || m.Contains("rỗng")
            || m.Contains("no context")
            || m.Contains("no matching")
            || m.Contains("empty")
            || m.Contains("không có tài liệu")
            || m.Contains("not found");
    }

    public class ProviderBenchmarkRow
    {
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int SuccessCount { get; set; }
        public double SuccessRate { get; set; }
        public double ErrorRate { get; set; }
        public double AvgTotalMs { get; set; }
        public double AvgGenerationMs { get; set; }
        public double AvgRetrievalMs { get; set; }
        public double AvgTokens { get; set; }
        public decimal EstimatedCostUsd { get; set; }
    }
}
