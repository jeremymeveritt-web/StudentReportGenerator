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
                string securePrompt = PromptBuilderService.BuildSecurePrompt(request);

                var payload = new
                {
                    model = activeModel,
                    max_tokens = 1000,
                    system = "You are a professional school teacher writing academic update reports directly to parents about their child. Do not write directly to the student.",
                    messages = new[]
                    {
                        new { role = "user", content = securePrompt }
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

                    return new ReportResponse { IsSuccess = false, ErrorMessage = $"Claude Error: {response.StatusCode}" };
                }
            }
            catch (Exception ex)
            {
                return new ReportResponse { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        private string BuildPrompt(ReportRequest request)
        {
            return $"Write an academic update report card directed exclusively TO the parents of the student. " +
                   $"PRONOUN REQUIREMENT: Speak to the parents about their child. Use terms like 'Your child, {request.StudentName}, has achieved...', '{request.StudentName} continues to excel in...'. Never address the student directly.\n\n" +
                   $"Student Name: {request.StudentName}\n" +
                   $"Subject: {request.Subject}\n" +
                   $"Target Grade: {request.TargetGrade}\n" +
                   $"Support Needs/SEN: {request.SupportNeeds}\n" +
                   $"Tone Template: {request.SelectedFramework}\n" +
                   $"Teacher Notes: {request.RawNotes}\n\n" +
                   $"Write a ~{request.WordCount} word update. Sign off as '{request.TeacherSignoff}' from '{request.SchoolName}'.";
        }
    }
}