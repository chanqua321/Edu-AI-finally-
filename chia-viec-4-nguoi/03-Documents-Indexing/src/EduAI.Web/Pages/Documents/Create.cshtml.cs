using System.Text.Json;
using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.Model.Enums;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using EduAI.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using EduAI.Model.Settings;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;

namespace EduAI.Web.Pages.Documents;

[Authorize(Policy = "TeacherOnly")]
public class CreateModel : PageModel
{
    private readonly IDocumentService _documentService;
    private readonly ISubjectService _subjectService;
    private readonly IChapterService _chapterService;
    private readonly ILessonService _lessonService;
    private readonly AppSettings _appSettings;

    public CreateModel(
        IDocumentService documentService,
        ISubjectService subjectService,
        IChapterService chapterService,
        ILessonService lessonService,
        IOptions<AppSettings> appSettings)
    {
        _documentService = documentService;
        _subjectService = subjectService;
        _chapterService = chapterService;
        _lessonService = lessonService;
        _appSettings = appSettings.Value;
    }

    [BindProperty]
    public DocumentCreateViewModel Input { get; set; } = new();

    [BindProperty]
    public IFormFile? UploadFile { get; set; }

    public SelectList SubjectOptions { get; set; } = null!;
    public SelectList CategoryOptions { get; set; } = null!;
    public bool NoSubjectsAvailable { get; set; }
    public bool CanUpload { get; set; }
    public int MaxUploadMb { get; set; }
    public bool IsTeacher { get; set; }
    public string LessonsByChapterJson { get; set; } = "{}";

    public async Task<IActionResult> OnGetAsync(int? subjectId, int? chapterId, int? lessonId)
    {
        if (chapterId.HasValue && chapterId.Value > 0)
            Input.ChapterId = chapterId.Value;
        if (lessonId.HasValue && lessonId.Value > 0)
            Input.LessonId = lessonId.Value;

        await LoadPageAsync(subjectId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        IsTeacher = User.IsInRole(Roles.Teacher);

        if (UploadFile == null || UploadFile.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng chọn file tài liệu.");
            await LoadPageAsync(Input.SubjectId);
            return Page();
        }

        await LoadPageAsync(Input.SubjectId);

        if (NoSubjectsAvailable)
        {
            ModelState.AddModelError(string.Empty, "No subjects available for upload.");
            return Page();
        }

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;

        var chapterId = await ResolveChapterIdAsync(userId, role);
        if (chapterId <= 0)
            return Page();

        var lessonId = await ResolveLessonIdAsync(chapterId, userId, role);
        if (lessonId <= 0)
            return Page();

        await using var stream = UploadFile.OpenReadStream();
        var result = await _documentService.UploadAsync(new UploadDocumentDto
        {
            SubjectId = Input.SubjectId,
            ChapterId = chapterId,
            LessonId = lessonId,
            UploadedByUserId = userId,
            UploaderRole = role,
            FileName = UploadFile.FileName,
            Category = Input.Category,
            FileStream = stream,
            ContentType = UploadFile.ContentType,
            FileSizeBytes = UploadFile.Length
        }, IpAddressHelper.GetClientIp(HttpContext));

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Tải lên thất bại.");
            return Page();
        }

        return RedirectToPage("Details", new { id = result.DocumentId, showChunks = true });
    }

    private async Task<int> ResolveChapterIdAsync(string userId, string role)
    {
        var picker = Input.ChapterPicker?.Trim();
        if (string.IsNullOrWhiteSpace(picker))
        {
            ModelState.AddModelError(nameof(Input.ChapterPicker), "Chọn hoặc nhập tên chương.");
            return 0;
        }

        var chapters = await _chapterService.GetBySubjectAsync(Input.SubjectId, userId, role);
        var existing = chapters.FirstOrDefault(c =>
            string.Equals(c.Name, picker, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing.Id;

        var nextOrder = chapters.Count == 0 ? 1 : chapters.Max(c => c.OrderNumber) + 1;
        var created = await _chapterService.CreateAsync(new CreateChapterDto
        {
            SubjectId = Input.SubjectId,
            Name = picker,
            OrderNumber = nextOrder
        }, userId, role);

        return created.Id;
    }

    private async Task<int> ResolveLessonIdAsync(int chapterId, string userId, string role)
    {
        var lessons = await _lessonService.GetByChapterAsync(chapterId, userId, role);
        var picker = Input.LessonPicker?.Trim();

        if (string.IsNullOrWhiteSpace(picker))
        {
            if (lessons.Count > 0)
                return lessons.OrderBy(l => l.OrderNumber).ThenBy(l => l.Id).First().Id;

            ModelState.AddModelError(nameof(Input.LessonPicker), "Chọn hoặc nhập tên bài.");
            return 0;
        }

        var existing = lessons.FirstOrDefault(l =>
            string.Equals(l.Name, picker, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing.Id;

        var nextOrder = lessons.Count == 0 ? 1 : lessons.Max(l => l.OrderNumber) + 1;
        try
        {
            var created = await _lessonService.CreateAsync(new CreateLessonDto
            {
                ChapterId = chapterId,
                Name = picker,
                OrderNumber = nextOrder
            }, userId, role);
            return created.Id;
        }
        catch (UnauthorizedAccessException)
        {
            ModelState.AddModelError(string.Empty, "Bạn không có quyền tạo bài cho môn này.");
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(Input.LessonPicker), ex.Message);
            return 0;
        }
    }

    private async Task LoadPageAsync(int? subjectId)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var role = User.IsInRole(Roles.Admin) ? Roles.Admin : Roles.Teacher;
        IsTeacher = role == Roles.Teacher;

        var subjects = await _subjectService.GetAllAsync(userId, role);
        NoSubjectsAvailable = subjects.Count == 0;

        if (subjects.Count == 0)
        {
            SubjectOptions = new SelectList(Array.Empty<object>(), "Id", "Name");
            CanUpload = false;
            return;
        }

        var activeSubjectId = subjectId ?? Input.SubjectId;
        if (activeSubjectId <= 0 || subjects.All(s => s.Id != activeSubjectId))
            activeSubjectId = subjects[0].Id;

        Input.SubjectId = activeSubjectId;
        var chapters = await _chapterService.GetBySubjectAsync(activeSubjectId, userId, role);
        Input.AvailableChapters = chapters;

        if (Input.ChapterId > 0 && string.IsNullOrWhiteSpace(Input.ChapterPicker))
        {
            var chapter = chapters.FirstOrDefault(c => c.Id == Input.ChapterId);
            if (chapter != null)
                Input.ChapterPicker = chapter.Name;
        }

        if (string.IsNullOrWhiteSpace(Input.ChapterPicker) && chapters.Count > 0)
            Input.ChapterPicker = chapters[0].Name;

        var activeChapter = chapters.FirstOrDefault(c =>
            string.Equals(c.Name, Input.ChapterPicker?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? chapters.FirstOrDefault();

        var allLessons = await _lessonService.GetBySubjectAsync(activeSubjectId, userId, role);
        LessonsByChapterJson = JsonSerializer.Serialize(
            allLessons
                .GroupBy(l => l.ChapterId)
                .ToDictionary(
                    g => g.Key.ToString(),
                    g => g.OrderBy(l => l.OrderNumber).ThenBy(l => l.Id)
                        .Select(l => new { l.Id, l.Name })
                        .ToList()),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        if (activeChapter != null)
        {
            Input.AvailableLessons = allLessons
                .Where(l => l.ChapterId == activeChapter.Id)
                .OrderBy(l => l.OrderNumber).ThenBy(l => l.Id)
                .ToList();

            if (Input.LessonId > 0 && string.IsNullOrWhiteSpace(Input.LessonPicker))
            {
                var lesson = Input.AvailableLessons.FirstOrDefault(l => l.Id == Input.LessonId);
                if (lesson != null)
                    Input.LessonPicker = lesson.Name;
            }

            if (string.IsNullOrWhiteSpace(Input.LessonPicker))
                Input.LessonPicker = Input.AvailableLessons.FirstOrDefault()?.Name;
        }
        else
        {
            Input.AvailableLessons = Array.Empty<LessonDto>();
        }

        CanUpload = true;
        MaxUploadMb = (int)(_appSettings.MaxUploadBytes / (1024 * 1024));

        SubjectOptions = new SelectList(
            subjects.Select(s => new { s.Id, Name = s.Name }),
            "Id", "Name", activeSubjectId);

        CategoryOptions = new SelectList(
            Enum.GetValues<DocumentCategory>().Select(c => new { Value = (int)c, Text = c.ToVietnameseLabel() }),
            "Value", "Text", (int)Input.Category);
    }
}
