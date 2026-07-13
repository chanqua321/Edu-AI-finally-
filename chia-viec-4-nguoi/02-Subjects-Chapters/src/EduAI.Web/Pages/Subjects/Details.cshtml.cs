using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EduAI.Web.Helpers;

using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduAI.Web.Pages.Subjects;

[Authorize]
public class DetailsModel : PageModel
{
    private readonly ISubjectService _subjectService;
    private readonly IDocumentService _documentService;
    private readonly IChunkService _chunkService;
    private readonly IChapterService _chapterService;
    private readonly ILessonService _lessonService;
    private readonly IUserManagementService _userService;

    public DetailsModel(
        ISubjectService subjectService,
        IDocumentService documentService,
        IChunkService chunkService,
        IChapterService chapterService,
        ILessonService lessonService,
        IUserManagementService userService)
    {
        _subjectService = subjectService;
        _documentService = documentService;
        _chunkService = chunkService;
        _chapterService = chapterService;
        _lessonService = lessonService;
        _userService = userService;
    }

    public SubjectDetailsViewModel ViewModel { get; set; } = new();
    public SelectList TeacherOptions { get; set; } = null!;
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<DocumentDto> Documents { get; set; } = Array.Empty<DocumentDto>();
    public int? ExpandedDocumentId { get; set; }
    public IReadOnlyList<ChunkDto> ExpandedChunks { get; set; } = Array.Empty<ChunkDto>();
    public bool ShowEmbedding { get; set; }
    public IReadOnlyList<SubjectAssignmentDto> AssignmentHistory { get; set; } = Array.Empty<SubjectAssignmentDto>();
    public IReadOnlyList<ChapterDto> Chapters { get; set; } = Array.Empty<ChapterDto>();
    public IReadOnlyList<LessonDto> Lessons { get; set; } = Array.Empty<LessonDto>();
    public bool CanManageChapters { get; set; }
    public bool CanViewCourseStructure { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, int? showChunks)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin
            : User.IsInRole(Roles.Teacher) ? Roles.Teacher : Roles.Student;

        ViewModel.Subject = await _subjectService.GetByIdAsync(id, userId, role);
        if (ViewModel.Subject == null) return NotFound();

        Documents = await _documentService.GetBySubjectAsync(id, userId ?? string.Empty, role);
        ShowEmbedding = role == Roles.Admin;

        if (role == Roles.Admin)
            AssignmentHistory = await _subjectService.GetAssignmentHistoryAsync(id);

        if (role == Roles.Admin)
        {
            await LoadTeacherOptionsAsync(ViewModel.Subject.TeacherId);
            CanViewCourseStructure = true;
            Chapters = await _chapterService.GetBySubjectAsync(id, userId ?? string.Empty, role);
            Lessons = await _lessonService.GetBySubjectAsync(id, userId ?? string.Empty, role);
        }
        else if (role == Roles.Teacher && !string.IsNullOrEmpty(userId))
        {
            CanManageChapters = await _subjectService.IsTeacherAssignedToSubjectAsync(userId, id);
            if (CanManageChapters)
            {
                CanViewCourseStructure = true;
                Chapters = await _chapterService.GetBySubjectAsync(id, userId, role);
                Lessons = await _lessonService.GetBySubjectAsync(id, userId, role);
            }
        }

        if (showChunks.HasValue && role != Roles.Student)
        {
            ExpandedDocumentId = showChunks;
            ExpandedChunks = await _chunkService.GetByDocumentAsync(showChunks.Value, userId ?? string.Empty, role);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAddChapterAsync(int id, string? chapterName)
    {
        var (userId, role) = GetCurrentUser();
        if (role != Roles.Teacher)
            return RedirectToPage("Details", new { id });

        if (!string.IsNullOrWhiteSpace(chapterName))
        {
            var chapters = await _chapterService.GetBySubjectAsync(id, userId, role);
            var nextOrder = chapters.Count == 0 ? 1 : chapters.Max(c => c.OrderNumber) + 1;
            try
            {
                await _chapterService.CreateAsync(new CreateChapterDto
                {
                    SubjectId = id,
                    Name = chapterName.Trim(),
                    OrderNumber = nextOrder
                }, userId, role);
            }
            catch (UnauthorizedAccessException)
            {
                TempData["Error"] = "Bạn không có quyền quản lý chương của môn này.";
            }
        }

        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostRenameChapterAsync(int id, int chapterId, string? chapterName)
    {
        var (userId, role) = GetCurrentUser();
        if (role != Roles.Teacher || string.IsNullOrWhiteSpace(chapterName))
            return RedirectToPage("Details", new { id });

        var existing = await _chapterService.GetByIdAsync(chapterId, userId, role);
        if (existing != null)
        {
            await _chapterService.UpdateAsync(new UpdateChapterDto
            {
                Id = chapterId,
                Name = chapterName.Trim(),
                OrderNumber = existing.OrderNumber
            }, userId, role);
        }

        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostMoveChapterAsync(int id, int chapterId, string direction)
    {
        var (userId, role) = GetCurrentUser();
        if (role != Roles.Teacher)
            return RedirectToPage("Details", new { id });

        var chapters = (await _chapterService.GetBySubjectAsync(id, userId, role))
            .OrderBy(c => c.OrderNumber).ThenBy(c => c.Id).ToList();
        var index = chapters.FindIndex(c => c.Id == chapterId);
        if (index >= 0)
        {
            var swapIndex = direction == "up" ? index - 1 : index + 1;
            if (swapIndex >= 0 && swapIndex < chapters.Count)
            {
                var current = chapters[index];
                var neighbor = chapters[swapIndex];
                var currentOrder = current.OrderNumber;
                var neighborOrder = neighbor.OrderNumber;

                // Guarantee a visible swap even if order numbers were duplicated.
                if (currentOrder == neighborOrder)
                    neighborOrder = direction == "up" ? currentOrder - 1 : currentOrder + 1;

                await _chapterService.UpdateAsync(new UpdateChapterDto
                {
                    Id = current.Id,
                    Name = current.Name,
                    OrderNumber = neighborOrder
                }, userId, role);
                await _chapterService.UpdateAsync(new UpdateChapterDto
                {
                    Id = neighbor.Id,
                    Name = neighbor.Name,
                    OrderNumber = currentOrder
                }, userId, role);
            }
        }

        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostDeleteChapterAsync(int id, int chapterId)
    {
        var (userId, role) = GetCurrentUser();
        if (role != Roles.Teacher)
            return RedirectToPage("Details", new { id });

        await _chapterService.DeleteAsync(chapterId, userId, role, IpAddressHelper.GetClientIp(HttpContext));
        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostAddLessonAsync(int id, int chapterId, string? lessonName)
    {
        var (userId, role) = GetCurrentUser();
        if (role != Roles.Teacher)
            return RedirectToPage("Details", new { id });

        if (!string.IsNullOrWhiteSpace(lessonName))
        {
            var lessons = await _lessonService.GetByChapterAsync(chapterId, userId, role);
            var nextOrder = lessons.Count == 0 ? 1 : lessons.Max(l => l.OrderNumber) + 1;
            try
            {
                await _lessonService.CreateAsync(new CreateLessonDto
                {
                    ChapterId = chapterId,
                    Name = lessonName.Trim(),
                    OrderNumber = nextOrder
                }, userId, role);
            }
            catch (UnauthorizedAccessException)
            {
                TempData["Error"] = "Bạn không có quyền quản lý bài của môn này.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
            }
        }

        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostRenameLessonAsync(int id, int lessonId, string? lessonName)
    {
        var (userId, role) = GetCurrentUser();
        if (role != Roles.Teacher || string.IsNullOrWhiteSpace(lessonName))
            return RedirectToPage("Details", new { id });

        var existing = await _lessonService.GetByIdAsync(lessonId, userId, role);
        if (existing != null)
        {
            await _lessonService.UpdateAsync(new UpdateLessonDto
            {
                Id = lessonId,
                Name = lessonName.Trim(),
                OrderNumber = existing.OrderNumber
            }, userId, role);
        }

        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostMoveLessonAsync(int id, int chapterId, int lessonId, string direction)
    {
        var (userId, role) = GetCurrentUser();
        if (role != Roles.Teacher)
            return RedirectToPage("Details", new { id });

        var lessons = (await _lessonService.GetByChapterAsync(chapterId, userId, role))
            .OrderBy(l => l.OrderNumber).ThenBy(l => l.Id).ToList();
        var index = lessons.FindIndex(l => l.Id == lessonId);
        if (index >= 0)
        {
            var swapIndex = direction == "up" ? index - 1 : index + 1;
            if (swapIndex >= 0 && swapIndex < lessons.Count)
            {
                var current = lessons[index];
                var neighbor = lessons[swapIndex];
                var currentOrder = current.OrderNumber;
                var neighborOrder = neighbor.OrderNumber;
                if (currentOrder == neighborOrder)
                    neighborOrder = direction == "up" ? currentOrder - 1 : currentOrder + 1;

                await _lessonService.UpdateAsync(new UpdateLessonDto
                {
                    Id = current.Id,
                    Name = current.Name,
                    OrderNumber = neighborOrder
                }, userId, role);
                await _lessonService.UpdateAsync(new UpdateLessonDto
                {
                    Id = neighbor.Id,
                    Name = neighbor.Name,
                    OrderNumber = currentOrder
                }, userId, role);
            }
        }

        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostDeleteLessonAsync(int id, int lessonId)
    {
        var (userId, role) = GetCurrentUser();
        if (role != Roles.Teacher)
            return RedirectToPage("Details", new { id });

        await _lessonService.DeleteAsync(lessonId, userId, role, IpAddressHelper.GetClientIp(HttpContext));
        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostReindexDocumentAsync(int id, int documentId)
    {
        var (userId, role) = GetCurrentUser();
        if (role != Roles.Teacher)
            return RedirectToPage("Details", new { id });

        var doc = await _documentService.GetByIdAsync(documentId, userId, role);
        if (doc == null || doc.SubjectId != id)
            return NotFound();

        if (role == Roles.Teacher && !await _subjectService.IsTeacherAssignedToSubjectAsync(userId, id))
            return Forbid();

        var result = await _documentService.ReindexAsync(documentId, userId, role, IpAddressHelper.GetClientIp(HttpContext));
        if (!result.Success)
            TempData["SubjectActionError"] = result.ErrorMessage;

        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostAssignTeacherAsync(int id, string teacherId)
    {
        if (!User.IsInRole(Roles.Admin))
            return Forbid();

        var normalizedTeacherId = string.IsNullOrWhiteSpace(teacherId) ? null : teacherId.Trim();
        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _subjectService.AssignTeacherAsync(new AssignTeacherDto
        {
            SubjectId = id,
            TeacherId = normalizedTeacherId ?? string.Empty
        }, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
            TempData["SubjectActionError"] = result.ErrorMessage ?? "Không thể cập nhật giáo viên.";
        else
            TempData["SubjectActionSuccess"] = normalizedTeacherId == null
                ? "Đã gỡ giáo viên khỏi môn học."
                : "Đã cập nhật giáo viên cho môn học.";

        return RedirectToPage("Details", new { id });
    }

    private async Task LoadTeacherOptionsAsync(string? selectedTeacherId)
    {
        var teachers = (await _userService.GetTeachersAsync()).ToList();
        var allSubjects = await _subjectService.GetAllAsync(null, Roles.Admin);
        var subjectCountByTeacher = allSubjects
            .Where(s => !string.IsNullOrEmpty(s.TeacherId))
            .GroupBy(s => s.TeacherId!)
            .ToDictionary(g => g.Key, g => g.Count());

        var choices = teachers.Select(t =>
        {
            var count = subjectCountByTeacher.GetValueOrDefault(t.Id, 0);
            var suffix = count == 0 ? "chưa có môn" : $"{count} môn";
            return new { t.Id, Label = $"{t.FullName} ({suffix})" };
        }).ToList();

        TeacherOptions = new SelectList(choices, "Id", "Label", selectedTeacherId);
    }

    public async Task<IActionResult> OnPostHideSubjectAsync(int id)
    {
        if (!User.IsInRole(Roles.Admin))
            return Forbid();

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _subjectService.DeleteAsync(id, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success = result.Success,
                message = result.Success ? "Đã ẩn môn học khỏi giáo viên và sinh viên." : null,
                error = result.Success ? null : (result.ErrorMessage ?? "Không thể ẩn môn học.")
            });
        }

        if (!result.Success)
            TempData["SubjectActionError"] = result.ErrorMessage ?? "Không thể ẩn môn học.";
        else
            TempData["SubjectActionSuccess"] = "Đã ẩn môn học khỏi giáo viên và sinh viên.";

        return RedirectToPage("Details", new { id });
    }

    public async Task<IActionResult> OnPostRestoreSubjectAsync(int id)
    {
        if (!User.IsInRole(Roles.Admin))
            return Forbid();

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _subjectService.RestoreAsync(id, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success = result.Success,
                message = result.Success ? "Đã khôi phục môn học." : null,
                error = result.Success ? null : (result.ErrorMessage ?? "Không thể khôi phục môn học.")
            });
        }

        if (!result.Success)
            TempData["SubjectActionError"] = result.ErrorMessage ?? "Không thể khôi phục môn học.";
        else
            TempData["SubjectActionSuccess"] = "Đã khôi phục môn học.";

        return RedirectToPage("Details", new { id });
    }

    private bool IsAjaxRequest()
    {
        if (Request.Headers.TryGetValue("X-Requested-With", out var value) &&
            value.ToString().Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            return true;

        if (Request.Headers.TryGetValue("Accept", out var accept) &&
            accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private (string userId, string role) GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin
            : User.IsInRole(Roles.Teacher) ? Roles.Teacher : Roles.Student;
        return (userId, role);
    }
}
