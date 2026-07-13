namespace EduAI.Web.Helpers;

public static class IpAddressHelper
{
    public static string? GetClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString();
}
