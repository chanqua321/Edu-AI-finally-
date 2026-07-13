using EduAI.Model.Constants;

namespace EduAI.Web.Data;

public sealed class AppUser
{
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public static class AppUserSeed
{
    public static readonly DateTime SeedCreatedAt = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Password: 12345 (ASP.NET Core Identity V3 hash)
    public const string DefaultPasswordHash =
        "AQAAAAIAAYagAAAAEGFbJU5Sqo+9zc1fJAmZe5yIVUQHHctxXqJaDJ+mXwndmSqbaFaHatqMagKZyNscBg==";

    public static IReadOnlyList<AppUser> GetUsers() =>
    [
        new AppUser
        {
            Email = "admin@gmail.com",
            Role = Roles.Admin,
            FullName = "System Administrator",
            CreatedAt = SeedCreatedAt
        },
        new AppUser
        {
            Email = "student@gmail.com",
            Role = Roles.Student,
            FullName = "Demo Student",
            CreatedAt = SeedCreatedAt
        },
        new AppUser
        {
            Email = "teacher@gmail.com",
            Role = Roles.Teacher,
            FullName = "Demo Teacher",
            CreatedAt = SeedCreatedAt
        }
    ];
}
