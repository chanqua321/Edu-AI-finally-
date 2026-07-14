using EduAI.Model.Entities;

namespace EduAI.Model.IRepository;

public interface IChatSessionRepository : IGenericRepository<ChatSession>
{
    Task<IReadOnlyList<ChatSession>> GetByStudentIdAsync(string studentId);
    Task<IReadOnlyList<ChatSession>> GetAllOrderedAsync();
    Task<ChatSession?> GetWithMessagesAsync(int id);
}

public interface IChatMessageRepository : IGenericRepository<ChatMessage>
{
    Task<IReadOnlyList<ChatMessage>> GetBySessionIdAsync(int sessionId);
}
