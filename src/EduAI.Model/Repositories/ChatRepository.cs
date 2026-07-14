using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using Microsoft.EntityFrameworkCore;

namespace EduAI.Model.Repositories;

public class ChatSessionRepository : GenericRepository<ChatSession>, IChatSessionRepository
{
    public ChatSessionRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ChatSession>> GetByStudentIdAsync(string studentId) =>
        await DbSet.AsNoTracking()
            .Include(s => s.Subject)
            .Where(s => s.StudentId == studentId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public async Task<IReadOnlyList<ChatSession>> GetAllOrderedAsync() =>
        await DbSet.AsNoTracking()
            .Include(s => s.Subject)
            .Include(s => s.Student)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

    public async Task<ChatSession?> GetWithMessagesAsync(int id) =>
        await DbSet.Include(s => s.Messages.OrderBy(m => m.CreatedAt))
            .Include(s => s.Subject)
            .FirstOrDefaultAsync(s => s.Id == id);
}

public class ChatMessageRepository : GenericRepository<ChatMessage>, IChatMessageRepository
{
    public ChatMessageRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ChatMessage>> GetBySessionIdAsync(int sessionId) =>
        await DbSet.AsNoTracking()
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
}
