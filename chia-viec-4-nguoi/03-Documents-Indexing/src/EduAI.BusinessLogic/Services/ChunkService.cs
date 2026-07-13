using EduAI.BusinessLogic.Helpers;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using EduAI.BusinessLogic.IService;

namespace EduAI.BusinessLogic.Services;

public class ChunkService : IChunkService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubjectService _subjectService;
    private readonly IGeminiAiService _geminiAiService;
    private readonly INotificationService _notificationService;

    public ChunkService(
        IUnitOfWork unitOfWork,
        ISubjectService subjectService,
        IGeminiAiService geminiAiService,
        INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _subjectService = subjectService;
        _geminiAiService = geminiAiService;
        _notificationService = notificationService;
    }

    public async Task<IReadOnlyList<ChunkDto>> GetBySubjectAsync(
        int subjectId, string userId, string role, string? keyword = null)
    {
        if (role == Roles.Student)
            return Array.Empty<ChunkDto>();

        if (role == Roles.Teacher && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, subjectId))
            return Array.Empty<ChunkDto>();

        if (role != Roles.Admin && role != Roles.Teacher)
            return Array.Empty<ChunkDto>();

        var chunks = await _unitOfWork.Chunks.GetBySubjectIdAsync(subjectId);
        var embeddingChunkIds = role == Roles.Admin
            ? (await _unitOfWork.Embeddings.GetBySubjectIdAsync(subjectId))
                .Select(e => e.ChunkId)
                .ToHashSet()
            : [];

        IEnumerable<Model.Entities.DocumentChunk> filtered = chunks;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filtered = filtered.Where(c =>
                c.Content.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                (c.Chapter?.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (c.Document?.FileName?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return filtered.Select(c => new ChunkDto
        {
            Id = c.Id,
            SubjectId = c.SubjectId,
            SubjectName = c.Subject?.Name ?? string.Empty,
            ChapterId = c.ChapterId,
            ChapterName = c.Chapter?.Name ?? string.Empty,
            DocumentId = c.DocumentId,
            DocumentName = c.Document?.FileName ?? string.Empty,
            ChunkIndex = c.ChunkIndex,
            Content = c.Content,
            HasEmbedding = role == Roles.Admin && embeddingChunkIds.Contains(c.Id)
        }).ToList();
    }

    public async Task<IReadOnlyList<ChunkDto>> GetByDocumentAsync(int documentId, string userId, string role)
    {
        if (role == Roles.Student)
            return Array.Empty<ChunkDto>();

        var chunks = await _unitOfWork.Chunks.GetByDocumentIdAsync(documentId);
        if (chunks.Count == 0)
            return Array.Empty<ChunkDto>();

        var subjectId = chunks[0].SubjectId;
        if (role == Roles.Teacher && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, subjectId))
            return Array.Empty<ChunkDto>();

        if (role != Roles.Admin && role != Roles.Teacher)
            return Array.Empty<ChunkDto>();

        var embeddingChunkIds = role == Roles.Admin
            ? (await _unitOfWork.Embeddings.GetBySubjectIdAsync(subjectId))
                .Where(e => e.DocumentId == documentId)
                .Select(e => e.ChunkId)
                .ToHashSet()
            : [];

        return chunks.Select(c => new ChunkDto
        {
            Id = c.Id,
            SubjectId = c.SubjectId,
            SubjectName = c.Subject?.Name ?? string.Empty,
            ChapterId = c.ChapterId,
            ChapterName = c.Chapter?.Name ?? string.Empty,
            DocumentId = c.DocumentId,
            DocumentName = c.Document?.FileName ?? string.Empty,
            ChunkIndex = c.ChunkIndex,
            Content = c.Content,
            HasEmbedding = role == Roles.Admin && embeddingChunkIds.Contains(c.Id)
        }).ToList();
    }

    public async Task<ChunkDto?> GetByIdAsync(int id, string userId, string role)
    {
        if (role == Roles.Student)
            return null;

        var chunk = await _unitOfWork.Chunks.GetByIdAsync(id);
        if (chunk == null) return null;

        if (role == Roles.Teacher && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, chunk.SubjectId))
            return null;

        if (role != Roles.Admin && role != Roles.Teacher)
            return null;

        var subject = await _unitOfWork.Subjects.GetByIdAsync(chunk.SubjectId);
        var chapter = await _unitOfWork.Chapters.GetByIdAsync(chunk.ChapterId);
        var document = await _unitOfWork.Documents.GetByIdAsync(chunk.DocumentId);
        var hasEmbedding = role == Roles.Admin &&
            await _unitOfWork.Embeddings.GetByChunkIdAsync(chunk.Id) != null;

        return new ChunkDto
        {
            Id = chunk.Id,
            SubjectId = chunk.SubjectId,
            SubjectName = subject?.Name ?? string.Empty,
            ChapterId = chunk.ChapterId,
            ChapterName = chapter?.Name ?? string.Empty,
            DocumentId = chunk.DocumentId,
            DocumentName = document?.FileName ?? string.Empty,
            ChunkIndex = chunk.ChunkIndex,
            Content = chunk.Content,
            HasEmbedding = hasEmbedding
        };
    }

    public async Task<ChunkOperationResultDto> CreateAsync(CreateChunkDto dto, string userId, string role)
    {
        if (!CanManageChunks(role))
            return Fail("Bạn không có quyền tạo đoạn (chunk).");

        if (string.IsNullOrWhiteSpace(dto.Content))
            return Fail("Nội dung đoạn (chunk) không được để trống.");

        var chapter = await _unitOfWork.Chapters.GetByIdAsync(dto.ChapterId);
        if (chapter == null || chapter.SubjectId != dto.SubjectId)
            return Fail("Chương không thuộc môn học đã chọn.");

        var document = await _unitOfWork.Documents.GetByIdAsync(dto.DocumentId);
        if (document == null || document.ChapterId != dto.ChapterId || document.SubjectId != dto.SubjectId)
            return Fail("Tài liệu không thuộc chương đã chọn.");

        if (role == Roles.Teacher && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, dto.SubjectId))
            return Fail("Bạn chưa được phân công cho môn học này.");

        var existing = await _unitOfWork.Chunks.GetByDocumentIdAsync(dto.DocumentId);
        var chunkIndex = dto.ChunkIndex ?? (existing.Count == 0 ? 0 : existing.Max(c => c.ChunkIndex) + 1);

        var chunk = new DocumentChunk
        {
            SubjectId = dto.SubjectId,
            ChapterId = dto.ChapterId,
            DocumentId = dto.DocumentId,
            Content = dto.Content.Trim(),
            ChunkIndex = chunkIndex
        };

        await _unitOfWork.Chunks.AddAsync(chunk);
        await _unitOfWork.SaveChangesAsync();

        var result = await GetByIdAsync(chunk.Id, userId, role);
        await _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "Chunk",
            Action = "Created",
            EntityId = chunk.Id,
            Message = $"Chunk #{chunk.ChunkIndex} created for document {document.FileName}"
        });

        return new ChunkOperationResultDto { Success = true, Chunk = result };
    }

    public async Task<ChunkOperationResultDto> UpdateAsync(UpdateChunkDto dto, string userId, string role)
    {
        if (!CanManageChunks(role))
            return Fail("Bạn không có quyền cập nhật đoạn (chunk).");

        if (string.IsNullOrWhiteSpace(dto.Content))
            return Fail("Nội dung đoạn (chunk) không được để trống.");

        var chunk = await _unitOfWork.Chunks.GetByIdAsync(dto.Id);
        if (chunk == null)
            return Fail("Không tìm thấy đoạn (chunk).");

        if (role == Roles.Teacher && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, chunk.SubjectId))
            return Fail("Bạn chưa được phân công cho môn học này.");

        chunk.Content = dto.Content.Trim();
        _unitOfWork.Chunks.Update(chunk);
        await _unitOfWork.SaveChangesAsync();

        var result = await GetByIdAsync(chunk.Id, userId, role);
        await _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "Chunk",
            Action = "Updated",
            EntityId = chunk.Id,
            Message = $"Chunk #{chunk.ChunkIndex} updated"
        });

        return new ChunkOperationResultDto { Success = true, Chunk = result };
    }

    public async Task<bool> DeleteAsync(int id, string userId, string role)
    {
        if (!CanManageChunks(role))
            return false;

        var chunk = await _unitOfWork.Chunks.GetByIdAsync(id);
        if (chunk == null)
            return false;

        if (role == Roles.Teacher && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, chunk.SubjectId))
            return false;

        var embedding = await _unitOfWork.Embeddings.GetByChunkIdAsync(id);
        if (embedding != null)
            _unitOfWork.Embeddings.Remove(embedding);

        _unitOfWork.Chunks.Remove(chunk);
        await _unitOfWork.SaveChangesAsync();

        await _notificationService.NotifyAsync(new RealtimeEventDto
        {
            EntityType = "Chunk",
            Action = "Deleted",
            EntityId = id,
            Message = $"Chunk #{chunk.ChunkIndex} deleted"
        });

        return true;
    }

    private static bool CanManageChunks(string role) =>
        role == Roles.Teacher;

    private static ChunkOperationResultDto Fail(string message) =>
        new() { Success = false, ErrorMessage = message };

    private async Task TryCreateEmbeddingAsync(DocumentChunk chunk)
    {
        var vector = await _geminiAiService.EmbedTextAsync(chunk.Content);
        await _unitOfWork.Embeddings.AddAsync(new DocumentEmbedding
        {
            ChunkId = chunk.Id,
            SubjectId = chunk.SubjectId,
            ChapterId = chunk.ChapterId,
            DocumentId = chunk.DocumentId,
            EmbeddingVector = VectorHelper.Serialize(vector)
        });
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task TryRegenerateEmbeddingAsync(DocumentChunk chunk)
    {
        var vector = await _geminiAiService.EmbedTextAsync(chunk.Content);
        var embedding = await _unitOfWork.Embeddings.GetByChunkIdAsync(chunk.Id);
        if (embedding == null)
        {
            await TryCreateEmbeddingAsync(chunk);
            return;
        }

        embedding.EmbeddingVector = VectorHelper.Serialize(vector);
        _unitOfWork.Embeddings.Update(embedding);
        await _unitOfWork.SaveChangesAsync();
    }
}
