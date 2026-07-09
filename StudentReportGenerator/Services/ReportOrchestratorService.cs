using System;
using System.Threading;
using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Single entry point for every AI-generated report in the app: single reports, whole-class
    /// batches, side-by-side provider comparison, tone previews, and utility calls (simplify/
    /// translate/tone-audit) all funnel through <see cref="GenerateAsync"/>. Resolves the correct
    /// provider via <see cref="IAiServiceFactory"/>, executes the call, and centrally tracks usage
    /// analytics (token estimates, per-provider report counts) so that bookkeeping lives in exactly
    /// one place regardless of which UI flow triggered the generation.
    /// </summary>
    public class ReportOrchestratorService
    {
        private readonly IAiServiceFactory _aiServiceFactory;
        private readonly AppStateService _appState;

        public ReportOrchestratorService(IAiServiceFactory aiServiceFactory, AppStateService appState)
        {
            _aiServiceFactory = aiServiceFactory;
            _appState = appState;
        }

        /// <param name="request">The fully-populated report request (student data, notes, framework).</param>
        /// <param name="providerOverride">When set (used by Compare Mode to query two providers side by
        /// side), generates against this provider instead of the teacher's configured default.</param>
        /// <param name="onDelta">When set, the report is streamed: this callback receives each text
        /// fragment as it generates (single-report path). Null keeps the classic all-at-once call.</param>
        /// <param name="ct">Cancels the in-flight HTTP call; user cancellation propagates as
        /// <see cref="OperationCanceledException"/> per the <see cref="BaseAiService"/> contract.</param>
        public async Task<ReportResponse> GenerateAsync(ReportRequest request, string? providerOverride = null,
            Action<string>? onDelta = null, CancellationToken ct = default)
        {
            string provider = providerOverride ?? _appState.CurrentSettings.AiProvider;

            // 1. Resolve the right engine, decrypted key, and model tier through the DI factory.
            var (aiEngine, activeKey, modelTier) = _aiServiceFactory.Create(provider);
            request.SelectedModel = modelTier;

            if (string.IsNullOrWhiteSpace(activeKey))
            {
                return new ReportResponse { IsSuccess = false, ErrorMessage = "ERROR: Missing API Key inside configuration profiles. Please check Settings." };
            }

            // 2. Execute the network request (retry/backoff/stream-fallback handled inside BaseAiService).
            var response = onDelta != null
                ? await aiEngine.GenerateReportStreamAsync(request, onDelta, ct)
                : await aiEngine.GenerateReportAsync(request, ct);

            // 3. Centralised analytics: every successful generation, regardless of which screen
            //    triggered it, updates the same running totals shown on the Usage Statistics tab.
            if (response.IsSuccess)
            {
                int words = response.GeneratedReport.Split(' ').Length;
                _appState.CurrentSettings.TotalTokensEstimated += (long)(words * 1.3); // rough words-to-tokens estimate
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