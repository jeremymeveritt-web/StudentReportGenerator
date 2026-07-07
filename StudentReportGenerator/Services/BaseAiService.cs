using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Shared HTTP plumbing for every <see cref="IAiService"/> provider implementation: retry-with-
    /// backoff on rate limiting, uniform error handling, and a template-method split between building
    /// the provider-specific HTTP request (<see cref="BuildRequest"/>) and parsing its response
    /// (<see cref="ParseResponse"/>). Concrete providers (Claude/Gemini/OpenAI/NVIDIA) only need to
    /// implement those two methods — everything else (timeouts, 429 backoff, exception safety) is
    /// handled once, here.
    /// </summary>
    public abstract class BaseAiService : IAiService
    {
        protected readonly HttpClient _httpClient;
        protected readonly string _apiKey;

        protected BaseAiService(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey;
        }

        /// <summary>
        /// Sends the request with automatic retry on HTTP 429 (rate limited), using exponential
        /// backoff starting at 2 seconds and doubling each attempt, up to 3 retries. All failure
        /// modes (timeout, non-2xx response, network/parsing exception) are converted into a
        /// <see cref="ReportResponse"/> with <see cref="ReportResponse.IsSuccess"/> = false rather
        /// than thrown, so callers never need a try/catch around this call.
        /// </summary>
        public async Task<ReportResponse> GenerateReportAsync(ReportRequest request)
        {
            int maxRetries = 3;
            int delayMs = 2000;

            for (int i = 0; i <= maxRetries; i++)
            {
                try
                {
                    var httpRequest = BuildRequest(request);
                    var response = await _httpClient.SendAsync(httpRequest);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        string generatedText = ParseResponse(responseBody);
                        return new ReportResponse { IsSuccess = true, GeneratedReport = generatedText.Trim() };
                    }

                    // Exponential backoff for rate limiting (HTTP 429): 2s, 4s, 8s before giving up.
                    if (response.StatusCode == (System.Net.HttpStatusCode)429 && i < maxRetries)
                    {
                        await Task.Delay(delayMs);
                        delayMs *= 2;
                        continue;
                    }

                    string errBody = await response.Content.ReadAsStringAsync();
                    return new ReportResponse { IsSuccess = false, ErrorMessage = $"API Error ({response.StatusCode}): {errBody}" };
                }
                catch (TaskCanceledException)
                {
                    return new ReportResponse { IsSuccess = false, ErrorMessage = "CONNECTION TIMEOUT: The downstream system connection window expired." };
                }
                catch (Exception ex)
                {
                    return new ReportResponse { IsSuccess = false, ErrorMessage = $"Network/Parsing Error: {ex.Message}" };
                }
            }

            return new ReportResponse { IsSuccess = false, ErrorMessage = "API Error: Maximum retry attempts exceeded." };
        }

        /// <summary>Builds the provider-specific HTTP request (endpoint, headers, JSON payload) from
        /// the generic <see cref="ReportRequest"/>, using <see cref="PromptBuilderService"/> to assemble the prompt text.</summary>
        protected abstract HttpRequestMessage BuildRequest(ReportRequest request);

        /// <summary>Extracts the generated report text from the provider's raw JSON response body.</summary>
        protected abstract string ParseResponse(string responseBody);
    }
}