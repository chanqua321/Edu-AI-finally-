using EduAI.Model.Entities;
using Microsoft.AspNetCore.Identity;

namespace EduAI.Web.Middleware;

public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        if (context.User.Identity?.IsAuthenticated != true || IsExemptPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var user = await userManager.GetUserAsync(context.User);
        if (user is not { MustChangePassword: true })
        {
            await _next(context);
            return;
        }

        context.Response.Redirect("/Account/Profile?section=password&force=1");
    }

    private static bool IsExemptPath(PathString path)
    {
        if (path.StartsWithSegments("/Account/Profile", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Account/Logout", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Account/Login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Account/ConfirmEmail", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Account/ResendEmailConfirmation", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Account/AccessDenied", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Error", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
