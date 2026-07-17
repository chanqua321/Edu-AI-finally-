using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Entities;
using EduAI.Model.Enums;
using EduAI.Model.IRepository;
using EduAI.BusinessLogic.IService;
using Microsoft.AspNetCore.Identity;

namespace EduAI.BusinessLogic.Services;

public class SubjectService : ISubjectService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubjectNotificationService _subjectNotificationService;

    public SubjectService(
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        UserManager<ApplicationUser> userManager,
        ISubjectNotificationService subjectNotificationService)
    {
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _userManager = userManager;
        _subjectNotificationService = subjectNotificationService;
    }

    public async Task<IReadOnlyList<SubjectDto>> GetAllAsync(string? userId, string role, bool studentIndexedOnly = false)
    {
        IReadOnlyList<Subject> subjects = role switch
        {
            Roles.Admin => await _unitOfWork.Subjects.GetAllWithTeacherAsync(includeInactive: true),
            Roles.Teacher => string.IsNullOrEmpty(userId)
                ? Array.Empty<Subject>()
                : await _unitOfWork.Subjects.GetForTeacherWithHistoryAsync(userId),
            Roles.Student => studentIndexedOnly
                ? await GetSubjectsWithIndexedMaterialsAsync()
                : await GetSubjectsWithDocumentsAsync(),
            _ => Array.Empty<Subject>()
        };

        var result = new List<SubjectDto>();
        foreach (var subject in subjects)
        {
            var dto = MapToDto(subject);
            await EnrichMaterialStatsAsync(dto, subject.Id);
            result.Add(dto);
        }

        return result;
    }

    public async Task<SubjectDto?> GetByIdAsync(int id, string? userId, string role)
    {
        var subject = await _unitOfWork.Subjects.GetWithTeacherAsync(id);
        if (subject == null) return null;

        if (!subject.IsActive && role != Roles.Admin)
            return null;

        if (role == Roles.Teacher && !string.IsNullOrEmpty(userId) &&
            !await IsTeacherAssignedToSubjectAsync(userId, subject.Id))
            return null;

        var dto = MapToDto(subject);
        await EnrichMaterialStatsAsync(dto, subject.Id);

        // Students can see (and download from) any subject that has uploaded documents,
        // even if indexing has not finished yet. Chat still requires indexed chunks.
        if (role == Roles.Student && dto.DocumentCount == 0)
            return null;

        return dto;
    }

    public async Task<bool> HasMaterialsAsync(int subjectId)
    {
        var chunks = await _unitOfWork.Chunks.GetBySubjectIdAsync(subjectId);
        return chunks.Count > 0;
    }

    public async Task<bool> HasDocumentsAsync(int subjectId)
    {
        var documents = await _unitOfWork.Documents.GetBySubjectIdAsync(subjectId);
        return documents.Count > 0;
    }

    public async Task<SubjectOperationResultDto> CreateAsync(CreateSubjectDto dto, string adminId, string? ipAddress)
    {
        var name = dto.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Vui lòng nhập tên môn học."
            };
        }

        if (await _unitOfWork.Subjects.ExistsByNameAsync(name))
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Subject '{name}' already exists."
            };
        }

        var subject = new Subject
        {
            Name = name,
            Description = dto.Description?.Trim()
        };

        await _unitOfWork.Subjects.AddAsync(subject);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.CreateSubject,
            IpAddress = ipAddress,
            Details = $"Created subject: {subject.Name}"
        });

        var subjectDto = MapToDto(subject);
        await EnrichMaterialStatsAsync(subjectDto, subject.Id);

        await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
        {
            Action = SubjectRealtimeActions.Created,
            SubjectId = subject.Id,
            Subject = subjectDto
        });

        return new SubjectOperationResultDto
        {
            Success = true,
            Subject = subjectDto
        };
    }

    public async Task<SubjectOperationResultDto> UpdateAsync(UpdateSubjectDto dto, string adminId, string? ipAddress)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(dto.Id);
        if (subject == null)
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Không tìm thấy môn học."
            };
        }

        var name = dto.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Vui lòng nhập tên môn học."
            };
        }

        if (await _unitOfWork.Subjects.ExistsByNameAsync(name, dto.Id))
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = $"Subject '{name}' already exists."
            };
        }

        subject.Name = name;
        subject.Description = dto.Description?.Trim();
        subject.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Subjects.Update(subject);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.UpdateSubject,
            IpAddress = ipAddress,
            Details = $"Updated subject: {subject.Name} (Id: {subject.Id})"
        });

        var updated = await GetByIdAsync(subject.Id, null, Roles.Admin);
        await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
        {
            Action = SubjectRealtimeActions.Updated,
            SubjectId = subject.Id,
            Subject = updated
        });

        return new SubjectOperationResultDto
        {
            Success = true,
            Subject = updated
        };
    }

    public async Task<SubjectOperationResultDto> DeleteAsync(int id, string adminId, string? ipAddress)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(id);
        if (subject == null)
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Không tìm thấy môn học."
            };
        }

        if (!subject.IsActive)
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Môn học đã bị ẩn trước đó."
            };
        }

        subject.IsActive = false;
        subject.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Subjects.Update(subject);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.DeactivateSubject,
            IpAddress = ipAddress,
            Details = $"Hidden subject: {subject.Name} (Id: {id})"
        });

        await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
        {
            Action = SubjectRealtimeActions.Deleted,
            SubjectId = id,
            Subject = await GetByIdAsync(id, null, Roles.Admin)
        });

        return new SubjectOperationResultDto { Success = true };
    }

    public async Task<SubjectOperationResultDto> RestoreAsync(int id, string adminId, string? ipAddress)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(id);
        if (subject == null)
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Không tìm thấy môn học."
            };
        }

        if (subject.IsActive)
        {
            return new SubjectOperationResultDto
            {
                Success = false,
                ErrorMessage = "Môn học đã được hiển thị trước đó."
            };
        }

        subject.IsActive = true;
        subject.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Subjects.Update(subject);
        await _unitOfWork.SaveChangesAsync();

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = AuditActions.RestoreSubject,
            IpAddress = ipAddress,
            Details = $"Restored subject: {subject.Name} (Id: {id})"
        });

        var restored = await GetByIdAsync(subject.Id, null, Roles.Admin);
        await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
        {
            Action = SubjectRealtimeActions.Restored,
            SubjectId = subject.Id,
            Subject = restored
        });

        return new SubjectOperationResultDto
        {
            Success = true,
            Subject = restored
        };
    }

    public async Task<AssignTeacherResultDto> AssignTeacherAsync(AssignTeacherDto dto, string adminId, string? ipAddress)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(dto.SubjectId);
        if (subject == null)
        {
            return new AssignTeacherResultDto
            {
                Success = false,
                ErrorMessage = "Không tìm thấy môn học."
            };
        }

        var previousTeacherId = subject.TeacherId;
        var newTeacherId = string.IsNullOrWhiteSpace(dto.TeacherId) ? null : dto.TeacherId.Trim();

        if (string.Equals(previousTeacherId, newTeacherId, StringComparison.Ordinal))
        {
            return new AssignTeacherResultDto { Success = true };
        }

        ApplicationUser? newTeacher = null;
        if (newTeacherId != null)
        {
            newTeacher = await _userManager.FindByIdAsync(newTeacherId);
            if (newTeacher == null)
            {
                return new AssignTeacherResultDto
                {
                    Success = false,
                    ErrorMessage = "Không tìm thấy tài khoản giáo viên."
                };
            }

            if (!newTeacher.IsActive)
            {
                return new AssignTeacherResultDto
                {
                    Success = false,
                    ErrorMessage = "Không thể gán tài khoản giáo viên đã bị khóa."
                };
            }

            if (!newTeacher.EmailConfirmed)
            {
                return new AssignTeacherResultDto
                {
                    Success = false,
                    ErrorMessage = "Giáo viên chưa xác thực email. Vui lòng yêu cầu giáo viên xác thực trước khi gán."
                };
            }

            var roles = await _userManager.GetRolesAsync(newTeacher);
            if (!roles.Contains(Roles.Teacher))
            {
                return new AssignTeacherResultDto
                {
                    Success = false,
                    ErrorMessage = "Người dùng được chọn không phải giáo viên."
                };
            }
        }

        var now = DateTime.UtcNow;

        // Step 1: End the current assignment without deleting it (keep history).
        var currentAssignment = await _unitOfWork.SubjectAssignments.GetCurrentBySubjectAsync(subject.Id);
        if (currentAssignment != null)
        {
            currentAssignment.EndDate = now;
            currentAssignment.Status = AssignmentStatus.Ended;
            currentAssignment.UpdatedAt = now;
            _unitOfWork.SubjectAssignments.Update(currentAssignment);
        }

        // Step 2: Create a new "Current" assignment for the new teacher (if any).
        if (newTeacherId != null)
        {
            await _unitOfWork.SubjectAssignments.AddAsync(new SubjectAssignment
            {
                SubjectId = subject.Id,
                TeacherId = newTeacherId,
                StartDate = now,
                Status = AssignmentStatus.Current
            });
        }

        // Step 3: Keep Subject.TeacherId as the denormalized "current teacher" pointer.
        subject.TeacherId = newTeacherId;
        subject.UpdatedAt = now;
        _unitOfWork.Subjects.Update(subject);

        await _unitOfWork.SaveChangesAsync();

        // Step 4: Audit log (old/new teacher captured, no data is lost).
        var previousTeacherName = previousTeacherId == null
            ? null
            : (await _userManager.FindByIdAsync(previousTeacherId))?.FullName ?? previousTeacherId;

        string action;
        string auditDetails;
        if (previousTeacherId == null)
        {
            action = AuditActions.AssignTeacher;
            auditDetails = $"Assigned teacher {newTeacher!.FullName} to subject {subject.Name} (Id: {subject.Id})";
        }
        else if (newTeacherId == null)
        {
            action = AuditActions.RemoveTeacher;
            auditDetails = $"Removed teacher {previousTeacherName} from subject {subject.Name} (Id: {subject.Id})";
        }
        else
        {
            action = AuditActions.ChangeTeacher;
            auditDetails = $"Changed teacher of subject {subject.Name} (Id: {subject.Id}): {previousTeacherName} -> {newTeacher!.FullName}";
        }

        await _auditLogService.LogAsync(new CreateAuditLogDto
        {
            UserId = adminId,
            Action = action,
            IpAddress = ipAddress,
            Details = auditDetails
        });

        var assigned = await GetByIdAsync(subject.Id, null, Roles.Admin);
        await _subjectNotificationService.NotifySubjectChangedAsync(new SubjectRealtimeEventDto
        {
            Action = newTeacherId == null ? SubjectRealtimeActions.TeacherUnassigned : SubjectRealtimeActions.TeacherAssigned,
            SubjectId = subject.Id,
            Subject = assigned,
            PreviousTeacherId = previousTeacherId
        });

        return new AssignTeacherResultDto { Success = true };
    }

    public async Task<IReadOnlyList<SubjectAssignmentDto>> GetAssignmentHistoryAsync(int subjectId)
    {
        var assignments = await _unitOfWork.SubjectAssignments.GetHistoryBySubjectAsync(subjectId);
        return assignments.Select(a => new SubjectAssignmentDto
        {
            Id = a.Id,
            TeacherId = a.TeacherId,
            TeacherName = a.Teacher?.FullName ?? a.TeacherId,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            Status = a.Status.ToString(),
            IsCurrent = a.Status == AssignmentStatus.Current
        }).ToList();
    }

    public async Task<bool> IsTeacherAssignedToSubjectAsync(string teacherId, int subjectId)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(subjectId);
        return subject is { IsActive: true, TeacherId: var tid } && tid == teacherId;
    }

    // View-level access: the current teacher OR any teacher who previously taught the subject
    // (so a handed-over teacher keeps read access to material they were responsible for).
    public async Task<bool> CanTeacherAccessSubjectAsync(string teacherId, int subjectId)
    {
        var subject = await _unitOfWork.Subjects.GetByIdAsync(subjectId);
        if (subject is not { IsActive: true })
            return false;

        if (subject.TeacherId == teacherId)
            return true;

        return await _unitOfWork.SubjectAssignments.HasAnyAssignmentAsync(teacherId, subjectId);
    }

    private async Task<IReadOnlyList<Subject>> GetSubjectsWithDocumentsAsync()
    {
        var subjects = await _unitOfWork.Subjects.GetAllWithTeacherAsync();
        var eligible = new List<Subject>();
        foreach (var subject in subjects)
        {
            if (await HasDocumentsAsync(subject.Id))
                eligible.Add(subject);
        }

        return eligible;
    }

    private async Task<IReadOnlyList<Subject>> GetSubjectsWithIndexedMaterialsAsync()
    {
        var subjects = await _unitOfWork.Subjects.GetAllWithTeacherAsync();
        var eligible = new List<Subject>();
        foreach (var subject in subjects)
        {
            if (await HasMaterialsAsync(subject.Id))
                eligible.Add(subject);
        }

        return eligible;
    }

    private async Task EnrichMaterialStatsAsync(SubjectDto dto, int subjectId)
    {
        var documents = await _unitOfWork.Documents.GetBySubjectIdAsync(subjectId);
        var chunks = await _unitOfWork.Chunks.GetBySubjectIdAsync(subjectId);
        dto.DocumentCount = documents.Count;
        dto.ChunkCount = chunks.Count;
        dto.HasMaterials = chunks.Count > 0;
    }

    private static SubjectDto MapToDto(Subject subject) => new()
    {
        Id = subject.Id,
        Name = subject.Name,
        Description = subject.Description,
        TeacherId = subject.TeacherId,
        TeacherName = subject.Teacher?.FullName,
        CreatedAt = subject.CreatedAt,
        IsActive = subject.IsActive
    };
}
