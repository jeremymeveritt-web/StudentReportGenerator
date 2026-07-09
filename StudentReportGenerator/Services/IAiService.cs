using StudentReportGenerator.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// The single contract every AI provider integration must implement (NVIDIA NIM, Google Gemini,
    /// OpenAI, Anthropic Claude). Keeping this interface intentionally minimal is what
    /// lets <see cref="AiServiceFactory"/> and <see cref="ReportOrchestratorService"/> treat all four
    /// providers interchangeably at runtime, and is the pattern <c>ISchoolDatabaseService</c> deliberately
    /// mirrors for SIS/MIS integrations.
    /// </summary>
    public interface IAiService
    {
        /// <summary>Sends the built prompt to the provider and returns the generated report,
        /// or a populated <see cref="ReportResponse.ErrorMessage"/> on failure. Never throws for
        /// expected failure modes (network errors, rate limits, bad API keys) — see <see cref="BaseAiService"/>.
        /// The one exception: user cancellation via <paramref name="ct"/> propagates as
        /// <see cref="OperationCanceledException"/> so callers can distinguish it from a timeout.</summary>
        Task<ReportResponse> GenerateReportAsync(ReportRequest request, CancellationToken ct = default);

        /// <summary>Streaming variant: <paramref name="onDelta"/> is invoked with each text fragment
        /// as the provider produces it, and the completed report is returned at the end. Providers
        /// that cannot stream, and any streaming failure, silently fall back to
        /// <see cref="GenerateReportAsync"/> — callers always get a final <see cref="ReportResponse"/>.</summary>
        Task<ReportResponse> GenerateReportStreamAsync(ReportRequest request, Action<string> onDelta, CancellationToken ct = default);
    }
}
