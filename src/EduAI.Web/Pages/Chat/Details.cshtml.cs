using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Chat;

[Authorize(Policy = "StudentOnly")]
public class DetailsModel : PageModel
{
    private readonly IChatService _chatService;
    private readonly IPaymentService _paymentService;
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly IDocumentService _documentService;

    public DetailsModel(
        IChatService chatService,
        IPaymentService paymentService,
        ISystemSettingsService systemSettingsService,
        IDocumentService documentService)
    {
        _chatService = chatService;
        _paymentService = paymentService;
        _systemSettingsService = systemSettingsService;
        _documentService = documentService;
    }

    public ChatSessionViewModel ViewModel { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        return await LoadPageAsync(id);
    }

    private async Task<IActionResult> LoadPageAsync(int id)
    {
        var studentId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        var session = await _chatService.GetSessionAsync(id, studentId);
        if (session == null) return NotFound();

        ViewModel.SessionId = session.Id;
        ViewModel.SubjectId = session.SubjectId;
        ViewModel.SubjectName = session.SubjectName;
        ViewModel.Title = session.Title;
        ViewModel.Messages = await _chatService.GetMessagesAsync(id, studentId);
        ViewModel.ProviderQuotaOverview = await _paymentService.GetAiProviderQuotaOverviewAsync(studentId);

        var documents = await _documentService.GetBySubjectAsync(session.SubjectId, studentId, Roles.Student);
        ViewModel.Documents = documents
            .OrderBy(d => d.FileName, StringComparer.OrdinalIgnoreCase)
            .Select(d => new ChatDocumentOptionViewModel { Id = d.Id, FileName = d.FileName })
            .ToList();

        var settings = await _systemSettingsService.GetAsync();
        var suggestedProvider = AiProviderIds.Normalize(settings.GenerationProvider);
        var suggestedAvailable = ViewModel.ProviderQuotaOverview.Providers
            .FirstOrDefault(p => p.ProviderId == suggestedProvider && p.IsAvailable);
        var defaultChoice = ViewModel.ProviderQuotaOverview.Providers
            .FirstOrDefault(p => p.IsDefaultChoice && p.IsAvailable);
        var firstAvailable = ViewModel.ProviderQuotaOverview.Providers
            .FirstOrDefault(p => p.IsAvailable);

        ViewModel.DefaultProviderId =
            suggestedAvailable?.ProviderId
            ?? defaultChoice?.ProviderId
            ?? firstAvailable?.ProviderId
            ?? string.Empty;

        return Page();
    }
}
