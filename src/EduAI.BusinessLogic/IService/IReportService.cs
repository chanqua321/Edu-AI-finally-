using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface IReportService
{
    Task<ReportDashboardDto> GetDashboardAsync(string userId, string role, ReportFilterDto filter);

    /// <summary>
    /// Chạy chat RAG thật (Gemini) trên môn có tài liệu đã index để sinh AiUsageLog/citation thật cho demo.
    /// </summary>
    Task<(int sessions, int questions, string? error)> RunRealChatWarmupAsync(
        string actorUserId,
        string actorRole,
        int maxSubjects = 2,
        int questionsPerSubject = 2);
}
