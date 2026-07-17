using EduAI.Model;
using EduAI.Model.Constants;
using EduAI.Model.Entities;
using EduAI.BusinessLogic.IService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using EduAI.Model.Settings;

namespace EduAI.Web.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        // appsettings: "AppSettings:UploadPath" → xoá thư mục upload khi Database:ResetOnStartup = true.
        var appSettings = scope.ServiceProvider.GetRequiredService<IOptions<AppSettings>>().Value;
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var systemSettingsService = scope.ServiceProvider.GetRequiredService<ISystemSettingsService>();

        // appsettings: "Database:ResetOnStartup" → true thì xoá DB + migrate lại (chỉ dùng dev).
        if (configuration.GetValue<bool>("Database:ResetOnStartup"))
            await ResetDatabaseAsync(context, appSettings);
        else
            await context.Database.MigrateAsync();

        await SeedRolesAsync(roleManager);
        await SeedPaymentPackagesAsync(context);
        await SeedSystemSettingsAsync(systemSettingsService, configuration);
        await SeedUsersAsync(context, userManager, paymentService);
    }

    public static async Task ResetDatabaseAsync(AppDbContext context, AppSettings appSettings)
    {
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
        ClearUploadFolder(appSettings);
    }

    private static void ClearUploadFolder(AppSettings appSettings)
    {
        var uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), appSettings.UploadPath);
        if (!Directory.Exists(uploadRoot))
            return;

        foreach (var entry in Directory.EnumerateFileSystemEntries(uploadRoot))
        {
            if (Directory.Exists(entry))
                Directory.Delete(entry, recursive: true);
            else
                File.Delete(entry);
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { Roles.Admin, Roles.Teacher, Roles.Student })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private static async Task SeedPaymentPackagesAsync(AppDbContext context)
    {
        if (await context.PaymentPackages.AnyAsync())
            return;

        context.PaymentPackages.AddRange(
            new PaymentPackage
            {
                Id = PaymentPackageIds.Free,
                Name = "Gói Miễn Phí",
                Price = 0,
                Description = "Gói cước cơ bản dùng thử hệ thống. Phù hợp cho học sinh mới bắt đầu.",
                MaxDailyQuestions = 5,
                MonthlyGeminiQuestions = 0,
                DailyOllamaQuestions = 1,
                DurationDays = 99999,
                DisplayOrder = 1,
                IsRecommended = false,
                IsActive = true
            },
            new PaymentPackage
            {
                Id = PaymentPackageIds.Premium,
                Name = "Gói Premium",
                Price = 100_000,
                Description = "Gói cước nâng cao với Gemini theo tháng và Ollama theo ngày.",
                MaxDailyQuestions = 100,
                MonthlyGeminiQuestions = 40,
                DailyOllamaQuestions = 5,
                DurationDays = 30,
                DisplayOrder = 2,
                IsRecommended = true,
                IsActive = true
            },
            new PaymentPackage
            {
                Id = PaymentPackageIds.Enterprise,
                Name = "Gói Enterprise",
                Price = 500_000,
                Description = "Gói cước học tập chuyên sâu với quota Gemini lớn và Ollama hàng ngày.",
                MaxDailyQuestions = 0,
                MonthlyGeminiQuestions = 150,
                DailyOllamaQuestions = 20,
                DurationDays = 30,
                DisplayOrder = 3,
                IsRecommended = false,
                IsActive = true
            });

        await context.SaveChangesAsync();
    }

    // appsettings: "Gemini:EmbeddingModel" + "Gemini:ChatModel" → seed model mặc định vào bảng SystemSettings.
    private static async Task SeedSystemSettingsAsync(ISystemSettingsService systemSettingsService, IConfiguration configuration)
    {
        var geminiSection = configuration.GetSection("Gemini");
        await systemSettingsService.SeedDefaultAsync(
            geminiSection["EmbeddingModel"],
            geminiSection["ChatModel"]);
    }

    private static async Task SeedUsersAsync(AppDbContext context, UserManager<ApplicationUser> userManager, IPaymentService paymentService)
    {
        foreach (var seedUser in AppUserSeed.GetUsers())
        {
            var user = await userManager.FindByEmailAsync(seedUser.Email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    Email = seedUser.Email,
                    UserName = seedUser.Email,
                    NormalizedEmail = seedUser.Email.ToUpperInvariant(),
                    NormalizedUserName = seedUser.Email.ToUpperInvariant(),
                    PasswordHash = AppUserSeed.DefaultPasswordHash,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N"),
                    FullName = seedUser.FullName,
                    EmailConfirmed = true,
                    IsActive = true,
                    MustChangePassword = false,
                    CreatedAt = seedUser.CreatedAt
                };

                context.Users.Add(user);
                await context.SaveChangesAsync();
            }
            else
            {
                // Existing seed users: keep password / MustChangePassword as-is.
                user.FullName = seedUser.FullName;
                user.Email = seedUser.Email;
                user.UserName = seedUser.Email;
                user.NormalizedEmail = seedUser.Email.ToUpperInvariant();
                user.NormalizedUserName = seedUser.Email.ToUpperInvariant();
                user.EmailConfirmed = true;
                user.IsActive = true;

                await userManager.UpdateAsync(user);
            }

            if (!await userManager.IsInRoleAsync(user, seedUser.Role))
                await userManager.AddToRoleAsync(user, seedUser.Role);

            if (seedUser.Role == Roles.Student)
            {
                var activeSub = await paymentService.GetActiveSubscriptionAsync(user.Id);
                if (activeSub == null)
                {
                    var txn = await paymentService.CreateTransactionAsync(user.Id, PaymentPackageIds.Free, 0);
                    await paymentService.CompleteTransactionAsync(txn.Id, "FREE_SEEDED", "Success");
                }
            }
        }
    }
}
