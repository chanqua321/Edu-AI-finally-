using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using EduAI.BusinessLogic.Helpers;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.Enums;
using EduAI.Model.IRepository;
using EduAI.Model.Settings;
using EduAI.BusinessLogic.IService;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EduAI.BusinessLogic.Services;

public class ChatService : IChatService
{
    private const float MinCitationSimilarity = 0.55f;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly IGeminiAiService _geminiAiService;
    private readonly ILogger<ChatService> _logger;
    private readonly INotificationService _notificationService;
    private readonly IPaymentService _paymentService;
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly IAiGenerationProviderResolver _generationProviderResolver;
    // appsettings: "AiRuntime" (inject qua IOptions) → Temperature, MaxChatHistory, GenerationModel dùng trong chat/RAG.
    private readonly AiRuntimeSettings _aiRuntime;

    public ChatService(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        IGeminiAiService geminiAiService,
        ILogger<ChatService> logger,
        INotificationService notificationService,
        IPaymentService paymentService,
        ISystemSettingsService systemSettingsService,
        IAiGenerationProviderResolver generationProviderResolver,
        IOptions<AiRuntimeSettings> aiRuntime)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _geminiAiService = geminiAiService;
        _logger = logger;
        _notificationService = notificationService;
        _paymentService = paymentService;
        _systemSettingsService = systemSettingsService;
        _generationProviderResolver = generationProviderResolver;
        _aiRuntime = aiRuntime.Value;
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

        string? scopedDocumentName = null;
        if (dto.DocumentId is int documentId)
        {
            var scopedDocument = await _unitOfWork.Documents.GetByIdAsync(documentId);
            if (scopedDocument == null || scopedDocument.SubjectId != dto.SubjectId)
            {
                return new ChatResponseDto
                {
                    Success = false,
                    ErrorMessage = "Tài liệu đã chọn không thuộc môn học của phiên chat này."
                };
            }

            if (scopedDocument.IndexStatus != DocumentIndexStatus.Indexed)
            {
                return new ChatResponseDto
                {
                    Success = false,
                    ErrorMessage = "Tài liệu này chưa index xong, chưa thể hỏi riêng file đó."
                };
            }

            scopedDocumentName = scopedDocument.FileName;
        }

        // Catalog / list documents by natural language — không cần quota AI.
        var catalogAnswer = await TryBuildDocumentCatalogAnswerAsync(dto.SubjectId, dto.Question);
        if (catalogAnswer != null)
        {
            var userCatalogMessage = new ChatMessage
            {
                ChatSessionId = dto.SessionId,
                Role = "user",
                Content = dto.Question.Trim()
            };
            await _unitOfWork.ChatMessages.AddAsync(userCatalogMessage);
            await _unitOfWork.SaveChangesAsync();

            await SaveAssistantMessageAsync(dto.SessionId, catalogAnswer, null);
            await NotifyChatSessionAsync(dto.SessionId, "MessageAdded", $"Chat session #{dto.SessionId} updated");

            var catalogProviderId = string.IsNullOrWhiteSpace(dto.ProviderId)
                ? AiProviderIds.Normalize((await _systemSettingsService.GetAsync()).GenerationProvider)
                : AiProviderIds.Normalize(dto.ProviderId);
            var catalogProvider = _generationProviderResolver.Resolve(catalogProviderId);

            return new ChatResponseDto
            {
                Success = true,
                Answer = catalogAnswer,
                ProviderId = catalogProvider.ProviderId,
                ProviderName = catalogProvider.DisplayName,
                Quota = await _paymentService.CheckProviderQuotaAsync(dto.StudentId, catalogProvider.ProviderId)
            };
        }

        var sysSettingsEarly = await _systemSettingsService.GetAsync();
        var requestedProviderId = string.IsNullOrWhiteSpace(dto.ProviderId)
            ? AiProviderIds.Normalize(sysSettingsEarly.GenerationProvider)
            : AiProviderIds.Normalize(dto.ProviderId);
        var providerQuota = await _paymentService.CheckProviderQuotaAsync(
            dto.StudentId,
            requestedProviderId);
        if (!providerQuota.CanUse)
        {
            var fallbackQuota = await GetFallbackQuotaAsync(dto.StudentId, providerQuota.ProviderId);
            return new ChatResponseDto 
            { 
                Success = false, 
                ErrorMessage = BuildQuotaErrorMessage(providerQuota, fallbackQuota),
                Quota = providerQuota,
                FallbackProviderId = fallbackQuota?.ProviderId,
                FallbackProviderName = fallbackQuota?.ProviderDisplayName,
                FallbackQuota = fallbackQuota
            };
        }

        // Do not store the raw student question in the audit log (it may contain personal data).
        // ghi laij log data dành cho mỗi khi student có câu hỏi hay gì ghi lại log 
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

        var totalSw = Stopwatch.StartNew();
        var embeddingTimeMs = 0;
        var searchTimeMs = 0;
        var generationTimeMs = 0;

        try
        {
            var subjectName = await GetSubjectNameAsync(dto.SubjectId);
            var sysSettings = sysSettingsEarly;
            var priorMessages = await _unitOfWork.ChatMessages.GetBySessionIdAsync(dto.SessionId);
            var history = priorMessages
                .Where(m => m.Id != userMessage.Id)
                .OrderBy(m => m.CreatedAt)
                .TakeLast(_aiRuntime.MaxChatHistory)
                .Select(m => new ChatHistoryItemDto
                {
                    Role = m.Role,
                    Content = m.Content
                })
                .ToList();

            var generationProvider = _generationProviderResolver.Resolve(providerQuota.ProviderId);

            if (IsMetaOrConversationalQuestion(dto.Question))
            {
                var introAnswer =
                    $"Tôi là trợ lý học tập EduAI cho môn {subjectName}. " +
                    "Bạn có thể hỏi nội dung bài học, hoặc nhờ liệt kê tài liệu (ví dụ: \"cung cấp tài liệu chapter 1\").";
                await SaveAssistantMessageAsync(dto.SessionId, introAnswer, null);
                await NotifyChatSessionAsync(dto.SessionId, "MessageAdded", $"Chat session #{dto.SessionId} updated");
                
                totalSw.Stop();
                return new ChatResponseDto
                {
                    Success = true,
                    Answer = introAnswer,
                    ProviderId = generationProvider.ProviderId,
                    ProviderName = generationProvider.DisplayName,
                    Quota = await _paymentService.CheckProviderQuotaAsync(dto.StudentId, generationProvider.ProviderId)
                };
            }

            var geminiOptions = new GeminiGenerationOptions
            {
                // appsettings: "AiRuntime:GenerationModel" → model Gemini dùng sinh câu trả lời chat.
                GenerationModel = generationProvider.ProviderId == AiProviderIds.Gemini
                    ? _aiRuntime.GenerationModel
                    : generationProvider.ModelName,
                // appsettings: "AiRuntime:EmbeddingModel" → model dùng tạo vector khi retrieve tài liệu.
                EmbeddingModel = _aiRuntime.EmbeddingModel,
                Temperature = _aiRuntime.Temperature,
                MaxOutputTokens = _aiRuntime.MaxOutputTokens
            };

            IReadOnlyCollection<int>? scopedDocumentIds = dto.DocumentId is int onlyDoc
                ? new[] { onlyDoc }
                : null;

            // Hỏi nội dung theo chương → tự hẹp phạm vi retrieve, không bắt chọn dropdown.
            if (scopedDocumentIds == null && TryParseChapterNumber(dto.Question) is int chapterNumber)
            {
                scopedDocumentIds = await ResolveDocumentIdsForChapterAsync(dto.SubjectId, chapterNumber);
                if (scopedDocumentIds.Count == 0)
                {
                    var missingChapterAnswer =
                        $"Môn {subjectName} hiện chưa có tài liệu khớp \"chapter/chương {chapterNumber}\".";
                    await SaveAssistantMessageAsync(dto.SessionId, missingChapterAnswer, null);
                    await NotifyChatSessionAsync(dto.SessionId, "MessageAdded", $"Chat session #{dto.SessionId} updated");
                    totalSw.Stop();
                    return new ChatResponseDto
                    {
                        Success = true,
                        Answer = missingChapterAnswer,
                        ProviderId = generationProvider.ProviderId,
                        ProviderName = generationProvider.DisplayName,
                        Quota = await _paymentService.CheckProviderQuotaAsync(dto.StudentId, generationProvider.ProviderId)
                    };
                }

                scopedDocumentName ??= $"chapter/chương {chapterNumber}";
            }

            // Hỏi nội dung theo chủ đề (WPF, LINQ, EF Core, ...) → hẹp theo tên file/chương.
            if (scopedDocumentIds == null && IsContentRequest(dto.Question))
            {
                var topicScope = await ResolveDocumentIdsByTopicAsync(dto.SubjectId, dto.Question);
                if (topicScope.DocumentIds.Count > 0)
                {
                    scopedDocumentIds = topicScope.DocumentIds;
                    scopedDocumentName ??= topicScope.Label;
                }
            }

            var (relevantChunks, embedTime, searchTime) = await FindRelevantChunksAsync(
                dto.SubjectId, dto.Question, sysSettings.RetrievalTopK, geminiOptions, scopedDocumentIds);
            embeddingTimeMs = embedTime;
            searchTimeMs = searchTime;

            if (relevantChunks.Count == 0 && scopedDocumentIds is { Count: > 0 })
            {
                (relevantChunks, embedTime, searchTime) = await FindRelevantChunksAsync(
                    dto.SubjectId, dto.Question, sysSettings.RetrievalTopK, geminiOptions, null);
                embeddingTimeMs += embedTime;
                searchTimeMs += searchTime;
            }

            if (relevantChunks.Count == 0)
            {
                var noDataAnswer = string.IsNullOrWhiteSpace(scopedDocumentName)
                    ? "Tôi không tìm thấy thông tin liên quan trong tài liệu môn học hiện tại."
                    : $"Tôi không tìm thấy thông tin liên quan trong tài liệu \"{scopedDocumentName}\".";
                await SaveAssistantMessageAsync(dto.SessionId, noDataAnswer, null);
                await NotifyChatSessionAsync(dto.SessionId, "MessageAdded", $"Chat session #{dto.SessionId} updated");
                
                totalSw.Stop();
                return new ChatResponseDto
                {
                    Success = true,
                    Answer = noDataAnswer,
                    ProviderId = generationProvider.ProviderId,
                    ProviderName = generationProvider.DisplayName,
                    Quota = await _paymentService.CheckProviderQuotaAsync(dto.StudentId, generationProvider.ProviderId)
                };
            }

            var maxRelevance = relevantChunks.Max(c => c.RelevanceScore);
            var context = string.Join("\n\n", relevantChunks.Select(c => c.Chunk.Content));
            
            var generationSw = Stopwatch.StartNew();
            // appsettings: "Gemini:ApiKey" (qua GeminiGenerationProvider → GeminiAiService) → gọi API sinh câu trả lời.
            var generateResult = await generationProvider.GenerateAnswerAsync(
                dto.Question, context, subjectName, history, geminiOptions);
            generationSw.Stop();
            generationTimeMs = (int)generationSw.ElapsedMilliseconds;
            
            var answer = generateResult.Answer;

            var usedChunks = relevantChunks
                .Select(c => new ChatUsedChunkDto
                {
                    ChunkId = c.Chunk.Id,
                    DocumentId = c.Chunk.DocumentId,
                    DocumentFileName = c.Chunk.Document?.FileName ?? $"#{c.Chunk.DocumentId}",
                    ChapterName = c.Chunk.Chapter?.Name,
                    ChunkIndex = c.Chunk.ChunkIndex,
                    RelevanceScore = c.RelevanceScore,
                    Preview = c.Chunk.Content.Length > 160 ? c.Chunk.Content[..160] + "…" : c.Chunk.Content
                })
                .ToList();

            string? citationText = null;
            IReadOnlyList<int> citedIds = Array.Empty<int>();
            // EnableCitation from SystemSettings
            if (sysSettings.EnableCitation && maxRelevance >= MinCitationSimilarity)
            {
                var cited = relevantChunks.Where(c => c.RelevanceScore >= MinCitationSimilarity).ToList();
                citationText = string.Join("; ",
                    cited.Select(c => FormatCitation(c.Chunk)).Distinct());
                citedIds = cited.Select(c => c.Chunk.Id).Distinct().ToList();
            }

            var assistantMessage = await SaveAssistantMessageAsync(
                dto.SessionId, answer, citationText, citedIds);

            totalSw.Stop();
            var totalTimeMs = (int)totalSw.ElapsedMilliseconds;

            var usageOperation = string.IsNullOrWhiteSpace(dto.UsageOperation)
                ? AiUsageOperations.GenerateAnswer
                : dto.UsageOperation.Trim();

            // AiUsageLog — luôn ghi bản ghi tối thiểu để tính quota; chi tiết theo logging flags
            var promptTokens = sysSettings.EnableTokenLogging ? (generateResult.Usage?.PromptTokens ?? 0) : 0;
            var completionTokens = sysSettings.EnableTokenLogging ? (generateResult.Usage?.CompletionTokens ?? 0) : 0;
            var totalTokens = sysSettings.EnableTokenLogging ? (generateResult.Usage?.TotalTokens ?? 0) : 0;
            await _unitOfWork.AiUsageLogs.AddAsync(new AiUsageLog
            {
                SubjectId = dto.SubjectId,
                ChatSessionId = dto.SessionId,
                ChatMessageId = assistantMessage.Id,
                UserId = dto.StudentId,
                Operation = usageOperation,
                Provider = generationProvider.ProviderId,
                Model = generationProvider.ModelName,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = totalTokens,
                EstimatedCostUsd = sysSettings.EnableCostLogging && generateResult.Usage != null
                    && generationProvider.ProviderId == AiProviderIds.Gemini
                    ? AiCostCalculator.EstimateGenerationCostUsd(generateResult.Usage, sysSettings) : 0,
                EmbeddingTimeMs = sysSettings.EnableLatencyLogging ? embeddingTimeMs : 0,
                RetrievalTimeMs = sysSettings.EnableLatencyLogging ? searchTimeMs : 0,
                GenerationTimeMs = sysSettings.EnableLatencyLogging ? generationTimeMs : 0,
                TotalTimeMs = sysSettings.EnableLatencyLogging || sysSettings.EnableBenchmarkLogging ? totalTimeMs : 0,
                IsSuccess = true
            });
            await _unitOfWork.SaveChangesAsync();

            await NotifyChatSessionAsync(dto.SessionId, "MessageAdded", $"Chat session #{dto.SessionId} updated");

            return new ChatResponseDto
            {
                Success = true,
                Answer = answer,
                Citations = citationText,
                UsedChunks = usedChunks,
                PromptTokens = generateResult.Usage?.PromptTokens,
                CompletionTokens = generateResult.Usage?.CompletionTokens,
                TotalTokens = generateResult.Usage?.TotalTokens,
                ProviderId = generationProvider.ProviderId,
                ProviderName = generationProvider.DisplayName,
                Quota = await _paymentService.CheckProviderQuotaAsync(dto.StudentId, generationProvider.ProviderId)
            };
        }
        catch (Exception ex)
        {
            totalSw.Stop();
            _logger.LogError(ex, "AI response error for session {SessionId}", dto.SessionId);

            try
            {
                var failedProvider = _generationProviderResolver.Resolve(requestedProviderId);
                var failedOperation = string.IsNullOrWhiteSpace(dto.UsageOperation)
                    ? AiUsageOperations.GenerateAnswer
                    : dto.UsageOperation.Trim();
                await _unitOfWork.AiUsageLogs.AddAsync(new AiUsageLog
                {
                    SubjectId = dto.SubjectId,
                    ChatSessionId = dto.SessionId,
                    UserId = dto.StudentId,
                    Operation = failedOperation,
                    Provider = failedProvider.ProviderId,
                    Model = failedProvider.ModelName,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    EmbeddingTimeMs = embeddingTimeMs,
                    RetrievalTimeMs = searchTimeMs,
                    GenerationTimeMs = generationTimeMs,
                    TotalTimeMs = (int)totalSw.ElapsedMilliseconds
                });
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "Failed to log AI failure to AiUsageLogs");
            }

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
    // lưu ý async đung để giải phóng luồng nếu như k dùng có cảm giác như máy bạn 10 nhân chỉ sài đc 1 nhân 


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

    private async Task<ChatMessage> SaveAssistantMessageAsync(
        int sessionId,
        string content,
        string? citations,
        IReadOnlyList<int>? citedChunkIds = null)
    {
        var message = new ChatMessage
        {
            ChatSessionId = sessionId,
            Role = "assistant",
            Content = content,
            Citations = citations,
            CitedChunkIds = citedChunkIds is { Count: > 0 }
                ? System.Text.Json.JsonSerializer.Serialize(citedChunkIds)
                : null
        };
        await _unitOfWork.ChatMessages.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();
        return message;
    }

    private async Task<string> GetSubjectNameAsync(int subjectId)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(subjectId);
        return subject?.Name ?? "subject";
    }

    private static string FormatCitation(DocumentChunk chunk) =>
        $"[{chunk.Chapter?.Name ?? "Chapter"} - {chunk.Document?.FileName ?? "Document"}, Chunk {chunk.ChunkIndex}]";

    private async Task<string?> TryBuildDocumentCatalogAnswerAsync(int subjectId, string question)
    {
        if (!IsDocumentCatalogRequest(question))
            return null;

        var subjectName = await GetSubjectNameAsync(subjectId);
        var documents = await _unitOfWork.Documents.GetBySubjectIdAsync(subjectId);
        if (documents.Count == 0)
            return $"Môn {subjectName} hiện chưa có tài liệu nào được tải lên.";

        var chapterNumber = TryParseChapterNumber(question);
        IReadOnlyList<Document> matched;
        string scopeLabel;

        if (chapterNumber is int n)
        {
            matched = documents
                .Where(d => DocumentMatchesChapter(d, n))
                .OrderBy(d => d.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            scopeLabel = $"chapter/chương {n}";
        }
        else
        {
            matched = documents
                .OrderBy(d => d.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            scopeLabel = "toàn bộ môn";
        }

        if (matched.Count == 0)
            return $"Không tìm thấy tài liệu khớp \"{scopeLabel}\" trong môn {subjectName}.";

        var sb = new StringBuilder();
        sb.AppendLine($"Đây là danh sách tài liệu ({scopeLabel}) của môn {subjectName}:");
        sb.AppendLine();
        for (var i = 0; i < matched.Count; i++)
        {
            var doc = matched[i];
            var fileChapter = TryParseChapterNumber(doc.FileName);
            var chapterPart = fileChapter is int fc
                ? $" — Chapter {fc:00}"
                : string.IsNullOrWhiteSpace(doc.Chapter?.Name)
                    ? string.Empty
                    : $" — {doc.Chapter!.Name}";
            sb.AppendLine($"{i + 1}. {doc.FileName}{chapterPart}");
        }

        sb.AppendLine();
        sb.Append("Bạn có thể hỏi nội dung trong các file trên (ví dụ: \"giải thích WPF\", \"tóm tắt Chapter 07\") hoặc chọn \"Lọc tài liệu\" nếu muốn khoá đúng 1 file.");
        return sb.ToString().TrimEnd();
    }

    private async Task<IReadOnlyList<int>> ResolveDocumentIdsForChapterAsync(int subjectId, int chapterNumber)
    {
        var documents = await _unitOfWork.Documents.GetBySubjectIdAsync(subjectId);
        return documents
            .Where(d => DocumentMatchesChapter(d, chapterNumber))
            .Select(d => d.Id)
            .Distinct()
            .ToList();
    }

    private sealed record TopicDocumentScope(IReadOnlyList<int> DocumentIds, string Label);

    private async Task<TopicDocumentScope> ResolveDocumentIdsByTopicAsync(int subjectId, string question)
    {
        var documents = await _unitOfWork.Documents.GetBySubjectIdAsync(subjectId);
        if (documents.Count == 0)
            return new TopicDocumentScope(Array.Empty<int>(), string.Empty);

        var keywords = ExtractTopicKeywords(question);
        if (keywords.Count == 0)
            return new TopicDocumentScope(Array.Empty<int>(), string.Empty);

        var matched = documents
            .Where(d => keywords.Any(k =>
                d.FileName.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                (d.Chapter?.Name?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)))
            .OrderBy(d => d.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matched.Count == 0)
            return new TopicDocumentScope(Array.Empty<int>(), string.Empty);

        var labelKeywords = keywords
            .Where(k => matched.Any(d =>
                d.FileName.Contains(k, StringComparison.OrdinalIgnoreCase) ||
                (d.Chapter?.Name?.Contains(k, StringComparison.OrdinalIgnoreCase) ?? false)))
            .Take(3)
            .ToList();

        var label = labelKeywords.Count > 0
            ? $"chủ đề {string.Join(", ", labelKeywords)}"
            : "chủ đề liên quan";

        return new TopicDocumentScope(matched.Select(d => d.Id).Distinct().ToList(), label);
    }

    private static IReadOnlyList<string> ExtractTopicKeywords(string question)
    {
        var normalized = NormalizeQuestion(question);
        var rawTokens = Regex.Split(question, @"[\s,.;:!?""'()\-–—]+")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .ToList();

        var keywords = new List<string>();
        foreach (var token in rawTokens)
        {
            var normalizedToken = NormalizeQuestion(token);
            if (string.IsNullOrWhiteSpace(normalizedToken))
                continue;

            if (TopicStopWords.Contains(normalizedToken))
                continue;

            if (normalizedToken.Length >= 3 || LooksLikeAcronym(token))
                keywords.Add(token);
        }

        return keywords
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool LooksLikeAcronym(string token) =>
        token.Length is >= 2 and <= 8 &&
        token.All(ch => char.IsUpper(ch) || char.IsDigit(ch));

    private static readonly HashSet<string> TopicStopWords = new(StringComparer.Ordinal)
    {
        "ban", "cho", "toi", "minh", "em", "co", "khong", "gi", "ve", "trong", "tai", "lieu", "file",
        "chapter", "chuong", "noi", "dung", "giai", "thich", "tom", "tat", "la", "nhu", "the", "nao",
        "hoc", "mon", "bai", "hay", "biet", "tim", "hieu", "doc", "xin", "cung", "cap", "danh", "sach",
        "cac", "nhung", "giup", "duoc", "neu", "ro", "mo", "ta", "hien", "thoi", "a", "k", "o", "ah",
        "uh", "vay", "duoc", "khong", "lam", "sao", "cach", "nao", "giup", "minh", "em", "cho", "xin",
        "hoi", "ve", "trong", "tai", "lieu", "file", "document", "slide", "ppt", "pptx", "pdf",
        "eduai", "tro", "ly", "assistant", "please", "what", "about", "the", "and", "for", "with",
        "building", "application", "applications", "windows", "presentation", "foundation"
    };

    private static bool IsContentRequest(string question)
    {
        var normalized = NormalizeQuestion(question);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return ContentIntentHints.Any(h => normalized.Contains(h, StringComparison.Ordinal));
    }

    private static readonly string[] ContentIntentHints =
    [
        "giai thich", "la gi", "nhu the nao", "tom tat", "noi dung", "phan biet",
        "so sanh", "vi du ve", "lam sao", "cach nao", "dinh nghia", "y nghia",
        "tai sao", "khac gi", "su khac nhau", "code", "viet code", "implement",
        "tom tat noi dung", "hoi ve", "cho biet ve", "biet ve", "tim hieu ve",
        "doc ve", "trong bai", "trong tai lieu", "trong chapter", "trong chuong",
        "neu ro", "mo ta", "trinh bay", "ke ve", "huong dan ve", "cach lam",
        "vi du", "minh hoa", "chi tiet ve", "noi ve", "giai dap ve"
    ];

    private static bool DocumentMatchesChapter(Document document, int chapterNumber)
    {
        // Ưu tiên số chương trong tên file (Chapter 01, Chapter 02, ...)
        // vì nhiều slide có thể nằm chung 1 "Chương 1: Tài liệu tổng hợp" trên DB.
        var fileChapter = TryParseChapterNumber(document.FileName);
        if (fileChapter.HasValue)
            return fileChapter.Value == chapterNumber;

        if (document.Chapter == null)
            return false;

        var chapterName = NormalizeQuestion(document.Chapter.Name);
        // Bucket "Chương 1: Tài liệu tổng hợp" chứa nhiều Chapter 01..11 — không map theo OrderNumber.
        if (chapterName.Contains("tong hop", StringComparison.Ordinal))
            return false;

        var chapterNameNumber = TryParseChapterNumber(document.Chapter.Name);
        if (chapterNameNumber.HasValue)
            return chapterNameNumber.Value == chapterNumber;

        return document.Chapter.OrderNumber == chapterNumber;
    }

    private static bool IsDocumentCatalogRequest(string question)
    {
        var normalized = NormalizeQuestion(question);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        // Câu hỏi nội dung → để RAG xử lý (có thể tự hẹp theo chapter/chủ đề).
        if (IsContentRequest(question))
            return false;

        string[] catalogHints =
        [
            "cung cap", "danh sach", "ten file", "nhung file", "cac file",
            "file nao", "tai lieu nao", "nhung tai lieu", "cac tai lieu",
            "cho toi tai lieu", "gui toi tai lieu", "cho minh tai lieu",
            "co nhung tai lieu", "co tai lieu gi", "co file gi",
            "liet ke", "list file", "list document", "show document",
            "tai lieu chapter", "tai lieu chuong", "file chapter", "file chuong"
        ];

        if (catalogHints.Any(h => normalized.Contains(h, StringComparison.Ordinal)))
            return true;

        // "tài liệu gì về X" / "file về X" — xin danh mục, không hỏi lý thuyết.
        if ((normalized.Contains("tai lieu", StringComparison.Ordinal) ||
             normalized.Contains("file", StringComparison.Ordinal)) &&
            normalized.Contains(" ve ", StringComparison.Ordinal) &&
            !IsContentRequest(question))
            return true;

        // "tai lieu chapter 1" / "chuong 2 co nhung gi" — xin danh mục, không hỏi lý thuyết.
        if ((normalized.Contains("tai lieu", StringComparison.Ordinal) ||
             normalized.Contains("file", StringComparison.Ordinal)) &&
            TryParseChapterNumber(question) != null)
            return true;

        return false;
    }

    private static int? TryParseChapterNumber(string question)
    {
        var normalized = NormalizeQuestion(question);
        // chapter / chappter / chapeter / chuong (chịu lỗi gõ)
        var match = Regex.Match(
            normalized,
            @"\b(?:ch+a+p+t*e*r+|ch+u+o*n+g+)\s*0*(?<n>\d{1,2})\b",
            RegexOptions.CultureInvariant);
        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups["n"].Value, out var number))
            return null;

        return number is >= 1 and <= 99 ? number : null;
    }

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
            "con ai vay",
            // Bổ sung thêm các biến thể phổ biến
            "ban la ai vay",
            "may la ai",
            "you are who",
            "chao buoi sang",
            "chao buoi chieu",
            "chao buoi toi",
            "hay gioi thieu",
            "ban ten gi",
            "ten ban la gi",
            "ok cam on",
            "thanks",
            "ok thanks",
            "ok",
            "oke",
            "bye",
            "tam biet"
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

    private async Task<ProviderQuotaCheckResult?> GetFallbackQuotaAsync(string studentId, string providerId)
    {
        if (providerId == AiProviderIds.Gemini)
        {
            var ollamaQuota = await _paymentService.CheckProviderQuotaAsync(studentId, AiProviderIds.Ollama);
            return ollamaQuota.CanUse ? ollamaQuota : null;
        }

        if (providerId == AiProviderIds.Ollama)
        {
            var geminiQuota = await _paymentService.CheckProviderQuotaAsync(studentId, AiProviderIds.Gemini);
            return geminiQuota.CanUse ? geminiQuota : null;
        }

        return null;
    }

    private static string BuildQuotaErrorMessage(ProviderQuotaCheckResult blockedQuota, ProviderQuotaCheckResult? fallbackQuota)
    {
        if (fallbackQuota != null && blockedQuota.ProviderId == AiProviderIds.Gemini)
        {
            return $"{blockedQuota.Message} Bạn vẫn còn {fallbackQuota.RemainingCount}/{fallbackQuota.LimitCount} lượt {fallbackQuota.ProviderDisplayName.ToLowerInvariant()} {fallbackQuota.WindowLabel}.";
        }

        return blockedQuota.Message;
    }

    private sealed record RetrievedChunk(DocumentChunk Chunk, float RelevanceScore);

    private async Task<(IReadOnlyList<RetrievedChunk> chunks, int embeddingTimeMs, int searchTimeMs)> FindRelevantChunksAsync(
        int subjectId,
        string question,
        int topK,
        GeminiGenerationOptions geminiOptions,
        IReadOnlyCollection<int>? documentIds = null)
    {
        try
        {
            var (semanticMatches, embedTime, searchTime) = await FindSemanticChunksAsync(
                subjectId, question, topK, geminiOptions, documentIds);
            if (semanticMatches.Count > 0)
                return (semanticMatches, embedTime, searchTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic retrieval failed for subject {SubjectId}; falling back to keyword search.", subjectId);
        }

        var (keywordMatches, kwTime) = await FindKeywordChunksAsync(subjectId, question, topK, documentIds);
        return (keywordMatches, 0, kwTime);
    }

    private async Task<(IReadOnlyList<RetrievedChunk> chunks, int searchTimeMs)> FindKeywordChunksAsync(
        int subjectId, string question, int topK, IReadOnlyCollection<int>? documentIds = null)
    {
        var sw = Stopwatch.StartNew();
        var keywords = question.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (keywords.Length == 0)
            return (Array.Empty<RetrievedChunk>(), 0);

        IReadOnlyList<DocumentChunk> chunks;
        if (documentIds is { Count: > 0 })
        {
            var idSet = documentIds as HashSet<int> ?? documentIds.ToHashSet();
            chunks = (await _unitOfWork.Chunks.GetBySubjectIdAsync(subjectId))
                .Where(c => idSet.Contains(c.DocumentId))
                .ToList();
        }
        else
        {
            chunks = await _unitOfWork.Chunks.SearchBySubjectAsync(subjectId, question, topK);
        }

        var result = chunks
            .Select(chunk =>
            {
                var hits = keywords.Count(k => chunk.Content.Contains(k, StringComparison.OrdinalIgnoreCase));
                var score = (float)hits / keywords.Length;
                return new RetrievedChunk(chunk, score);
            })
            .Where(x => x.RelevanceScore > 0)
            .OrderByDescending(x => x.RelevanceScore)
            .Take(topK)
            .ToList();
        sw.Stop();
        return (result, (int)sw.ElapsedMilliseconds);
    }

    private async Task<(IReadOnlyList<RetrievedChunk> chunks, int embeddingTimeMs, int searchTimeMs)> FindSemanticChunksAsync(
        int subjectId,
        string question,
        int topK,
        GeminiGenerationOptions geminiOptions,
        IReadOnlyCollection<int>? documentIds = null)
    {
        var embeddings = await _unitOfWork.Embeddings.GetBySubjectIdAsync(subjectId);
        var idSet = documentIds is { Count: > 0 }
            ? (documentIds as HashSet<int> ?? documentIds.ToHashSet())
            : null;

        var vectorEmbeddings = embeddings
            .Where(e => idSet == null || idSet.Contains(e.DocumentId))
            .Where(e => VectorHelper.TryDeserialize(e.EmbeddingVector, out _))
            .ToList();

        if (vectorEmbeddings.Count == 0)
            return (Array.Empty<RetrievedChunk>(), 0, 0);

        var embedSw = Stopwatch.StartNew();
        // appsettings: "Gemini:ApiKey" (qua GeminiAiService) → gọi API embedding để tìm chunk liên quan trong RAG.
        var queryVector = await _geminiAiService.EmbedTextAsync(question, geminiOptions);
        embedSw.Stop();
        var embeddingTimeMs = (int)embedSw.ElapsedMilliseconds;

        var searchSw = Stopwatch.StartNew();
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
        {
            searchSw.Stop();
            return (Array.Empty<RetrievedChunk>(), embeddingTimeMs, (int)searchSw.ElapsedMilliseconds);
        }

        var chunks = await _unitOfWork.Chunks.GetBySubjectIdAsync(subjectId);
        var chunkMap = chunks
            .Where(c => idSet == null || idSet.Contains(c.DocumentId))
            .ToDictionary(c => c.Id);
        var result = ranked
            .Where(x => chunkMap.ContainsKey(x.ChunkId))
            .Select(x => new RetrievedChunk(chunkMap[x.ChunkId], (float)x.Score))
            .ToList();
        searchSw.Stop();
        return (result, embeddingTimeMs, (int)searchSw.ElapsedMilliseconds);
    }
}
