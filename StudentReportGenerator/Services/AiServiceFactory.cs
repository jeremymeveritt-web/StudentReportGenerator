using System.Net.Http;

namespace StudentReportGenerator.Services
{
    // Resolves the correct AI engine for a provider name, keeping construction inside the DI container
    // instead of scattering 'new XxxReportService(...)' calls through the orchestrator.
    public interface IAiServiceFactory
    {
        (IAiService Service, string ApiKey, string ModelTier) Create(string provider);
    }

    public class AiServiceFactory : IAiServiceFactory
    {
        public const string HttpClientName = "AiProvider";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppStateService _appState;

        public AiServiceFactory(IHttpClientFactory httpClientFactory, AppStateService appState)
        {
            _httpClientFactory = httpClientFactory;
            _appState = appState;
        }

        public (IAiService Service, string ApiKey, string ModelTier) Create(string provider)
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var settings = _appState.CurrentSettings;

            if (provider.Contains("NVIDIA"))
            {
                string key = CryptoService.DecryptSecret(settings.NvidiaApiKey);
                return (new NvidiaReportService(client, key), key, settings.NvidiaModelTier);
            }
            if (provider.Contains("OpenAI"))
            {
                string key = CryptoService.DecryptSecret(settings.OpenAiApiKey);
                return (new OpenAiReportService(client, key), key, settings.OpenAiModelTier);
            }
            if (provider.Contains("Claude"))
            {
                string key = CryptoService.DecryptSecret(settings.ClaudeApiKey);
                return (new ClaudeReportService(client, key), key, settings.ClaudeModelTier);
            }

            string geminiKey = CryptoService.DecryptSecret(settings.GeminiApiKey);
            return (new GeminiReportService(client, geminiKey), geminiKey, settings.GeminiModelTier);
        }
    }
}
