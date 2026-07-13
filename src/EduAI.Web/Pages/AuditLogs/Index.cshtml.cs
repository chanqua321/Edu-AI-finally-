using EduAI.Model.Constants;
using EduAI.Model.DTOs;
using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.AuditLogs;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IAuditLogService _auditLogService;

    public IndexModel(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public AuditLogIndexViewModel ViewModel { get; set; } = new();

    public IReadOnlyList<string> KnownActions { get; } =
    [
        AuditActions.Login,
        AuditActions.Logout,
        AuditActions.CreateAccount,
        AuditActions.DisableAccount,
        AuditActions.ActivateAccount,
        AuditActions.ResetPassword,
        AuditActions.CreateSubject,
        AuditActions.UpdateSubject,
        AuditActions.DeleteSubject,
        AuditActions.AssignTeacher,
        AuditActions.UploadDocument,
        AuditActions.DeleteDocument,
        AuditActions.DownloadDocument,
        AuditActions.CreateChatSession,
        AuditActions.UpdateChatSession,
        AuditActions.DeleteChatSession,
        AuditActions.AiRequest,
        AuditActions.AiResponseError,
        AuditActions.EmailSendFailed
    ];

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public AuditLogQueryDto Filter { get; set; } = new();

    public async Task OnGetAsync()
    {
        if (Filter.MaxResults <= 0)
            Filter.MaxResults = 200;

        ViewModel.Logs = await _auditLogService.SearchAsync(Filter);
    }
}
