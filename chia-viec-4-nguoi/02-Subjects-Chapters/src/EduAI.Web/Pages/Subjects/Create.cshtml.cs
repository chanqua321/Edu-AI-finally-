using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Subjects;

[Authorize(Policy = "AdminOnly")]
public class CreateModel : PageModel
{
    private readonly ISubjectService _subjectService;

    public CreateModel(ISubjectService subjectService)
    {
        _subjectService = subjectService;
    }

    [BindProperty]
    public SubjectFormViewModel Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _subjectService.CreateAsync(new CreateSubjectDto
        {
            Name = Input.Name,
            Description = Input.Description
        }, adminId, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Failed to create subject.");
            return Page();
        }

        return RedirectToPage("Details", new { id = result.Subject!.Id });
    }
}
