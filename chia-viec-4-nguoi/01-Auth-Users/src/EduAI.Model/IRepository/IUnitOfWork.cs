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
    IIndexingSettingsRepository IndexingSettings { get; }
    Task<int> SaveChangesAsync();
}
