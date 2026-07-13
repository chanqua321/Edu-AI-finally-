using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.IRepository;
using EduAI.Model.Settings;
using Microsoft.Extensions.Options;

namespace EduAI.BusinessLogic.Services;

public class IndexingSettingsService : IIndexingSettingsService
{
    private const int MinChunkSize = 200;
    private const int MaxChunkSize = 8000;
    private const int MaxOverlap = 2000;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly IndexingSettingsDefaults _defaults;

    public IndexingSettingsService(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        IOptions<IndexingSettingsDefaults> defaults)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _defaults = defaults.Value;
    }

    public async Task<IndexingSettingsDto> GetAsync()
    {
        var settings = await EnsureSettingsAsync();
        return MapToDto(settings);
    }

    public async Task<IndexingSettingsOperationResultDto> UpdateAsync(
        UpdateIndexingSettingsDto dto,
        string adminUserId,
        string? ipAddress)
    {
        var validationError = Validate(dto.ChunkSize, dto.ChunkOverlap);
        if (validationError != null)
        {
            return new IndexingSettingsOperationResultDto
            {
                Success = false,
                ErrorMessage = validationError
            };
        }

        var settings = await EnsureSettingsAsync();
        var previousSize = settings.ChunkSize;
        var previousOverlap = settings.ChunkOverlap;

        settings.ChunkSize = dto.ChunkSize;
        settings.ChunkOverlap = dto.ChunkOverlap;
        settings.UpdatedAt = DateTime.UtcNow;
        settings.UpdatedByUserId = string.IsNullOrEmpty(adminUserId) ? null : adminUserId;
        _unitOfWork.IndexingSettings.Update(settings);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminUserId,
            Action = AuditActions.UpdateIndexingSettings,
            IpAddress = ipAddress,
            Details = $"ChunkSize: {previousSize} -> {dto.ChunkSize}, ChunkOverlap: {previousOverlap} -> {dto.ChunkOverlap}"
        });

        var refreshed = await _unitOfWork.IndexingSettings.GetAsync();
        return new IndexingSettingsOperationResultDto
        {
            Success = true,
            Settings = refreshed == null ? MapToDto(settings) : MapToDto(refreshed)
        };
    }

    private async Task<IndexingSettings> EnsureSettingsAsync()
    {
        var existing = await _unitOfWork.IndexingSettings.GetAsync();
        if (existing != null)
            return existing;

        var created = new IndexingSettings
        {
            ChunkSize = _defaults.ChunkSize,
            ChunkOverlap = _defaults.ChunkOverlap,
            UpdatedAt = DateTime.UtcNow
        };
        await _unitOfWork.IndexingSettings.AddAsync(created);
        await _unitOfWork.SaveChangesAsync();
        return created;
    }

    private static IndexingSettingsDto MapToDto(IndexingSettings settings) =>
        new()
        {
            ChunkSize = settings.ChunkSize,
            ChunkOverlap = settings.ChunkOverlap,
            UpdatedAt = settings.UpdatedAt,
            UpdatedByName = settings.UpdatedBy?.FullName ?? settings.UpdatedBy?.Email
        };

    private static string? Validate(int chunkSize, int chunkOverlap)
    {
        if (chunkSize < MinChunkSize || chunkSize > MaxChunkSize)
            return $"Kích thước chunk phải từ {MinChunkSize} đến {MaxChunkSize} ký tự.";

        if (chunkOverlap < 0 || chunkOverlap > MaxOverlap)
            return $"Độ chồng lấp phải từ 0 đến {MaxOverlap} ký tự.";

        if (chunkOverlap >= chunkSize)
            return "Độ chồng lấp phải nhỏ hơn kích thước chunk.";

        return null;
    }
}
