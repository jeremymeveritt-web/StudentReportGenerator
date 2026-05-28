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

                string securePrompt = PromptBuilderService.BuildSecurePrompt(request);

                var payload = new
                {
                    contents = new[]
                    {
                        new { parts = new[] { new { text = securePrompt } } }
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

                return new ReportResponse { IsSuccess = false, ErrorMessage = $"Gemini Error: {response.StatusCode}" };
            }
            catch (Exception ex)
            {
                return new ReportResponse { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        private string BuildPrompt(ReportRequest request)
        {
            return $"You are a school teacher writing an academic update report directly TO the parents of a student. " +
                   $"CRITICAL PERSPECTIVE RULE: Do not address the student. Address the parents directly about their child using formal, professional, yet warm pronouns (e.g., 'Your child, {request.StudentName}, has shown...', '{request.StudentName} has worked hard...').\n\n" +
                   $"Student Name: {request.StudentName}\n" +
                   $"Subject: {request.Subject}\n" +
                   $"Target Grade: {request.TargetGrade}\n" +
                   $"Learning Support / SEN: {request.SupportNeeds}\n" +
                   $"Tone Template: {request.SelectedFramework}\n" +
                   $"Teacher Notes: {request.RawNotes}\n\n" +
                   $"Write a ~{request.WordCount} word report update. Incorporate the target grade, accommodate mentioned support needs constructively, and sign off as '{request.TeacherSignoff}' from '{request.SchoolName}'.";
        }
    }
}