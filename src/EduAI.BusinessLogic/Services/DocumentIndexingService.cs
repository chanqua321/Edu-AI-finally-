using System.Text.Json;
using EduAI.BusinessLogic.Helpers;
using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.Enums;
using EduAI.Model.IRepository;

namespace EduAI.BusinessLogic.Services;

public sealed class DocumentIndexingService : IDocumentIndexingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGeminiAiService _geminiAiService;
    private readonly INotificationService _notificationService;
    private readonly ISubjectService _subjectService;
    private readonly ISubjectNotificationService _subjectNotificationService;
    private readonly IIndexingSettingsService _indexingSettingsService;

    public DocumentIndexingService(
        IUnitOfWork unitOfWork,
        IGeminiAiService geminiAiService,
        INotificationService notificationService,
        ISubjectService subjectService,
        ISubjectNotificationService subjectNotificationService,
        IIndexingSettingsService indexingSettingsService)
    {
        _unitOfWork = unitOfWork;
        _geminiAiService = geminiAiService;
        _notificationService = notificationService;
        _subjectService = subjectService;
        _subjectNotificationService = subjectNotificationService;
        _indexingSettingsService = indexingSettingsService;
    }

    public async Task IndexAsync(int documentId, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var document = await _unitOfWork.Documents.GetWithDetailsAsync(documentId);
        if (document == null)
            return;

        var physicalPath = DocumentPathHelper.ResolvePhysicalPath(document.FilePath);

        if (!File.Exists(physicalPath))
        {
            await SetStatusAsync(document, DocumentIndexStatus.Failed, "File không còn tồn tại trên máy chủ.");
            await NotifyProgressAsync(documentId, "Failed", new { status = "Failed", error = "File không còn tồn tại trên máy chủ." });
            return;
        }

        await SetStatusAsync(document, DocumentIndexStatus.Processing, null);
        await NotifyProgressAsync(documentId, "Started", new { status = "Processing", chunkCount = 0, embedded = 0 });

        try
        {
            // Idempotent: clear any partial/previous index so a re-queue (e.g. after a restart)
            // does not create duplicate chunks/embeddings.
            await ClearExistingIndexAsync(document);

            int chunkCount;
            await using (var readStream = File.OpenRead(physicalPath))
            {
                chunkCount = await CreateChunksAsync(document, readStream, cancellationToken);
            }

            await NotifyProgressAsync(documentId, "ChunksCreated", new { status = "Processing", chunkCount, embedded = 0 });

            var createdChunks = await _unitOfWork.Chunks.GetByDocumentIdAsync(documentId);
            var embedded = 0;
            foreach (var chunk in createdChunks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var embedInput = chunk.Content.Length > 8000 ? chunk.Content[..8000] : chunk.Content;
                var vector = await _geminiAiService.EmbedTextAsync(embedInput);
                await _unitOfWork.Embeddings.AddAsync(new DocumentEmbedding
                {
                    ChunkId = chunk.Id,
                    SubjectId = document.SubjectId,
                    ChapterId = document.ChapterId,
                    DocumentId = document.Id,
                    EmbeddingVector = VectorHelper.Serialize(vector)
                });
                embedded++;

                if (embedded % 5 == 0 || embedded == chunkCount)
                    await NotifyProgressAsync(documentId, "EmbeddingProgress", new { status = "Processing", chunkCount, embedded });
            }

            await _unitOfWork.SaveChangesAsync();

            await SetStatusAsync(document, DocumentIndexStatus.Indexed, null);
            await NotifyProgressAsync(documentId, "Completed", new
            {
                status = "Indexed",
                chunkCount,
                embedded,
                processedAt = DateTime.UtcNow
            });
            await NotifySubjectMaterialsChangedAsync(document.SubjectId);
        }
        catch (OperationCanceledException)
        {
            // App is shutting down: leave status as Processing so it can be retried later.
            throw;
        }
        catch (Exception ex)
        {
            await SetStatusAsync(document, DocumentIndexStatus.Failed, ex.Message);
            await NotifyProgressAsync(documentId, "Failed", new { status = "Failed", error = ex.Message });
        }
    }

    private async Task ClearExistingIndexAsync(Document document)
    {
        var embeddings = await _unitOfWork.Embeddings.GetBySubjectIdAsync(document.SubjectId);
        foreach (var embedding in embeddings.Where(e => e.DocumentId == document.Id))
            _unitOfWork.Embeddings.Remove(embedding);

        var existingChunks = await _unitOfWork.Chunks.GetByDocumentIdAsync(document.Id);
        foreach (var chunk in existingChunks)
            _unitOfWork.Chunks.Remove(chunk);

        if (embeddings.Any(e => e.DocumentId == document.Id) || existingChunks.Count > 0)
            await _unitOfWork.SaveChangesAsync();
    }

    private async Task SetStatusAsync(Document document, DocumentIndexStatus status, string? error)
    {
        document.IndexStatus = status;
        document.IndexError = error is { Length: > 1000 } ? error[..1000] : error;
        document.IndexedAt = status == DocumentIndexStatus.Indexed ? DateTime.UtcNow : document.IndexedAt;
        _unitOfWork.Documents.Update(document);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<int> CreateChunksAsync(Document document, Stream fileStream, CancellationToken cancellationToken)
    {
        fileStream.Position = 0;
        var text = await DocumentTextExtractor.ExtractTextAsync(fileStream, document.FileName);
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Không tìm thấy nội dung văn bản có thể đọc trong file đã tải lên.");

        var indexingSettings = await _indexingSettingsService.GetAsync();
        var textChunks = DocumentTextExtractor.ChunkText(text, indexingSettings.ChunkSize, indexingSettings.ChunkOverlap);
        if (textChunks.Count == 0)
            throw new InvalidOperationException("Không thể chia nội dung tài liệu thành các đoạn (chunk).");

        var index = 0;
        foreach (var content in textChunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _unitOfWork.Chunks.AddAsync(new DocumentChunk
            {
                SubjectId = document.SubjectId,
                ChapterId = document.ChapterId,
                DocumentId = document.Id,
                Content = content,
                ChunkIndex = index++
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return textChunks.Count;
    }

    private Task NotifyProgressAsync(int documentId, string action, object payload) =>
        _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "DocumentIndex",
            Action = action,
            EntityId = documentId,
            Message = JsonSerializer.Serialize(payload)
        });

    private async Task NotifySubjectMaterialsChangedAsync(int subjectId)
    {
        var subject = await _subjectService.GetByIdAsync(subjectId, null, Roles.Admin);
        if (subject == null)
            return;

        await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
        {
            Action = SubjectRealtimeActions.Updated,
            SubjectId = subjectId,
            Subject = subject
        });
    }
}

