using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface IChatService
{
    Task<IReadOnlyList<ChatSessionDto>> GetSessionsAsync(string studentId);
    Task<IReadOnlyList<ChatSessionDto>> GetAllSessionsAsync(string userId, string role);
    Task<ChatSessionDto?> GetSessionForAdminAsync(int sessionId);
    Task<ChatSessionOperationResultDto> DeleteSessionAsAdminAsync(int sessionId, string adminId, string? ipAddress);
    Task<ChatSessionOperationResultDto> UpdateSessionAsAdminAsync(UpdateChatSessionDto dto, string adminId, string? ipAddress);
    Task<ChatSessionDto?> GetSessionAsync(int sessionId, string studentId);
    Task<CreateChatSessionResultDto> CreateSessionAsync(CreateChatSessionDto dto, string? ipAddress);
    Task<ChatSessionOperationResultDto> UpdateSessionAsync(UpdateChatSessionDto dto, string? ipAddress);
    Task<ChatSessionOperationResultDto> DeleteSessionAsync(int sessionId, string studentId, string? ipAddress);
    Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(int sessionId, string studentId);
    Task<ChatResponseDto> SendMessageAsync(SendChatMessageDto dto, string? ipAddress);
    Task<IReadOnlyList<ChatMessageDto>> GetAllMessagesAsync(int? sessionId, string userId, string role);
    Task<ChatMessageDto?> GetMessageByIdAsync(int id, string userId, string role);
    Task<ChatMessageOperationResultDto> UpdateMessageAsync(UpdateChatMessageDto dto, string userId, string role, string? ipAddress);
    Task<ChatMessageOperationResultDto> DeleteMessageAsync(int id, string userId, string role, string? ipAddress);
    Task<ChatMessageOperationResultDto> CreateMessageAsync(CreateChatMessageDto dto, string userId, string role, string? ipAddress);
}
