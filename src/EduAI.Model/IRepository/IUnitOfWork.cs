using EduAI.Model.Entities;

namespace EduAI.Model.IRepository;

public interface IUnitOfWork
{
    ISubjectRepository Subjects { get; }
    ISubjectAssignmentRepository SubjectAssignments { get; }
    IChapterRepository Chapters { get; }
    ILessonRepository Lessons { get; }
    IDocumentRepository Documents { get; }
    IChunkRepository Chunks { get; }
    IEmbeddingRepository Embeddings { get; }
    IChatSessionRepository ChatSessions { get; }
    IChatMessageRepository ChatMessages { get; }
    IAuditLogRepository AuditLogs { get; }
    ISystemSettingsRepository SystemSettings { get; }
    IAiUsageLogRepository AiUsageLogs { get; }
    IGenericRepository<PaymentPackage> PaymentPackages { get; }
    IGenericRepository<UserSubscription> UserSubscriptions { get; }
    IGenericRepository<PaymentTransaction> PaymentTransactions { get; }
    Task<int> SaveChangesAsync();
}

