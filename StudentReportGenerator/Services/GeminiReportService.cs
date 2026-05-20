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
        private readonly string _apiKey;

        public GeminiReportService(string apiKey)
        {
            _apiKey = apiKey;
        }

        public async Task<ReportResponse> GenerateReportAsync(ReportRequest request)
        {
            try
            {
                // Fallback to flash if nothing was selected
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

                using (var client = new HttpClient())
                {
                    string jsonPayload = JsonSerializer.Serialize(payload);
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(endpoint, content);
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

                            return new ReportResponse { IsSuccess = true, GeneratedReport = generatedText.Trim() };
                        }
                    }
                    else
                    {
                        return new ReportResponse { IsSuccess = false, ErrorMessage = $"Gemini API Error: {response.StatusCode} - {responseString}" };
                    }
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
                   $"Target/Expected Grade: {request.TargetGrade}\n" + // NEW
                   $"Learning Support Needs / EHCP: {request.SupportNeeds}\n" + // NEW
                   $"Framework: {request.SelectedFramework}\n" +
                   $"Notes: {request.RawNotes}\n\n" +
                   $"Write a ~{request.WordCount} word report. " +
                   $"Address their target grade and accommodate any mentioned learning needs in your tone/content. " +
                   $"Sign off as '{request.TeacherSignoff}' from '{request.SchoolName}'.";
        }
    }
}