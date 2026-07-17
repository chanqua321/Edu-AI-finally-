using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduAI.Web.Pages.Chat;

[Authorize(Policy = "StudentOnly")]
public class CreateModel : PageModel
{
    private readonly IChatService _chatService;
    private readonly ISubjectService _subjectService;

    public CreateModel(IChatService chatService, ISubjectService subjectService)
    {
        _chatService = chatService;
        _subjectService = subjectService;
    }

    [BindProperty]
    public ChatCreateSessionViewModel Input { get; set; } = new();

    public SelectList SubjectOptions { get; set; } = null!;

    public bool NoSubjectsAvailable { get; set; }
    public bool ShowSubjectPicker { get; set; }
    public string? SelectedSubjectName { get; set; }

    public async Task OnGetAsync(int? subjectId)
    {
        await LoadSubjectOptionsAsync(subjectId);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadSubjectOptionsAsync(Input.SubjectId);

        if (!ModelState.IsValid)
            return Page();

        var studentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var result = await _chatService.CreateSessionAsync(new CreateChatSessionDto
        {
            StudentId = studentId,
            SubjectId = Input.SubjectId,
            Title = string.Empty
        }, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not create chat session.");
            return Page();
        }

        return RedirectToPage("Details", new { id = result.Session!.Id });
    }

    private async Task LoadSubjectOptionsAsync(int? selectedId = null)
    {
        var studentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var subjects = await _subjectService.GetAllAsync(studentId, Roles.Student);
        NoSubjectsAvailable = subjects.Count == 0;

        if (selectedId.HasValue && subjects.Any(s => s.Id == selectedId.Value))
            Input.SubjectId = selectedId.Value;
        else if (Input.SubjectId <= 0 && subjects.Count > 0)
            Input.SubjectId = subjects[0].Id;

        var active = subjects.FirstOrDefault(s => s.Id == Input.SubjectId);
        if (active != null)
            SelectedSubjectName = active.Name;

        // Always allow picking another subject when more than one is available.
        ShowSubjectPicker = subjects.Count > 1;

        SubjectOptions = new SelectList(
            subjects.Select(s => new { s.Id, Name = s.Name }),
            "Id",
            "Name",
            Input.SubjectId > 0 ? Input.SubjectId : selectedId);
    }
}
