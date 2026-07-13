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



[Authorize]

public class IndexModel : PageModel

{

    private readonly ISubjectService _subjectService;

    private readonly IUserManagementService _userService;



    public IndexModel(ISubjectService subjectService, IUserManagementService userService)

    {

        _subjectService = subjectService;

        _userService = userService;

    }



    public SubjectIndexViewModel ViewModel { get; set; } = new();

    public SelectList TeacherOptions { get; set; } = null!;



    public async Task OnGetAsync()

    {

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var role = User.IsInRole(Roles.Admin) ? Roles.Admin

            : User.IsInRole(Roles.Teacher) ? Roles.Teacher

            : Roles.Student;



        ViewModel.Subjects = await _subjectService.GetAllAsync(userId, role);



        if (User.IsInRole(Roles.Admin))

            await LoadTeacherOptionsAsync();

    }



    public async Task<IActionResult> OnPostAssignTeacherAsync(int subjectId, string teacherId)

    {

        if (!User.IsInRole(Roles.Admin))

            return Forbid();



        var normalizedTeacherId = string.IsNullOrWhiteSpace(teacherId) ? null : teacherId.Trim();

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        var result = await _subjectService.AssignTeacherAsync(new AssignTeacherDto

        {

            SubjectId = subjectId,

            TeacherId = normalizedTeacherId ?? string.Empty

        }, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (IsAjaxRequest())
        {
            return new JsonResult(new
            {
                success = result.Success,
                message = result.Success
                    ? (normalizedTeacherId == null ? "Đã gỡ giáo viên khỏi môn học." : "Đã cập nhật giáo viên cho môn học.")
                    : null,
                error = result.Success ? null : (result.ErrorMessage ?? "Không thể cập nhật giáo viên.")
            });
        }

        if (!result.Success)

            TempData["AssignTeacherError"] = result.ErrorMessage ?? "Không thể cập nhật giáo viên.";

        else

            TempData["AssignTeacherSuccess"] = normalizedTeacherId == null

                ? "Đã gỡ giáo viên khỏi môn học."

                : "Đã cập nhật giáo viên cho môn học.";



        return RedirectToPage();

    }

    public async Task<IActionResult> OnPostHideSubjectAsync(int subjectId)
    {
        if (!User.IsInRole(Roles.Admin))
            return Forbid();

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _subjectService.DeleteAsync(subjectId, adminId, IpAddressHelper.GetClientIp(HttpContext));

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
            TempData["AssignTeacherError"] = result.ErrorMessage ?? "Không thể ẩn môn học.";
        else
            TempData["AssignTeacherSuccess"] = "Đã ẩn môn học khỏi giáo viên và sinh viên.";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRestoreSubjectAsync(int subjectId)
    {
        if (!User.IsInRole(Roles.Admin))
            return Forbid();

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _subjectService.RestoreAsync(subjectId, adminId, IpAddressHelper.GetClientIp(HttpContext));

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
            TempData["AssignTeacherError"] = result.ErrorMessage ?? "Không thể khôi phục môn học.";
        else
            TempData["AssignTeacherSuccess"] = "Đã khôi phục môn học.";

        return RedirectToPage();
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

    private async Task LoadTeacherOptionsAsync()

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



        TeacherOptions = new SelectList(choices, "Id", "Label");

    }

}

