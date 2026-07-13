using System.Globalization;
using System.Text;
using EduAI.BusinessLogic.Helpers;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using EduAI.BusinessLogic.IService;
using Microsoft.Extensions.Logging;

namespace EduAI.BusinessLogic.Services;

public class ChatService : IChatService
{
    private const float MinCitationSimilarity = 0.55f;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly IGeminiAiService _geminiAiService;
    private readonly ILogger<ChatService> _logger;
    private readonly INotificationService _notificationService;

    public ChatService(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        IGeminiAiService geminiAiService,
        ILogger<ChatService> logger,
        INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _geminiAiService = geminiAiService;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<IReadOnlyList<ChatSessionDto>> GetSessionsAsync(string studentId)
    {
        var sessions = await _unitOfWork.ChatSessions.GetByStudentIdAsync(studentId);
        return sessions.Select(MapSessionDto).ToList();
    }

    public async Task<IReadOnlyList<ChatSessionDto>> GetAllSessionsAsync(string userId, string role)
    {
        if (role != Roles.Admin)
            return Array.Empty<ChatSessionDto>();

        var sessions = await _unitOfWork.ChatSessions.GetAllOrderedAsync();
        return sessions.Select(MapSessionDto).ToList();
    }

    public async Task<ChatSessionDto?> GetSessionForAdminAsync(int sessionId)
    {
        var session = await _unitOfWork.ChatSessions.GetWithMessagesAsync(sessionId);
        return session == null ? null : MapSessionDto(session);
    }

    public async Task<ChatSessionOperationResultDto> UpdateSessionAsAdminAsync(
        UpdateChatSessionDto dto, string adminId, string? ipAddress)
    {
        var session = await _unitOfWork.ChatSessions.GetByIdAsync(dto.SessionId);
        if (session == null)
            return SessionFail("Không tìm thấy phiên chat.");

        dto.StudentId = session.StudentId;
        return await UpdateSessionAsync(dto, ipAddress);
    }

    public async Task<ChatSessionOperationResultDto> DeleteSessionAsAdminAsync(
        int sessionId, string adminId, string? ipAddress)
    {
        var session = await _unitOfWork.ChatSessions.GetByIdAsync(sessionId);
        if (session == null)
            return SessionFail("Không tìm thấy phiên chat.");

        return await DeleteSessionAsync(sessionId, session.StudentId, ipAddress);
    }

    public async Task<ChatSessionDto?> GetSessionAsync(int sessionId, string studentId)
    {
        var session = await _unitOfWork.ChatSessions.GetByIdAsync(sessionId);
        if (session == null || session.StudentId != studentId)
            return null;

        var subject = await _unitOfWork.Subjects.GetByIdAsync(session.SubjectId);
        return new ChatSessionDto
        {
            Id = session.Id,
            SubjectId = session.SubjectId,
            SubjectName = subject?.Name ?? "Subject",
            Title = session.Title,
            CreatedAt = session.CreatedAt
        };
    }

    public async Task<CreateChatSessionResultDto> CreateSessionAsync(CreateChatSessionDto dto, string? ipAddress)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(dto.SubjectId);
        if (subject == null)
        {
            return new CreateChatSessionResultDto
            {
                Success = false,
                ErrorMessage = "Không tìm thấy môn học."
            };
        }

        if (!subject.IsActive)
        {
            return new CreateChatSessionResultDto
            {
                Success = false,
                ErrorMessage = "Môn học hiện không khả dụng."
            };
        }

        var subjectChunks = await _unitOfWork.Chunks.GetBySubjectIdAsync(dto.SubjectId);
        if (subjectChunks.Count == 0)
        {
            var documents = await _unitOfWork.Documents.GetBySubjectIdAsync(dto.SubjectId);
            return new CreateChatSessionResultDto
            {
                Success = false,
                ErrorMessage = documents.Count > 0
                    ? "Tài liệu của môn học đang được xử lý. Vui lòng thử lại sau ít phút."
                    : "Môn học này chưa có tài liệu. Hãy đề nghị giáo viên tải tài liệu lên trước."
            };
        }

        var session = new ChatSession
        {
            StudentId = dto.StudentId,
            SubjectId = dto.SubjectId,
            Title = string.IsNullOrWhiteSpace(dto.Title) ? $"{subject.Name} Chat" : dto.Title.Trim()
        };

        await _unitOfWork.ChatSessions.AddAsync(session);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = dto.StudentId,
            Action = AuditActions.CreateChatSession,
            IpAddress = ipAddress,
            Details = $"Created chat session for subject {subject.Name}"
        });

        await NotifyChatSessionAsync(session.Id, "Created", $"Chat session #{session.Id} created");

        return new CreateChatSessionResultDto
        {
            Success = true,
            Session = new ChatSessionDto
            {
                Id = session.Id,
                SubjectId = session.SubjectId,
                SubjectName = subject.Name,
                Title = session.Title,
                CreatedAt = session.CreatedAt
            }
        };
    }

    public async Task<ChatSessionOperationResultDto> UpdateSessionAsync(UpdateChatSessionDto dto, string? ipAddress)
    {
        var title = dto.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return new ChatSessionOperationResultDto
            {
                Success = false,
                ErrorMessage = "Vui lòng nhập tiêu đề phiên chat."
            };
        }

        var session = await _unitOfWork.ChatSessions.GetByIdAsync(dto.SessionId);
        if (session == null || session.StudentId != dto.StudentId)
        {
            return new ChatSessionOperationResultDto
            {
                Success = false,
                ErrorMessage = "Không tìm thấy phiên chat."
            };
        }

        session.Title = title;
        session.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.ChatSessions.Update(session);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = dto.StudentId,
            Action = AuditActions.UpdateChatSession,
            IpAddress = ipAddress,
            Details = $"Renamed chat session {dto.SessionId} to \"{title}\""
        });

        return new ChatSessionOperationResultDto { Success = true };
    }

    public async Task<ChatSessionOperationResultDto> DeleteSessionAsync(int sessionId, string studentId, string? ipAddress)
    {
        var session = await _unitOfWork.ChatSessions.GetByIdAsync(sessionId);
        if (session == null || session.StudentId != studentId)
        {
            return new ChatSessionOperationResultDto
            {
                Success = false,
                ErrorMessage = "Không tìm thấy phiên chat."
            };
        }

        var title = session.Title;
        _unitOfWork.ChatSessions.Remove(session);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = studentId,
            Action = AuditActions.DeleteChatSession,
            IpAddress = ipAddress,
            Details = $"Deleted chat session \"{title}\" (Id: {sessionId})"
        });

        return new ChatSessionOperationResultDto { Success = true };
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(int sessionId, string studentId)
    {
        var session = await _unitOfWork.ChatSessions.GetByIdAsync(sessionId);
        if (session == null || session.StudentId != studentId)
            return Array.Empty<ChatMessageDto>();

        var messages = await _unitOfWork.ChatMessages.GetBySessionIdAsync(sessionId);
        return messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            Role = m.Role,
            Content = m.Content,
            Citations = m.Citations,
            CreatedAt = m.CreatedAt
        }).ToList();
    }

    public async Task<ChatResponseDto> SendMessageAsync(SendChatMessageDto dto, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(dto.Question))
            return new ChatResponseDto { Success = false, ErrorMessage = "Vui lòng nhập câu hỏi." };

        var session = await _unitOfWork.ChatSessions.GetByIdAsync(dto.SessionId);
        if (session == null || session.StudentId != dto.StudentId)
        {
            return new ChatResponseDto { Success = false, ErrorMessage = "Phiên chat không hợp lệ." };
        }

        if (session.SubjectId != dto.SubjectId)
        {
            return new ChatResponseDto { Success = false, ErrorMessage = "Môn học không khớp. Không cho phép truy vấn chéo môn." };
        }

        var subject = await _unitOfWork.Subjects.GetByIdAsync(session.SubjectId);
        if (subject is not { IsActive: true })
        {
            return new ChatResponseDto { Success = false, ErrorMessage = "Môn học hiện không khả dụng." };
        }

        // Do not store the raw student question in the audit log (it may contain personal data).
        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = dto.StudentId,
            Action = AuditActions.AiRequest,
            IpAddress = ipAddress,
            Details = $"AI request in subject {dto.SubjectId} (độ dài câu hỏi: {dto.Question.Trim().Length} ký tự)"
        });

        var userMessage = new ChatMessage
        {
            ChatSessionId = dto.SessionId,
            Role = "user",
            Content = dto.Question.Trim()
        };
        await _unitOfWork.ChatMessages.AddAsync(userMessage);
        await _unitOfWork.SaveChangesAsync();

        try
        {
            var subjectName = await GetSubjectNameAsync(dto.SubjectId);
            var priorMessages = await _unitOfWork.ChatMessages.GetBySessionIdAsync(dto.SessionId);
            var history = priorMessages
                .Where(m => m.Id != userMessage.Id)
                .OrderBy(m => m.CreatedAt)
                .TakeLast(10)
                .Select(m => new ChatHistoryItemDto
                {
                    Role = m.Role,
                    Content = m.Content
                })
                .ToList();

            if (IsMetaOrConversationalQuestion(dto.Question))
            {
                var introAnswer =
                    $"Tôi là trợ lý học tập EduAI cho môn {subjectName}. " +
                    "Bạn có thể hỏi tôi về nội dung trong tài liệu đã được upload cho môn này.";
                await SaveAssistantMessageAsync(dto.SessionId, introAnswer, null);
                await NotifyChatSessionAsync(dto.SessionId, "MessageAdded", $"Chat session #{dto.SessionId} updated");
                return new ChatResponseDto { Success = true, Answer = introAnswer };
            }

            var relevantChunks = await FindRelevantChunksAsync(dto.SubjectId, dto.Question);

            if (relevantChunks.Count == 0)
            {
                var noDataAnswer = "Tôi không tìm thấy thông tin liên quan trong tài liệu môn học hiện tại.";
                await SaveAssistantMessageAsync(dto.SessionId, noDataAnswer, null);
                await NotifyChatSessionAsync(dto.SessionId, "MessageAdded", $"Chat session #{dto.SessionId} updated");
                return new ChatResponseDto { Success = true, Answer = noDataAnswer };
            }

            var maxRelevance = relevantChunks.Max(c => c.RelevanceScore);
            var context = string.Join("\n\n", relevantChunks.Select(c => c.Chunk.Content));
            var answer = await _geminiAiService.GenerateAnswerAsync(
                dto.Question, context, subjectName, history);

            string? citationText = null;
            if (maxRelevance >= MinCitationSimilarity)
            {
                citationText = string.Join("; ",
                    relevantChunks
                        .Where(c => c.RelevanceScore >= MinCitationSimilarity)
                        .Select(c => FormatCitation(c.Chunk))
                        .Distinct());
            }

            await SaveAssistantMessageAsync(dto.SessionId, answer, citationText);
            await NotifyChatSessionAsync(dto.SessionId, "MessageAdded", $"Chat session #{dto.SessionId} updated");

            return new ChatResponseDto
            {
                Success = true,
                Answer = answer,
                Citations = citationText
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI response error for session {SessionId}", dto.SessionId);

            await _auditLogService.LogAsync(new CreateAuditLogDto
            {
                UserId = dto.StudentId,
                Action = AuditActions.AiResponseError,
                IpAddress = ipAddress,
                Details = ex.Message
            });

            return new ChatResponseDto { Success = false, ErrorMessage = "Đã xảy ra lỗi khi tạo câu trả lời. Vui lòng thử lại." };
        }
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetAllMessagesAsync(int? sessionId, string userId, string role)
    {
        if (role == Roles.Admin)
        {
            var messages = sessionId.HasValue
                ? await _unitOfWork.ChatMessages.GetBySessionIdAsync(sessionId.Value)
                : (await _unitOfWork.ChatMessages.GetAllAsync()).OrderByDescending(m => m.CreatedAt).Take(200).ToList();

            return messages.Select(MapMessageDto).ToList();
        }

        if (role != Roles.Student)
            return Array.Empty<ChatMessageDto>();

        if (!sessionId.HasValue)
            return Array.Empty<ChatMessageDto>();

        return await GetMessagesAsync(sessionId.Value, userId);
    }

    public async Task<ChatMessageDto?> GetMessageByIdAsync(int id, string userId, string role)
    {
        var message = await _unitOfWork.ChatMessages.GetByIdAsync(id);
        if (message == null)
            return null;

        var session = await _unitOfWork.ChatSessions.GetByIdAsync(message.ChatSessionId);
        if (session == null)
            return null;

        if (role == Roles.Admin || (role == Roles.Student && session.StudentId == userId))
            return MapMessageDto(message);

        return null;
    }

    public async Task<ChatMessageOperationResultDto> UpdateMessageAsync(
        UpdateChatMessageDto dto, string userId, string role, string? ipAddress)
    {
        if (string.IsNullOrWhiteSpace(dto.Content))
            return MessageFail("Nội dung tin nhắn không được để trống.");

        var message = await _unitOfWork.ChatMessages.GetByIdAsync(dto.Id);
        if (message == null)
            return MessageFail("Không tìm thấy tin nhắn.");

        var session = await _unitOfWork.ChatSessions.GetByIdAsync(message.ChatSessionId);
        if (session == null)
            return MessageFail("Không tìm thấy phiên chat.");

        // Chat history is an immutable transcript for students (they cannot tamper with
        // their own questions or the AI's answers). Only admins may moderate messages.
        if (role != Roles.Admin)
            return MessageFail("Bạn không thể chỉnh sửa tin nhắn trong hội thoại.");

        message.Content = dto.Content.Trim();
        _unitOfWork.ChatMessages.Update(message);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = userId,
            Action = AuditActions.UpdateChatMessage,
            IpAddress = ipAddress,
            Details = $"Updated chat message #{message.Id}"
        });

        var result = MapMessageDto(message);
        await _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "ChatMessage",
            Action = "Updated",
            EntityId = message.Id,
            Message = $"Chat message #{message.Id} updated"
        });

        return new ChatMessageOperationResultDto { Success = true, Message = result };
    }

    public async Task<ChatMessageOperationResultDto> DeleteMessageAsync(
        int id, string userId, string role, string? ipAddress)
    {
        var message = await _unitOfWork.ChatMessages.GetByIdAsync(id);
        if (message == null)
            return MessageFail("Không tìm thấy tin nhắn.");

        var session = await _unitOfWork.ChatSessions.GetByIdAsync(message.ChatSessionId);
        if (session == null)
            return MessageFail("Không tìm thấy phiên chat.");

        // Students cannot delete individual messages (which would skew the conversation
        // history). They may delete the whole session instead. Only admins moderate messages.
        if (role != Roles.Admin)
            return MessageFail("Bạn không thể xóa từng tin nhắn. Hãy xóa toàn bộ phiên chat nếu cần.");

        _unitOfWork.ChatMessages.Remove(message);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = userId,
            Action = AuditActions.DeleteChatMessage,
            IpAddress = ipAddress,
            Details = $"Deleted chat message #{id}"
        });

        await _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "ChatMessage",
            Action = "Deleted",
            EntityId = id,
            Message = $"Chat message #{id} deleted"
        });

        return new ChatMessageOperationResultDto { Success = true };
    }

    private static ChatMessageDto MapMessageDto(ChatMessage m) => new()
    {
        Id = m.Id,
        SessionId = m.ChatSessionId,
        Role = m.Role,
        Content = m.Content,
        Citations = m.Citations,
        CreatedAt = m.CreatedAt
    };

    public async Task<ChatMessageOperationResultDto> CreateMessageAsync(
        CreateChatMessageDto dto, string userId, string role, string? ipAddress)
    {
        if (role != Roles.Admin)
            return MessageFail("Chỉ Admin mới có thể tạo tin nhắn thủ công.");

        if (string.IsNullOrWhiteSpace(dto.Content))
            return MessageFail("Nội dung tin nhắn không được để trống.");

        var roleValue = dto.Role.Trim().ToLowerInvariant();
        if (roleValue is not ("user" or "assistant"))
            return MessageFail("Vai trò phải là user hoặc assistant.");

        var session = await _unitOfWork.ChatSessions.GetByIdAsync(dto.SessionId);
        if (session == null)
            return MessageFail("Không tìm thấy phiên chat.");

        var message = new ChatMessage
        {
            ChatSessionId = dto.SessionId,
            Role = roleValue,
            Content = dto.Content.Trim(),
            Citations = dto.Citations
        };

        await _unitOfWork.ChatMessages.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = userId,
            Action = AuditActions.CreateChatMessage,
            IpAddress = ipAddress,
            Details = $"Created {roleValue} message #{message.Id} in session {dto.SessionId}"
        });

        var result = MapMessageDto(message);
        await NotifyChatSessionAsync(dto.SessionId, "MessageAdded", $"Chat session #{dto.SessionId} updated");
        await _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "ChatMessage",
            Action = "Created",
            EntityId = message.Id,
            Message = $"Chat message #{message.Id} created"
        });

        return new ChatMessageOperationResultDto { Success = true, Message = result };
    }

    private Task NotifyChatSessionAsync(int sessionId, string action, string message) =>
        _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "ChatSession",
            Action = action,
            EntityId = sessionId,
            Message = message
        });

    private static ChatMessageOperationResultDto MessageFail(string message) =>
        new() { Success = false, ErrorMessage = message };

    private static ChatSessionOperationResultDto SessionFail(string message) =>
        new() { Success = false, ErrorMessage = message };

    private static ChatSessionDto MapSessionDto(ChatSession s) => new()
    {
        Id = s.Id,
        SubjectId = s.SubjectId,
        SubjectName = s.Subject?.Name ?? string.Empty,
        Title = s.Title,
        StudentId = s.StudentId,
        StudentName = s.Student?.FullName ?? s.Student?.UserName,
        CreatedAt = s.CreatedAt
    };

    private async Task SaveAssistantMessageAsync(int sessionId, string content, string? citations)
    {
        var message = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = "assistant",
            Content = content,
            Citations = citations
        };
        await _unitOfWork.ChatMessages.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<string> GetSubjectNameAsync(int subjectId)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(subjectId);
        return subject?.Name ?? "subject";
    }

    private static string FormatCitation(DocumentChunk chunk) =>
        $"[{chunk.Chapter?.Name ?? "Chapter"} - {chunk.Document?.FileName ?? "Document"}, Chunk {chunk.ChunkIndex}]";

    private static bool IsMetaOrConversationalQuestion(string question)
    {
        var normalized = NormalizeQuestion(question);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        string[] phrases =
        [
            "ban la ai",
            "who are you",
            "xin chao",
            "chao ban",
            "hello",
            "hi eduai",
            "cam on",
            "thank you",
            "tro ly la gi",
            "eduai la gi",
            "ban co the lam gi",
            "ban giup gi duoc",
            "gioi thieu ban than",
            "con ai vay"
        ];

        if (phrases.Any(p => normalized.Contains(p, StringComparison.Ordinal)))
            return true;

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length <= 4 && (normalized.Contains("la ai", StringComparison.Ordinal) ||
                                   normalized.Contains("la gi", StringComparison.Ordinal));
    }

    private static string NormalizeQuestion(string question)
    {
        var lowered = question.Trim().ToLowerInvariant();
        var normalized = lowered.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed record RetrievedChunk(DocumentChunk Chunk, float RelevanceScore);

    private async Task<IReadOnlyList<RetrievedChunk>> FindRelevantChunksAsync(int subjectId, string question, int topK = 5)
    {
        var semanticMatches = await FindSemanticChunksAsync(subjectId, question, topK);
        if (semanticMatches.Count > 0)
            return semanticMatches;

        return await FindKeywordChunksAsync(subjectId, question, topK);
    }

    private async Task<IReadOnlyList<RetrievedChunk>> FindKeywordChunksAsync(int subjectId, string question, int topK)
    {
        var keywords = question.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (keywords.Length == 0)
            return Array.Empty<RetrievedChunk>();

        var chunks = await _unitOfWork.Chunks.SearchBySubjectAsync(subjectId, question, topK);
        return chunks
            .Select(chunk =>
            {
                var hits = keywords.Count(k => chunk.Content.Contains(k, StringComparison.OrdinalIgnoreCase));
                var score = (float)hits / keywords.Length;
                return new RetrievedChunk(chunk, score);
            })
            .Where(x => x.RelevanceScore > 0)
            .OrderByDescending(x => x.RelevanceScore)
            .ToList();
    }

    private async Task<IReadOnlyList<RetrievedChunk>> FindSemanticChunksAsync(int subjectId, string question, int topK)
    {
        var embeddings = await _unitOfWork.Embeddings.GetBySubjectIdAsync(subjectId);
        var vectorEmbeddings = embeddings
            .Where(e => VectorHelper.TryDeserialize(e.EmbeddingVector, out _))
            .ToList();

        if (vectorEmbeddings.Count == 0)
            return Array.Empty<RetrievedChunk>();

        var queryVector = await _geminiAiService.EmbedTextAsync(question);
        var ranked = vectorEmbeddings
            .Select(e =>
            {
                VectorHelper.TryDeserialize(e.EmbeddingVector, out var vector);
                return new
                {
                    e.ChunkId,
                    Score = VectorHelper.CosineSimilarity(queryVector, vector)
                };
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Where(x => x.Score > 0)
            .ToList();

        if (ranked.Count == 0)
            return Array.Empty<RetrievedChunk>();

        var chunks = await _unitOfWork.Chunks.GetBySubjectIdAsync(subjectId);
        var chunkMap = chunks.ToDictionary(c => c.Id);
        return ranked
            .Where(x => chunkMap.ContainsKey(x.ChunkId))
            .Select(x => new RetrievedChunk(chunkMap[x.ChunkId], (float)x.Score))
            .ToList();
    }
}
