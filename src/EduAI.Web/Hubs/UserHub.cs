using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EduAI.Web.Hubs;

[Authorize]
public class UserHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await JoinUserFeed();
        await base.OnConnectedAsync();
    }

    public async Task JoinUserFeed()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, GetUserGroup(userId));
    }

    public static string GetUserGroup(string userId) => $"user-{userId}";
}
