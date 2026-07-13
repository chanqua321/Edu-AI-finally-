using EduAI.BusinessLogic.IService;
using EduAI.Model.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Users;

[Authorize(Policy = "AdminOnly")]
public class IndexModel : PageModel
{
    private readonly IUserManagementService _userService;

    public IndexModel(IUserManagementService userService)
    {
        _userService = userService;
    }

    public UserIndexViewModel ViewModel { get; set; } = new();

    public async Task OnGetAsync()
    {
        ViewModel.Users = await _userService.GetAllUsersAsync();
    }
}
