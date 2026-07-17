using EduAI.BusinessLogic;
using EduAI.Model;
using EduAI.Model.Constants;
using EduAI.Model.Entities;
using EduAI.Web.Data;
using EduAI.BusinessLogic.IService;
using EduAI.Web.Helpers;
using EduAI.Web.Hubs;
using EduAI.Web.Middleware;
using EduAI.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Text.Json;
using DotNetEnv;

//Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

// Kestrel ceiling is intentionally high; business validation uses SystemSettings.MaxUploadFileSizeBytes.
const long uploadRequestCeilingBytes = 209_715_200; // 200 MB
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = uploadRequestCeilingBytes;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = uploadRequestCeilingBytes;
});

if (builder.Environment.IsDevelopment()
    && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("https://localhost:7014");
}

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Users", "AdminOnly");
    options.Conventions.AuthorizeFolder("/AuditLogs", "AdminOnly");
    options.Conventions.AuthorizeFolder("/ChatSessions", "AdminOnly");
    options.Conventions.AuthorizePage("/ChatMessages/Details", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Chunks", "AdminOrTeacher");
    options.Conventions.AuthorizePage("/Settings/System", "AdminOnly");
    options.Conventions.AuthorizePage("/Chunks/Create", "AdminOrTeacher");
    options.Conventions.AuthorizePage("/Chunks/Edit", "AdminOrTeacher");
    options.Conventions.AllowAnonymousToPage("/Payment/Ipn");
    options.Conventions.AuthorizeFolder("/Chat", "StudentOnly");
    options.Conventions.AuthorizeFolder("/Documents", "AdminOrTeacher");
    options.Conventions.AuthorizeFolder("/Reports", "AdminOrTeacher");
    // Upload page should be visible to teachers only
    options.Conventions.AuthorizePage("/Documents/Create", "TeacherOnly");
    options.Conventions.AuthorizePage("/Documents/Edit", "TeacherOnly");
    options.Conventions.AuthorizeFolder("/Study", "AuthenticatedUser");
    options.Conventions.AuthorizePage("/Study/Materials", "StudentOnly");
    options.Conventions.AuthorizeFolder("/Chapters", "AdminOrTeacher");
    options.Conventions.AuthorizePage("/Chapters/Create", "TeacherOnly");
    options.Conventions.AuthorizePage("/Chapters/Edit", "TeacherOnly");
    options.Conventions.AuthorizeFolder("/Subjects");
    options.Conventions.AuthorizePage("/Subjects/Create", "AdminOnly");
    options.Conventions.AuthorizePage("/Subjects/Edit", "AdminOnly");
    options.Conventions.AuthorizePage("/Account/Profile", "AuthenticatedUser");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Register");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/Account/ConfirmEmail");
    options.Conventions.AllowAnonymousToPage("/Account/ResendEmailConfirmation");
});
// gọi db contect và các service liên quan đến business logic
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Chưa cấu hình chuỗi kết nối 'DefaultConnection'.");

builder.Services.AddAppDbContext(connectionString);
builder.Services.AddBusinessLogic(builder.Configuration);
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
builder.Services.AddHttpContextAccessor();
builder.Services.Replace(ServiceDescriptor.Scoped<ISubjectNotificationService, SignalRSubjectNotificationService>());
builder.Services.Replace(ServiceDescriptor.Scoped<IUserNotificationService, SignalRUserNotificationService>());
builder.Services.Replace(ServiceDescriptor.Scoped<INotificationService, SignalRNotificationService>());

// Background document indexing: upload returns fast, indexing runs in background
builder.Services.AddSingleton<DocumentIndexingQueue>();
builder.Services.AddSingleton<IDocumentIndexingQueue>(sp => sp.GetRequiredService<DocumentIndexingQueue>());
builder.Services.AddHostedService<DocumentIndexingWorker>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddErrorDescriber<VietnameseIdentityErrorDescriber>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/hubs"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(Roles.Admin));
    options.AddPolicy("TeacherOnly", policy => policy.RequireRole(Roles.Teacher));
    options.AddPolicy("StudentOnly", policy => policy.RequireRole(Roles.Student));
    options.AddPolicy("AdminOrTeacher", policy => policy.RequireRole(Roles.Admin, Roles.Teacher));
    options.AddPolicy("AuthenticatedUser", policy => policy.RequireAuthenticatedUser());
});

if (int.TryParse(builder.Configuration["ASPNETCORE_HTTPS_PORT"], out var httpsPort))
{
    builder.Services.AddHttpsRedirection(options => options.HttpsPort = httpsPort);
}

var app = builder.Build();

await DataSeeder.SeedAsync(app.Services, app.Configuration);

if (args.Contains("--warmup-reports", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    var reports = scope.ServiceProvider.GetRequiredService<IReportService>();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var teacher = await users.FindByEmailAsync("teacher@gmail.com");
    var actorId = teacher?.Id ?? string.Empty;
    var (sessions, questions, error) = await reports.RunRealChatWarmupAsync(actorId, Roles.Teacher);
    Console.WriteLine(error == null
        ? $"[warmup-reports] OK: {sessions} sessions, {questions} real Gemini questions"
        : $"[warmup-reports] FAIL: {error}");
    return;
}

if (args.Contains("--demo-upload-chat", StringComparer.OrdinalIgnoreCase))
{
    await DemoUploadChatBootstrap.RunAsync(app.Services);
    return;
}

if (args.Contains("--benchmark-providers", StringComparer.OrdinalIgnoreCase))
{
    await ProviderBenchmarkBootstrap.RunAsync(app.Services);
    return;
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<InactiveUserMiddleware>();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();
app.MapRazorPages();
app.MapHub<SubjectHub>("/hubs/subjects");
app.MapHub<UserHub>("/hubs/user");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");

if (app.Environment.IsDevelopment())
{
    // appsettings: "AppSettings:AppBaseUrl" → tự mở browser khi chạy Development.
    var launchUrl = builder.Configuration.GetSection("AppSettings")["AppBaseUrl"] ?? "https://localhost:7014";
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(launchUrl)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // Best-effort browser launch for local development.
        }
    });
}

app.Run();
