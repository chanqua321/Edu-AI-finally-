using EduAI.Model.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EduAI.Web.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public const string AdminGroup = "role-admin";
    public const string TeacherGroup = "role-teacher";
    public const string StudentGroup = "role-student";

    public override async Task OnConnectedAsync()
    {
        await JoinAppFeed();
        await base.OnConnectedAsync();
    }

    public async Task JoinAppFeed()
    {
        if (Context.User?.IsInRole(Roles.Admin) == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);

        if (Context.User?.IsInRole(Roles.Teacher) == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, TeacherGroup);

        if (Context.User?.IsInRole(Roles.Student) == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, StudentGroup);
    }
}
