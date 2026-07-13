using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EduAI.Web.Hubs;

[Authorize]
public class SubjectHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await JoinSubjectsFeed();
        await base.OnConnectedAsync();
    }

    public async Task JoinSubjectsFeed()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "subjects");
    }
}
