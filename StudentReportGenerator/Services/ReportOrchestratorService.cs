using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    // This service acts as the traffic cop for all AI requests across the entire application.
    public class ReportOrchestratorService
    {
        private readonly IAiServiceFactory _aiServiceFactory;
        private readonly AppStateService _appState;

        public ReportOrchestratorService(IAiServiceFactory aiServiceFactory, AppStateService appState)
        {
            _aiServiceFactory = aiServiceFactory;
            _appState = appState;
        }

        public async Task<ReportResponse> GenerateAsync(ReportRequest request, string? providerOverride = null)
        {
            // Use the override if provided (for Compare Mode), otherwise use the global default
            string provider = providerOverride ?? _appState.CurrentSettings.AiProvider;

            // 1. Resolve the right engine, key, and model tier through the DI factory
            var (aiEngine, activeKey, modelTier) = _aiServiceFactory.Create(provider);
            request.SelectedModel = modelTier;

            if (string.IsNullOrWhiteSpace(activeKey))
            {
                return new ReportResponse { IsSuccess = false, ErrorMessage = "ERROR: Missing API Key inside configuration profiles. Please check Settings." };
            }

            // 2. Execute the network request
            var response = await aiEngine.GenerateReportAsync(request);

            // 3. Centralized Analytics Tracking!
            if (response.IsSuccess)
            {
                int words = response.GeneratedReport.Split(' ').Length;
                _appState.CurrentSettings.TotalTokensEstimated += (long)(words * 1.3);
                _appState.CurrentSettings.TotalReportsGenerated++;

                if (provider.Contains("NVIDIA")) _appState.CurrentSettings.NvidiaReportsCount++;
                else if (provider.Contains("OpenAI")) _appState.CurrentSettings.OpenAiReportsCount++;
                else if (provider.Contains("Claude")) _appState.CurrentSettings.ClaudeReportsCount++;
                else _appState.CurrentSettings.GeminiReportsCount++;

                _appState.SaveSettings();
            }

            return response;
        }
    }
}