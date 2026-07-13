using EduAI.Model;
using EduAI.Model.Constants;
using EduAI.Model.Entities;
using EduAI.Model.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EduAI.Web.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var appSettings = scope.ServiceProvider.GetRequiredService<IOptions<AppSettings>>().Value;

        if (configuration.GetValue<bool>("Database:ResetOnStartup"))
            await ResetDatabaseAsync(context, appSettings);
        else
            await context.Database.MigrateAsync();

        await SeedRolesAsync(roleManager);
        await SeedUsersAsync(context, userManager);
        await SeedIndexingSettingsAsync(context, configuration);
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

    private static async Task SeedUsersAsync(AppDbContext context, UserManager<ApplicationUser> userManager)
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
                user.FullName = seedUser.FullName;
                user.Email = seedUser.Email;
                user.UserName = seedUser.Email;
                user.NormalizedEmail = seedUser.Email.ToUpperInvariant();
                user.NormalizedUserName = seedUser.Email.ToUpperInvariant();
                user.PasswordHash = AppUserSeed.DefaultPasswordHash;
                user.EmailConfirmed = true;
                user.IsActive = true;
                user.MustChangePassword = false;
                user.CreatedAt = seedUser.CreatedAt;

                await userManager.UpdateAsync(user);
            }

            if (!await userManager.IsInRoleAsync(user, seedUser.Role))
                await userManager.AddToRoleAsync(user, seedUser.Role);
        }
    }

    private static async Task SeedIndexingSettingsAsync(AppDbContext context, IConfiguration configuration)
    {
        if (await context.IndexingSettings.AnyAsync())
            return;

        var defaults = configuration.GetSection(IndexingSettingsDefaults.SectionName).Get<IndexingSettingsDefaults>()
            ?? new IndexingSettingsDefaults();

        context.IndexingSettings.Add(new IndexingSettings
        {
            ChunkSize = defaults.ChunkSize,
            ChunkOverlap = defaults.ChunkOverlap,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
    }
}
