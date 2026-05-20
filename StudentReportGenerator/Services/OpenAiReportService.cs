using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public class OpenAiReportService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public OpenAiReportService(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey;
        }

        public async Task<ReportResponse> GenerateReportAsync(ReportRequest request)
        {
            try
            {
                string endpoint = "https://api.openai.com/v1/chat/completions";
                string activeModel = string.IsNullOrWhiteSpace(request.SelectedModel) ? "gpt-4o-mini" : request.SelectedModel;
                string prompt = BuildPrompt(request);

                var payload = new
                {
                    model = activeModel,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a professional school teacher writing student reports." },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.7
                };

                using (var message = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    string jsonPayload = JsonSerializer.Serialize(payload);
                    message.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(message);
                    string responseString = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        using (JsonDocument doc = JsonDocument.Parse(responseString))
                        {
                            string generatedText = doc.RootElement.GetProperty("choices")[0]
                                                      .GetProperty("message")
                                                      .GetProperty("content").GetString();

                            return new ReportResponse { IsSuccess = true, GeneratedReport = generatedText?.Trim() ?? string.Empty };
                        }
                    }

                    return new ReportResponse { IsSuccess = false, ErrorMessage = $"OpenAI API Error: {response.StatusCode} - {responseString}" };
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