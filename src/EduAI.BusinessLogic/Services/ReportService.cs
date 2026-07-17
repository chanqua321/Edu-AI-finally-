using System.Text.Json;
using EduAI.BusinessLogic.IService;
using EduAI.Model;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.Enums;
using EduAI.Model.IRepository;
using EduAI.Model.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduAI.BusinessLogic.Services;

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChatService _chatService;
    private readonly UserManager<ApplicationUser> _userManager;
    // appsettings: "AiRuntime:UsdToVndRate" → quy đổi chi phí AI sang VND trên Reports/Index.
    private readonly AiRuntimeSettings _aiRuntime;

    public ReportService(
        AppDbContext db,
        IUnitOfWork unitOfWork,
        IChatService chatService,
        UserManager<ApplicationUser> userManager,
        IOptions<AiRuntimeSettings> aiRuntime)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _chatService = chatService;
        _userManager = userManager;
        _aiRuntime = aiRuntime.Value;
    }

    public async Task<ReportDashboardDto> GetDashboardAsync(string userId, string role, ReportFilterDto filter)
    {
        var isAdmin = role == Roles.Admin;
        var isTeacher = role == Roles.Teacher;
        if (!isAdmin && !isTeacher)
            return new ReportDashboardDto();

        HashSet<int>? subjectFilter = null;
        if (isTeacher)
        {
            if (string.IsNullOrEmpty(userId))
                return new ReportDashboardDto();
            var subjects = await _unitOfWork.Subjects.GetByTeacherIdAsync(userId);
            subjectFilter = subjects.Select(s => s.Id).ToHashSet();
            if (subjectFilter.Count == 0)
                return new ReportDashboardDto { IsAdminView = false };
        }

        var (from, to, period) = ResolveRange(filter);
        var dto = new ReportDashboardDto
        {
            IsAdminView = isAdmin,
            Period = period,
            FromUtc = from,
            ToUtc = to
        };

        await FillOverviewAsync(dto, subjectFilter, from, to, isAdmin);
        await FillAiAndChatAsync(dto, subjectFilter, from, to, includeFinance: isAdmin);
        await FillDocumentsAsync(dto, subjectFilter);
        if (isAdmin)
        {
            await FillAdminExtrasAsync(dto, from, to);
            await FillPaymentRevenueAsync(dto, from, to);
        }
        else
            await FillTeacherActivityAsync(dto, subjectFilter, from, to);

        return dto;
    }

    public async Task<(int sessions, int questions, string? error)> RunRealChatWarmupAsync(
        string actorUserId,
        string actorRole,
        int maxSubjects = 2,
        int questionsPerSubject = 2)
    {
        if (actorRole is not (Roles.Admin or Roles.Teacher))
            return (0, 0, "Chỉ Admin hoặc Teacher mới chạy được chat warmup.");

        var student = await _userManager.FindByEmailAsync("student@gmail.com");
        if (student == null)
            return (0, 0, "Không tìm thấy tài khoản student@gmail.com để chạy chat thật.");

        IQueryable<Subject> subjectQuery = _db.Subjects.AsNoTracking().Where(s => s.IsActive);
        if (actorRole == Roles.Teacher)
            subjectQuery = subjectQuery.Where(s => s.TeacherId == actorUserId);

        var subjects = await subjectQuery.OrderBy(s => s.Id).Take(maxSubjects).ToListAsync();
        if (subjects.Count == 0)
            return (0, 0, "Không có môn phù hợp để chat.");

        var sessions = 0;
        var questions = 0;
        var subjectIndex = 0;

        foreach (var subject in subjects)
        {
            var chunks = await _db.DocumentChunks.AsNoTracking()
                .Where(c => c.SubjectId == subject.Id)
                .OrderBy(c => c.ChunkIndex)
                .Take(5)
                .ToListAsync();
            if (chunks.Count == 0)
            {
                subjectIndex++;
                continue;
            }

            // Lệch số câu theo môn để chart không trùng (vẫn là chat Gemini thật).
            var qCount = Math.Clamp(questionsPerSubject + subjectIndex * 2, 1, Math.Max(1, chunks.Count));

            var create = await _chatService.CreateSessionAsync(new CreateChatSessionDto
            {
                StudentId = student.Id,
                SubjectId = subject.Id,
                Title = $"Chat thật — {subject.Name} ({DateTime.Now:dd/MM HH:mm})"
            }, ipAddress: null);

            if (!create.Success || create.Session == null)
            {
                subjectIndex++;
                continue;
            }

            sessions++;
            var qs = BuildQuestionsFromChunks(subject.Name, chunks, qCount);
            foreach (var q in qs)
            {
                var result = await _chatService.SendMessageAsync(new SendChatMessageDto
                {
                    SessionId = create.Session.Id,
                    StudentId = student.Id,
                    SubjectId = subject.Id,
                    Question = q,
                    UsageOperation = AiUsageOperations.Warmup
                }, ipAddress: null);

                if (result.Success)
                    questions++;
            }

            subjectIndex++;
        }

        if (sessions == 0)
            return (0, 0, "Các môn chưa có chunk đã index — hãy upload và xử lý tài liệu trước.");

        return (sessions, questions, null);
    }

    private static IReadOnlyList<string> BuildQuestionsFromChunks(
        string subjectName,
        IReadOnlyList<DocumentChunk> chunks,
        int count)
    {
        var questions = new List<string>();
        foreach (var chunk in chunks.Take(count))
        {
            var snippet = chunk.Content.Length > 80 ? chunk.Content[..80] : chunk.Content;
            questions.Add($"Dựa trên tài liệu môn {subjectName}, hãy giải thích ngắn nội dung liên quan: \"{snippet.Trim()}...\"");
        }

        if (questions.Count == 0)
            questions.Add($"Tóm tắt các kiến thức chính trong tài liệu môn {subjectName}.");

        return questions;
    }

    private static (DateTime from, DateTime to, string period) ResolveRange(ReportFilterDto filter)
    {
        var period = ReportPeriods.Normalize(filter.Period);
        var to = DateTime.UtcNow;

        if (period == ReportPeriods.Custom && filter.FromUtc.HasValue && filter.ToUtc.HasValue)
        {
            var from = filter.FromUtc.Value.ToUniversalTime();
            var toCustom = filter.ToUtc.Value.ToUniversalTime();
            if (toCustom < from)
                (from, toCustom) = (toCustom, from);
            // inclusive end-of-day
            if (toCustom.TimeOfDay == TimeSpan.Zero)
                toCustom = toCustom.Date.AddDays(1).AddTicks(-1);
            return (from, toCustom, ReportPeriods.Custom);
        }

        if (period == ReportPeriods.Day)
            return (to.AddHours(-24), to, ReportPeriods.Day);

        var days = ReportPeriods.ToDays(period);
        return (to.Date.AddDays(-(days - 1)), to, period);
    }

    private async Task FillOverviewAsync(
        ReportDashboardDto dto,
        HashSet<int>? subjectFilter,
        DateTime from,
        DateTime to,
        bool isAdmin)
    {
        if (isAdmin)
        {
            var users = await _db.Users.AsNoTracking().ToListAsync();
            dto.TotalUsers = users.Count;
            dto.ActiveUsers = users.Count(u => u.IsActive);
            dto.InactiveUsers = users.Count(u => !u.IsActive);

            var teachers = await _userManager.GetUsersInRoleAsync(Roles.Teacher);
            var students = await _userManager.GetUsersInRoleAsync(Roles.Student);
            dto.TotalTeachers = teachers.Count;
            dto.TotalStudents = students.Count;

            dto.TotalSubjects = await _db.Subjects.CountAsync();
            dto.LoginCount = await _db.AuditLogs.CountAsync(a =>
                a.Action == AuditActions.Login && a.Timestamp >= from && a.Timestamp <= to);
            dto.DownloadCount = await _db.AuditLogs.CountAsync(a =>
                a.Action == AuditActions.DownloadDocument && a.Timestamp >= from && a.Timestamp <= to);
            dto.ErrorAuditCount = await _db.AuditLogs.CountAsync(a =>
                a.Action == AuditActions.AiResponseError && a.Timestamp >= from && a.Timestamp <= to);
        }
        else
        {
            dto.TotalSubjects = subjectFilter?.Count ?? 0;
        }

        var docs = _db.Documents.AsNoTracking().AsQueryable();
        if (subjectFilter != null)
            docs = docs.Where(d => subjectFilter.Contains(d.SubjectId));

        var docList = await docs.ToListAsync();
        dto.TotalDocuments = docList.Count;
        dto.IndexedDocuments = docList.Count(d => d.IndexStatus == DocumentIndexStatus.Indexed);
        dto.StorageBytes = docList.Sum(d => d.FileSizeBytes);

        var chunkQuery = _db.DocumentChunks.AsNoTracking().AsQueryable();
        var embedQuery = _db.DocumentEmbeddings.AsNoTracking().AsQueryable();
        if (subjectFilter != null)
        {
            chunkQuery = chunkQuery.Where(c => subjectFilter.Contains(c.SubjectId));
            embedQuery = embedQuery.Where(e => subjectFilter.Contains(e.SubjectId));
        }

        dto.TotalChunks = await chunkQuery.CountAsync();
        dto.TotalEmbeddings = await embedQuery.CountAsync();
    }

    private async Task FillAiAndChatAsync(
        ReportDashboardDto dto,
        HashSet<int>? subjectFilter,
        DateTime from,
        DateTime to,
        bool includeFinance = false)
    {
        var usageQuery = _db.AiUsageLogs.AsNoTracking()
            .Include(l => l.Subject)
            .Where(l => l.CreatedAt >= from && l.CreatedAt <= to
                        && l.Operation != AiUsageOperations.DemoSeed
                        && l.Operation != AiUsageOperations.Warmup);
        if (subjectFilter != null)
            usageQuery = usageQuery.Where(l => subjectFilter.Contains(l.SubjectId));

        var usageLogs = await usageQuery.ToListAsync();

        dto.TokensBySubject = usageLogs
            .GroupBy(l => new { l.SubjectId, Name = l.Subject?.Name ?? $"#{l.SubjectId}" })
            .Select(g => new ReportTokenBySubjectDto
            {
                SubjectId = g.Key.SubjectId,
                SubjectName = g.Key.Name,
                PromptTokens = g.Sum(x => x.PromptTokens),
                CompletionTokens = g.Sum(x => x.CompletionTokens),
                TotalTokens = g.Sum(x => x.TotalTokens),
                RequestCount = g.Count(),
                EstimatedCostUsd = includeFinance ? g.Sum(x => x.EstimatedCostUsd) : 0
            })
            .OrderByDescending(x => x.TotalTokens)
            .ToList();

        dto.TotalTokens = dto.TokensBySubject.Sum(x => x.TotalTokens);

        if (includeFinance)
        {
            dto.UsdToVndRate = _aiRuntime.UsdToVndRate > 0 ? _aiRuntime.UsdToVndRate : 25_000m;
            dto.TotalEstimatedCostUsd = usageLogs.Sum(l => l.EstimatedCostUsd);
            dto.CostByProvider = usageLogs
                .GroupBy(l => string.IsNullOrWhiteSpace(l.Provider) ? AiProviderIds.Gemini : l.Provider!)
                .Select(g => new ReportCostByProviderDto
                {
                    Provider = g.Key,
                    RequestCount = g.Count(),
                    TotalTokens = g.Sum(x => x.TotalTokens),
                    EstimatedCostUsd = g.Sum(x => x.EstimatedCostUsd)
                })
                .OrderByDescending(x => x.EstimatedCostUsd)
                .ThenByDescending(x => x.RequestCount)
                .ToList();
        }

        dto.TokensByDay = BuildTimeline(usageLogs, from, to, dto.Period);

        var sessionsQuery = _db.ChatSessions.AsNoTracking()
            .Include(s => s.Subject)
            .Include(s => s.Student)
            .Where(s => s.CreatedAt >= from && s.CreatedAt <= to
                        && !s.Title.StartsWith("[Demo]"));
        if (subjectFilter != null)
            sessionsQuery = sessionsQuery.Where(s => subjectFilter.Contains(s.SubjectId));

        var sessions = await sessionsQuery.ToListAsync();
        dto.TotalChatSessions = sessions.Count;
        var sessionIds = sessions.Select(s => s.Id).ToList();

        var userMessages = await _db.ChatMessages.AsNoTracking()
            .Where(m => sessionIds.Contains(m.ChatSessionId) && m.Role == "user"
                        && m.CreatedAt >= from && m.CreatedAt <= to)
            .ToListAsync();

        dto.ChatActivityBySubject = sessions
            .GroupBy(s => new { s.SubjectId, Name = s.Subject?.Name ?? $"#{s.SubjectId}" })
            .Select(g => new ReportChatActivityBySubjectDto
            {
                SubjectId = g.Key.SubjectId,
                SubjectName = g.Key.Name,
                SessionCount = g.Count(),
                QuestionCount = userMessages.Count(m => g.Any(s => s.Id == m.ChatSessionId))
            })
            .OrderByDescending(x => x.QuestionCount)
            .ToList();

        dto.TotalQuestions = dto.ChatActivityBySubject.Sum(x => x.QuestionCount);

        dto.MostActiveStudents = sessions
            .GroupBy(s => s.Student?.Email ?? s.StudentId)
            .Select(g => new ReportNamedCountDto
            {
                Name = g.Key,
                Count = userMessages.Count(m => g.Any(s => s.Id == m.ChatSessionId))
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        var citedMessages = await _db.ChatMessages.AsNoTracking()
            .Include(m => m.ChatSession)
            .Where(m => m.Role == "assistant"
                        && m.CitedChunkIds != null
                        && m.CitedChunkIds != ""
                        && m.CreatedAt >= from
                        && m.CreatedAt <= to
                        && sessionIds.Contains(m.ChatSessionId))
            .ToListAsync();

        if (subjectFilter != null)
            citedMessages = citedMessages.Where(m => subjectFilter.Contains(m.ChatSession.SubjectId)).ToList();

        var chunkIdCounts = new Dictionary<int, int>();
        foreach (var message in citedMessages)
        {
            foreach (var chunkId in ParseChunkIds(message.CitedChunkIds))
                chunkIdCounts[chunkId] = chunkIdCounts.GetValueOrDefault(chunkId) + 1;
        }

        var topChunkIds = chunkIdCounts.OrderByDescending(kv => kv.Value).Take(10).Select(kv => kv.Key).ToList();
        var chunks = await _db.DocumentChunks.AsNoTracking()
            .Include(c => c.Document)
            .Include(c => c.Subject)
            .Where(c => topChunkIds.Contains(c.Id))
            .ToListAsync();

        dto.TopCitedChunks = topChunkIds
            .Select(id =>
            {
                var chunk = chunks.FirstOrDefault(c => c.Id == id);
                if (chunk == null) return null;
                if (subjectFilter != null && !subjectFilter.Contains(chunk.SubjectId)) return null;
                return new ReportTopCitedChunkDto
                {
                    ChunkId = chunk.Id,
                    DocumentId = chunk.DocumentId,
                    DocumentFileName = chunk.Document?.FileName ?? $"#{chunk.DocumentId}",
                    ChunkIndex = chunk.ChunkIndex,
                    SubjectName = chunk.Subject?.Name ?? "",
                    CitationCount = chunkIdCounts[id],
                    Preview = chunk.Content.Length > 120 ? chunk.Content[..120] + "…" : chunk.Content
                };
            })
            .Where(x => x != null)
            .Cast<ReportTopCitedChunkDto>()
            .ToList();

        // Activity trend: questions per bucket
        dto.ActivityTrend = BuildQuestionTimeline(userMessages, from, to, dto.Period);
    }

    private async Task FillDocumentsAsync(ReportDashboardDto dto, HashSet<int>? subjectFilter)
    {
        var query = _db.Documents.AsNoTracking()
            .Include(d => d.Subject)
            .Include(d => d.UploadedBy)
            .AsQueryable();
        if (subjectFilter != null)
            query = query.Where(d => subjectFilter.Contains(d.SubjectId));

        var docs = await query.ToListAsync();
        var chunkCounts = await _db.DocumentChunks.AsNoTracking()
            .Where(c => subjectFilter == null || subjectFilter.Contains(c.SubjectId))
            .GroupBy(c => c.SubjectId)
            .Select(g => new { SubjectId = g.Key, Count = g.Count() })
            .ToListAsync();
        var chunkMap = chunkCounts.ToDictionary(x => x.SubjectId, x => x.Count);

        dto.DocumentsBySubject = docs
            .GroupBy(d => new { d.SubjectId, Name = d.Subject?.Name ?? $"#{d.SubjectId}" })
            .Select(g => new ReportDocumentBySubjectDto
            {
                SubjectId = g.Key.SubjectId,
                SubjectName = g.Key.Name,
                DocumentCount = g.Count(),
                IndexedCount = g.Count(d => d.IndexStatus == DocumentIndexStatus.Indexed),
                TotalBytes = g.Sum(d => d.FileSizeBytes),
                ChunkCount = chunkMap.GetValueOrDefault(g.Key.SubjectId)
            })
            .OrderByDescending(x => x.DocumentCount)
            .ToList();

        dto.DocumentsByTeacher = docs
            .GroupBy(d => d.UploadedBy?.Email ?? d.UploadedByUserId)
            .Select(g => new ReportNamedCountDto
            {
                Name = g.Key,
                Count = g.Count(),
                ExtraLong = g.Sum(d => d.FileSizeBytes)
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();
    }

    private async Task FillAdminExtrasAsync(ReportDashboardDto dto, DateTime from, DateTime to)
    {
        var downloadLogs = await _db.AuditLogs.AsNoTracking()
            .Where(a => a.Action == AuditActions.DownloadDocument
                        && a.Timestamp >= from && a.Timestamp <= to)
            .ToListAsync();

        dto.TopDownloadedDocuments = downloadLogs
            .Select(a => ExtractFileName(a.Details) ?? "Không rõ")
            .GroupBy(x => x)
            .Select(g => new ReportNamedCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        // Most active teachers by upload in range
        var uploads = await _db.Documents.AsNoTracking()
            .Include(d => d.UploadedBy)
            .Where(d => d.CreatedAt >= from && d.CreatedAt <= to)
            .ToListAsync();

        dto.MostActiveTeachers = uploads
            .GroupBy(d => d.UploadedBy?.Email ?? d.UploadedByUserId)
            .Select(g => new ReportNamedCountDto { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();
    }

    private async Task FillPaymentRevenueAsync(ReportDashboardDto dto, DateTime from, DateTime to)
    {
        var payments = await _db.PaymentTransactions.AsNoTracking()
            .Include(t => t.Package)
            .Where(t => t.Status == TransactionStatus.Success
                        && t.CreatedAt >= from
                        && t.CreatedAt <= to)
            .ToListAsync();

        dto.SuccessfulPaymentCount = payments.Count;
        dto.TotalRevenueVnd = payments.Sum(t => t.Amount);
        dto.RevenueByPackage = payments
            .GroupBy(t => new
            {
                t.PackageId,
                Name = t.Package?.Name ?? t.PackageId
            })
            .Select(g => new ReportRevenueByPackageDto
            {
                PackageId = g.Key.PackageId,
                PackageName = g.Key.Name,
                PaymentCount = g.Count(),
                TotalAmountVnd = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.TotalAmountVnd)
            .ThenByDescending(x => x.PaymentCount)
            .ToList();
    }

    private Task FillTeacherActivityAsync(
        ReportDashboardDto dto,
        HashSet<int>? subjectFilter,
        DateTime from,
        DateTime to)
    {
        // Teacher extras already covered by document/AI fills.
        dto.MostActiveTeachers = Array.Empty<ReportNamedCountDto>();
        return Task.CompletedTask;
    }

    private static string? ExtractFileName(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
            return null;
        // Details often contain file name somewhere
        const string marker = "file ";
        var idx = details.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var rest = details[(idx + marker.Length)..].Trim().Trim('\'', '"');
            var space = rest.IndexOf(' ');
            return space > 0 ? rest[..space] : rest;
        }

        return details.Length > 80 ? details[..80] : details;
    }

    private static List<ReportTokenByDayDto> BuildTimeline(
        IReadOnlyList<AiUsageLog> usageLogs,
        DateTime from,
        DateTime to,
        string period)
    {
        if (period == ReportPeriods.Day || (to - from).TotalHours <= 36)
            return BuildHourly(usageLogs, from, to);

        var days = Math.Max(1, (int)Math.Ceiling((to.Date - from.Date).TotalDays) + 1);
        days = Math.Min(days, 90);
        return Enumerable.Range(0, days)
            .Select(offset => from.Date.AddDays(offset))
            .Where(day => day <= to.Date)
            .Select(day =>
            {
                var dayLogs = usageLogs.Where(l => l.CreatedAt.Date == day);
                return new ReportTokenByDayDto
                {
                    Date = DateOnly.FromDateTime(day),
                    Label = day.ToString("dd/MM"),
                    TotalTokens = dayLogs.Sum(l => l.TotalTokens),
                    RequestCount = dayLogs.Count()
                };
            })
            .ToList();
    }

    private static List<ReportTokenByDayDto> BuildHourly(
        IReadOnlyList<AiUsageLog> usageLogs,
        DateTime from,
        DateTime to)
    {
        var start = new DateTime(from.Year, from.Month, from.Day, from.Hour, 0, 0, DateTimeKind.Utc);
        var hours = Math.Max(1, (int)Math.Ceiling((to - start).TotalHours));
        hours = Math.Min(hours, 48);
        return Enumerable.Range(0, hours)
            .Select(offset => start.AddHours(offset))
            .Select(hour =>
            {
                var hourLogs = usageLogs.Where(l => l.CreatedAt >= hour && l.CreatedAt < hour.AddHours(1));
                return new ReportTokenByDayDto
                {
                    Date = DateOnly.FromDateTime(hour),
                    Label = hour.ToLocalTime().ToString("HH:mm"),
                    TotalTokens = hourLogs.Sum(l => l.TotalTokens),
                    RequestCount = hourLogs.Count()
                };
            })
            .ToList();
    }

    private static List<ReportTokenByDayDto> BuildQuestionTimeline(
        IReadOnlyList<ChatMessage> userMessages,
        DateTime from,
        DateTime to,
        string period)
    {
        if (period == ReportPeriods.Day || (to - from).TotalHours <= 36)
        {
            var start = new DateTime(from.Year, from.Month, from.Day, from.Hour, 0, 0, DateTimeKind.Utc);
            var hours = Math.Min(48, Math.Max(1, (int)Math.Ceiling((to - start).TotalHours)));
            return Enumerable.Range(0, hours)
                .Select(offset => start.AddHours(offset))
                .Select(hour => new ReportTokenByDayDto
                {
                    Date = DateOnly.FromDateTime(hour),
                    Label = hour.ToLocalTime().ToString("HH:mm"),
                    RequestCount = userMessages.Count(m => m.CreatedAt >= hour && m.CreatedAt < hour.AddHours(1)),
                    TotalTokens = 0
                })
                .ToList();
        }

        var days = Math.Min(90, Math.Max(1, (int)Math.Ceiling((to.Date - from.Date).TotalDays) + 1));
        return Enumerable.Range(0, days)
            .Select(offset => from.Date.AddDays(offset))
            .Where(day => day <= to.Date)
            .Select(day => new ReportTokenByDayDto
            {
                Date = DateOnly.FromDateTime(day),
                Label = day.ToString("dd/MM"),
                RequestCount = userMessages.Count(m => m.CreatedAt.Date == day),
                TotalTokens = 0
            })
            .ToList();
    }

    private static IEnumerable<int> ParseChunkIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            yield break;
        int[]? ids = null;
        try { ids = JsonSerializer.Deserialize<int[]>(json); }
        catch (JsonException) { yield break; }
        if (ids == null) yield break;
        foreach (var id in ids)
            yield return id;
    }
}
