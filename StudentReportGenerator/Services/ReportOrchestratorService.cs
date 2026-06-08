using System;
using System.Net.Http;
using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    // This service acts as the traffic cop for all AI requests across the entire application.
    public class ReportOrchestratorService
    {
        private readonly HttpClient _httpClient;
        private readonly AppStateService _appState;

        public ReportOrchestratorService(HttpClient httpClient, AppStateService appState)
        {
            _httpClient = httpClient;
            _appState = appState;
        }

        public async Task<ReportResponse> GenerateAsync(ReportRequest request, string? providerOverride = null)
        {
            // Use the override if provided (for Compare Mode), otherwise use the global default
            string provider = providerOverride ?? _appState.CurrentSettings.AiProvider;
            string activeKey = string.Empty;
            IAiService aiEngine;

            // 1. Decrypt the correct key, inject the correct model tier, and spin up the engine
            if (provider.Contains("NVIDIA"))
            {
                activeKey = CryptoService.DecryptSecret(_appState.CurrentSettings.NvidiaApiKey);
                request.SelectedModel = _appState.CurrentSettings.NvidiaModelTier;
                aiEngine = new NvidiaReportService(_httpClient, activeKey);
            }
            else if (provider.Contains("OpenAI"))
            {
                activeKey = CryptoService.DecryptSecret(_appState.CurrentSettings.OpenAiApiKey);
                request.SelectedModel = _appState.CurrentSettings.OpenAiModelTier;
                aiEngine = new OpenAiReportService(_httpClient, activeKey);
            }
            else if (provider.Contains("Claude"))
            {
                activeKey = CryptoService.DecryptSecret(_appState.CurrentSettings.ClaudeApiKey);
                request.SelectedModel = _appState.CurrentSettings.ClaudeModelTier;
                aiEngine = new ClaudeReportService(_httpClient, activeKey);
            }
            else
            {
                activeKey = CryptoService.DecryptSecret(_appState.CurrentSettings.GeminiApiKey);
                request.SelectedModel = _appState.CurrentSettings.GeminiModelTier;
                aiEngine = new GeminiReportService(_httpClient, activeKey);
            }

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