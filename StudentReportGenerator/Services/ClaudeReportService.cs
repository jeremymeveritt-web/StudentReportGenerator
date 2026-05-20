using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public class ClaudeReportService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public ClaudeReportService(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey;
        }

        public async Task<ReportResponse> GenerateReportAsync(ReportRequest request)
        {
            try
            {
                string endpoint = "https://api.anthropic.com/v1/messages";
                string activeModel = string.IsNullOrWhiteSpace(request.SelectedModel) ? "claude-3-haiku-20240307" : request.SelectedModel;
                string prompt = BuildPrompt(request);

                var payload = new
                {
                    model = activeModel,
                    max_tokens = 1000,
                    system = "You are a professional school teacher writing student reports.",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    }
                };

                using (var message = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    message.Headers.Add("x-api-key", _apiKey);
                    message.Headers.Add("anthropic-version", "2023-06-01");
                    message.Headers.Add("User-Agent", "StudentReportGenerator/1.0");

                    string jsonPayload = JsonSerializer.Serialize(payload);
                    message.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(message);
                    string responseString = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using (JsonDocument doc = JsonDocument.Parse(responseString))
                        {
                            string generatedText = doc.RootElement.GetProperty("content")[0]
                                                      .GetProperty("text").GetString();

                            return new ReportResponse { IsSuccess = true, GeneratedReport = generatedText?.Trim() ?? string.Empty };
                        }
                    }

                    return new ReportResponse { IsSuccess = false, ErrorMessage = $"Claude API Error: {response.StatusCode} - {responseString}" };
                }
            }
            catch (Exception ex)
            {
                return new ReportResponse { IsSuccess = false, ErrorMessage = $"Local Error: {ex.Message}" };
            }
        }

        private string BuildPrompt(ReportRequest request)
        {
            return $"You are a professional school teacher writing student reports.\n" +
                   $"Student Name: {request.StudentName}\n" +
                   $"Subject: {request.Subject}\n" +
                   $"Target/Expected Grade: {request.TargetGrade}\n" +
                   $"Learning Support Needs / EHCP: {request.SupportNeeds}\n" +
                   $"Framework: {request.SelectedFramework}\n" +
                   $"Notes: {request.RawNotes}\n\n" +
                   $"Write a ~{request.WordCount} word report. " +
                   $"Address their target grade and accommodate any mentioned learning needs in your tone/content. " +
                   $"Sign off as '{request.TeacherSignoff}' from '{request.SchoolName}'.";
        }
    }
}