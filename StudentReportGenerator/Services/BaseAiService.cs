using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public abstract class BaseAiService : IAiService
    {
        protected readonly HttpClient _httpClient;
        protected readonly string _apiKey;

        protected BaseAiService(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey;
        }

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

                    // Exponential Backoff for Rate Limits (HTTP 429)
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

        protected abstract HttpRequestMessage BuildRequest(ReportRequest request);
        protected abstract string ParseResponse(string responseBody);
    }
}