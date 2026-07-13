using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduAI.Web.Pages.Subjects;

[Authorize(Policy = "AdminOnly")]
public class EditModel : PageModel
{
    private readonly ISubjectService _subjectService;
    private readonly IUserManagementService _userService;

    public EditModel(ISubjectService subjectService, IUserManagementService userService)
    {
        _subjectService = subjectService;
        _userService = userService;
    }

    [BindProperty]
    public SubjectFormViewModel Input { get; set; } = new();

    public SelectList TeacherOptions { get; set; } = null!;
    public string? CurrentTeacherName { get; set; }
    public bool IsTeacherLocked { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var subject = await _subjectService.GetByIdAsync(id, null, Roles.Admin);
        if (subject == null) return NotFound();

        await LoadFormAsync(subject);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var existing = await _subjectService.GetByIdAsync(Input.Id, null, Roles.Admin);
        if (existing == null) return NotFound();

        IsTeacherLocked = !string.IsNullOrEmpty(existing.TeacherId);
        CurrentTeacherName = existing.TeacherName;
        await LoadTeacherOptionsAsync(Input.TeacherId);

        if (!ModelState.IsValid) return Page();

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var ipAddress = IpAddressHelper.GetClientIp(HttpContext);

        var updateResult = await _subjectService.UpdateAsync(new UpdateSubjectDto
        {
            Id = Input.Id,
            Name = Input.Name,
            Description = Input.Description
        }, adminId, ipAddress);

        if (!updateResult.Success)
        {
            ModelState.AddModelError(string.Empty, updateResult.ErrorMessage ?? "Failed to update subject.");
            return Page();
        }

        var assignResult = await _subjectService.AssignTeacherAsync(new AssignTeacherDto
        {
            SubjectId = Input.Id,
            TeacherId = Input.TeacherId
        }, adminId, ipAddress);

        if (!assignResult.Success)
        {
            ModelState.AddModelError(string.Empty, assignResult.ErrorMessage ?? "Failed to assign teacher.");
            return Page();
        }

        return RedirectToPage("Details", new { id = Input.Id });
    }

    private async Task LoadFormAsync(SubjectDto subject)
    {
        Input = new SubjectFormViewModel
        {
            Id = subject.Id,
            Name = subject.Name,
            Description = subject.Description,
            TeacherId = subject.TeacherId
        };

        CurrentTeacherName = subject.TeacherName;
        IsTeacherLocked = !string.IsNullOrEmpty(subject.TeacherId);
        await LoadTeacherOptionsAsync(subject.TeacherId);
    }

    private async Task LoadTeacherOptionsAsync(string? selectedTeacherId)
    {
        var teachers = (await _userService.GetTeachersAsync()).ToList();

        if (!string.IsNullOrEmpty(selectedTeacherId) &&
            teachers.All(t => t.Id != selectedTeacherId))
        {
            var currentTeacher = await _userService.GetUserByIdAsync(selectedTeacherId);
            if (currentTeacher != null)
                teachers.Insert(0, currentTeacher);
        }

        var allSubjects = await _subjectService.GetAllAsync(null, Roles.Admin);
        var subjectCountByTeacher = allSubjects
            .Where(s => !string.IsNullOrEmpty(s.TeacherId))
            .GroupBy(s => s.TeacherId!)
            .ToDictionary(g => g.Key, g => g.Count());

        var teacherChoices = teachers.Select(t =>
        {
            var count = subjectCountByTeacher.GetValueOrDefault(t.Id, 0);
            var label = count == 0
                ? $"{t.FullName} (no subjects yet)"
                : $"{t.FullName} ({count} subject{(count == 1 ? "" : "s")})";
            return new { t.Id, Label = label };
        }).ToList();

        Input.Teachers = teachers;
        TeacherOptions = new SelectList(teacherChoices, "Id", "Label", selectedTeacherId);
    }
}
