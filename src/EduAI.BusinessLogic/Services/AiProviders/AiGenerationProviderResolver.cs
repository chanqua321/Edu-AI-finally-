using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using EduAI.Model.Settings;
using Microsoft.Extensions.Options;

namespace EduAI.BusinessLogic.Services.AiProviders;

public sealed class AiGenerationProviderResolver : IAiGenerationProviderResolver
{
    private readonly IReadOnlyDictionary<string, IAiGenerationProvider> _providers;
    // appsettings: "AIProviders" (inject qua IOptions) → Default (Gemini/Ollama) dùng khi không chọn provider.
    private readonly AiProvidersSettings _settings;

    public AiGenerationProviderResolver(
        IEnumerable<IAiGenerationProvider> providers,
        IOptions<AiProvidersSettings> settings)
    {
        _providers = providers.ToDictionary(p => p.ProviderId, StringComparer.OrdinalIgnoreCase);
        _settings = settings.Value;
    }

    public IAiGenerationProvider Resolve(string? providerId = null)
    {
        // appsettings: "AIProviders:Default" → provider mặc định khi client không truyền ProviderId.
        var id = AiProviderIds.Normalize(
            string.IsNullOrWhiteSpace(providerId) ? _settings.Default : providerId);

        if (_providers.TryGetValue(id, out var provider))
            return provider;

        if (_providers.TryGetValue(AiProviderIds.Gemini, out var gemini))
            return gemini;

        throw new InvalidOperationException($"Không tìm thấy AI generation provider '{id}'.");
    }

    public IReadOnlyList<IAiGenerationProvider> GetAll() =>
        _providers.Values.OrderBy(p => p.ProviderId).ToList();
}
