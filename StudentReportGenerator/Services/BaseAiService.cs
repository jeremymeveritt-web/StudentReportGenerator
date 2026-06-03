using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    // This abstract class implements IAiService and handles all the boilerplate!
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
            try
            {
                // 1. Ask the specific child class (OpenAI, Gemini, etc.) for its unique request format
                var httpRequest = BuildRequest(request);

                // 2. We handle the actual sending and waiting right here
                var response = await _httpClient.SendAsync(httpRequest);
                string responseBody = await response.Content.ReadAsStringAsync();

                // 3. Centralized error handling
                if (!response.IsSuccessStatusCode)
                {
                    return new ReportResponse { IsSuccess = false, ErrorMessage = $"API Error ({response.StatusCode}): {responseBody}" };
                }

                // 4. Ask the child class to pluck the text out of the JSON
                string generatedText = ParseResponse(responseBody);
                return new ReportResponse { IsSuccess = true, GeneratedReport = generatedText.Trim() };
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

        // Child classes MUST provide these two methods
        protected abstract HttpRequestMessage BuildRequest(ReportRequest request);
        protected abstract string ParseResponse(string responseBody);
    }
}