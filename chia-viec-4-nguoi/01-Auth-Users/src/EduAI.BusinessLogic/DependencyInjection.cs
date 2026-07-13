using EduAI.BusinessLogic.Services;
using EduAI.BusinessLogic.IService;
using EduAI.Model.IRepository;
using EduAI.Model.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EduAI.BusinessLogic;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUnitOfWork, Model.UnitOfWork.UnitOfWork>();

        services.Configure<AppSettings>(configuration.GetSection(AppSettings.SectionName));
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.Configure<IndexingSettingsDefaults>(configuration.GetSection(IndexingSettingsDefaults.SectionName));

        services.AddOptions<GeminiSettings>()
            .Bind(configuration.GetSection(GeminiSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.ApiKey),
                "Gemini:ApiKey must be set in appsettings.json.")
            .ValidateOnStart();

        services.AddHttpClient<IGeminiAiService, GeminiAiService>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<GeminiSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });

        services.TryAddScoped<INotificationService, NullNotificationService>();
        services.TryAddScoped<ISubjectNotificationService, NullSubjectNotificationService>();
        services.TryAddScoped<IUserNotificationService, NullUserNotificationService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<IChapterService, ChapterService>();
        services.AddScoped<ILessonService, LessonService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDocumentIndexingService, DocumentIndexingService>();
        services.AddScoped<IIndexingSettingsService, IndexingSettingsService>();
        services.AddScoped<IChunkService, ChunkService>();
        services.AddScoped<IEmbeddingService, EmbeddingService>();
        services.AddScoped<IChatService, ChatService>();

        return services;
    }
}
