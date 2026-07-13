using EduAI.Model.DTOs;

namespace EduAI.BusinessLogic.IService;

public interface ISubjectService
{
    Task<IReadOnlyList<SubjectDto>> GetAllAsync(string? userId, string role, bool studentIndexedOnly = false);
    Task<SubjectDto?> GetByIdAsync(int id, string? userId, string role);
    Task<SubjectOperationResultDto> CreateAsync(CreateSubjectDto dto, string adminId, string? ipAddress);
    Task<SubjectOperationResultDto> UpdateAsync(UpdateSubjectDto dto, string adminId, string? ipAddress);
    Task<SubjectOperationResultDto> DeleteAsync(int id, string adminId, string? ipAddress);
    Task<SubjectOperationResultDto> RestoreAsync(int id, string adminId, string? ipAddress);
    Task<AssignTeacherResultDto> AssignTeacherAsync(AssignTeacherDto dto, string adminId, string? ipAddress);
    Task<IReadOnlyList<SubjectAssignmentDto>> GetAssignmentHistoryAsync(int subjectId);
    Task<bool> IsTeacherAssignedToSubjectAsync(string teacherId, int subjectId);
    Task<bool> CanTeacherAccessSubjectAsync(string teacherId, int subjectId);
    Task<bool> HasMaterialsAsync(int subjectId);
    Task<bool> HasDocumentsAsync(int subjectId);
}
