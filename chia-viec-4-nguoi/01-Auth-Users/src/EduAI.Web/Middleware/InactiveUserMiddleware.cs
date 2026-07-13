using EduAI.Model.Entities;
using Microsoft.AspNetCore.Identity;

namespace EduAI.Web.Middleware;

public class InactiveUserMiddleware
{
    private readonly RequestDelegate _next;

    public InactiveUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user is { IsActive: false })
            {
                await signInManager.SignOutAsync();
                context.Response.Redirect("/Account/Login?locked=1");
                return;
            }
        }

        await _next(context);
    }
}
