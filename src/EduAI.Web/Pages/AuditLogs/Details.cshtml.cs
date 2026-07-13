using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.AuditLogs;

[Authorize(Policy = "AdminOnly")]
public class DetailsModel : PageModel
{
    private readonly IAuditLogService _auditLogService;

    public DetailsModel(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public AuditLogDetailsViewModel ViewModel { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var log = await _auditLogService.GetByIdAsync(id);
        if (log == null)
            return NotFound();

        ViewModel.Log = log;
        return Page();
    }
}
