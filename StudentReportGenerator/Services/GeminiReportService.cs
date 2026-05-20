using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public class GeminiReportService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiReportService(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey;
        }

        public async Task<ReportResponse> GenerateReportAsync(ReportRequest request)
        {
            try
            {
                string activeModel = string.IsNullOrWhiteSpace(request.SelectedModel) ? "gemini-1.5-flash" : request.SelectedModel;
                string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{activeModel}:generateContent?key={_apiKey}";

                string prompt = BuildPrompt(request);

                var payload = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = prompt } } }
                    }
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, content);
                string responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using (JsonDocument doc = JsonDocument.Parse(responseString))
                    {
                        var root = doc.RootElement;
                        string generatedText = root.GetProperty("candidates")[0]
                                                   .GetProperty("content")
                                                   .GetProperty("parts")[0]
                                                   .GetProperty("text").GetString();

                        return new ReportResponse { IsSuccess = true, GeneratedReport = generatedText?.Trim() ?? string.Empty };
                    }
                }

                return new ReportResponse { IsSuccess = false, ErrorMessage = $"Gemini API Error: {response.StatusCode} - {responseString}" };
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