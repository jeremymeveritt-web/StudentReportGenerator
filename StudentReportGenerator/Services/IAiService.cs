using StudentReportGenerator.Models;
using System.Net.Http;
using System.Threading.Tasks;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// The single contract every AI provider integration must implement (NVIDIA NIM, Google Gemini,
    /// OpenAI, Anthropic Claude). Keeping this interface intentionally minimal — one method — is what
    /// lets <see cref="AiServiceFactory"/> and <see cref="ReportOrchestratorService"/> treat all four
    /// providers interchangeably at runtime, and is the pattern <c>ISchoolDatabaseService</c> deliberately
    /// mirrors for SIS/MIS integrations.
    /// </summary>
    public interface IAiService
    {
        /// <summary>Sends the built prompt to the provider and returns the generated report,
        /// or a populated <see cref="ReportResponse.ErrorMessage"/> on failure. Never throws for
        /// expected failure modes (network errors, rate limits, bad API keys) — see <see cref="BaseAiService"/>.</summary>
        Task<ReportResponse> GenerateReportAsync(ReportRequest request);
    }
}