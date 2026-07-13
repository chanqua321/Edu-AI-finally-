using EduAI.Model.Repositories;
using EduAI.Model.IRepository;

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
        IndexingSettings = new IndexingSettingsRepository(context);
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
    public IIndexingSettingsRepository IndexingSettings { get; }

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();
}
