using EduAI.BusinessLogic.Services;
using EduAI.BusinessLogic.Services.AiProviders;
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
        services.AddMemoryCache();
        services.AddScoped<IUnitOfWork, Model.UnitOfWork.UnitOfWork>();

        // Bind appsettings → IOptions<T> để inject vào service (xem comment từng dòng bên dưới).
        services.Configure<AppSettings>(configuration.GetSection(AppSettings.SectionName));       // AppSettings: UploadPath, AppBaseUrl
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));   // EmailSettings: SMTP gửi mail
        services.Configure<AiRuntimeSettings>(configuration.GetSection(AiRuntimeSettings.SectionName)); // AiRuntime: Temperature, UsdToVndRate
        services.Configure<AiProvidersSettings>(configuration.GetSection(AiProvidersSettings.SectionName)); // AIProviders: Default, Ollama

        // appsettings: "Gemini:ApiKey" → bắt buộc có khi start; dùng xác thực khi gọi Gemini REST API.
        services.AddOptions<GeminiSettings>()
            .Bind(configuration.GetSection(GeminiSettings.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                settings => !string.IsNullOrWhiteSpace(settings.ApiKey),
                "Gemini:ApiKey must be set in appsettings.json.")
            .ValidateOnStart();

        // appsettings: "Gemini:BaseUrl" + "Gemini:TimeoutSeconds" → cấu hình HttpClient gọi Gemini API.
        services.AddHttpClient<IGeminiAiService, GeminiAiService>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<GeminiSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });

        // Generation providers (Strategy)
        services.AddScoped<IAiGenerationProvider, GeminiGenerationProvider>();
        // appsettings: "AIProviders:Ollama:BaseUrl" → cấu hình HttpClient gọi Ollama local (/api/chat).
        services.AddHttpClient<OllamaGenerationProvider>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<AiProvidersSettings>>().Value.Ollama;
            var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
                ? "http://localhost:11434"
                : settings.BaseUrl.TrimEnd('/') + "/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds <= 0 ? 300 : settings.TimeoutSeconds);
        });
        services.AddScoped<IAiGenerationProvider>(sp => sp.GetRequiredService<OllamaGenerationProvider>());
        services.AddScoped<IAiGenerationProviderResolver, AiGenerationProviderResolver>();

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
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddScoped<IChunkService, ChunkService>();
        services.AddScoped<IEmbeddingService, EmbeddingService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IVnPayService, VnPayService>();
        services.AddScoped<IPaymentService, PaymentService>();

        return services;
    }
}
