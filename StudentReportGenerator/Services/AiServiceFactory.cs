using System.Net.Http;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Resolves the correct <see cref="IAiService"/> engine, decrypted API key, and configured model
    /// tier for a given provider name. Keeping this resolution behind an interface and constructing
    /// providers here — rather than scattering <c>new XxxReportService(...)</c> calls through
    /// <see cref="ReportOrchestratorService"/> — keeps provider construction inside the DI container
    /// and makes the orchestrator trivially testable with a mock factory.
    /// </summary>
    public interface IAiServiceFactory
    {
        /// <summary>Returns the AI engine to call for <paramref name="provider"/>, its decrypted API
        /// key (empty if none configured), and the model tier the teacher selected for that provider.</summary>
        (IAiService Service, string ApiKey, string ModelTier) Create(string provider);
    }

    public class AiServiceFactory : IAiServiceFactory
    {
        /// <summary>Name of the named <see cref="HttpClient"/> registered via <c>IHttpClientFactory</c>
        /// in App.xaml.cs, shared by all four AI providers so socket/DNS lifetime is managed centrally.</summary>
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
