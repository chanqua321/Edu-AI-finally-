using EduAI.Model.Repositories;
using EduAI.Model.IRepository;
using EduAI.Model.Entities;

namespace EduAI.Model.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
        Subjects = new SubjectRepository(context);
        SubjectAssignments = new SubjectAssignmentRepository(context);
        Chapters = new ChapterRepository(context);
        Lessons = new LessonRepository(context);
        Documents = new DocumentRepository(context);
        Chunks = new ChunkRepository(context);
        Embeddings = new EmbeddingRepository(context);
        ChatSessions = new ChatSessionRepository(context);
        ChatMessages = new ChatMessageRepository(context);
        AuditLogs = new AuditLogRepository(context);
        SystemSettings = new SystemSettingsRepository(context);
        AiUsageLogs = new AiUsageLogRepository(context);
        PaymentPackages = new GenericRepository<PaymentPackage>(context);
        UserSubscriptions = new GenericRepository<UserSubscription>(context);
        PaymentTransactions = new GenericRepository<PaymentTransaction>(context);
    }

    public ISubjectRepository Subjects { get; }
    public ISubjectAssignmentRepository SubjectAssignments { get; }
    public IChapterRepository Chapters { get; }
    public ILessonRepository Lessons { get; }
    public IDocumentRepository Documents { get; }
    public IChunkRepository Chunks { get; }
    public IEmbeddingRepository Embeddings { get; }
    public IChatSessionRepository ChatSessions { get; }
    public IChatMessageRepository ChatMessages { get; }
    public IAuditLogRepository AuditLogs { get; }
    public ISystemSettingsRepository SystemSettings { get; }
    public IAiUsageLogRepository AiUsageLogs { get; }
    public IGenericRepository<PaymentPackage> PaymentPackages { get; }
    public IGenericRepository<UserSubscription> UserSubscriptions { get; }
    public IGenericRepository<PaymentTransaction> PaymentTransactions { get; }

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
}

