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
                string securePrompt = PromptBuilderService.BuildSecurePrompt(request);

                var payload = new
                {
                    model = activeModel,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a professional school teacher writing end-of-term update reports directly to parents about their child." },
                        new { role = "user", content = securePrompt }
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

                    return new ReportResponse { IsSuccess = false, ErrorMessage = $"OpenAI Error: {response.StatusCode}" };
                }
            }
            catch (Exception ex)
            {
                return new ReportResponse { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        private string BuildPrompt(ReportRequest request)
        {
            return $"Write an academic report card update TO the parents of the student. " +
                   $"CRITICAL PRONOUN RULE: Address the parent directly about their child. Never say 'You' to mean the student. Use phrasing like 'Your child, {request.StudentName}, has...', 'In class, {request.StudentName} demonstrates...'.\n\n" +
                   $"Student Name: {request.StudentName}\n" +
                   $"Subject: {request.Subject}\n" +
                   $"Target Grade: {request.TargetGrade}\n" +
                   $"Learning Support/SEN: {request.SupportNeeds}\n" +
                   $"Tone Template: {request.SelectedFramework}\n" +
                   $"Teacher Notes: {request.RawNotes}\n\n" +
                   $"Write a ~{request.WordCount} word update report. Sign off as '{request.TeacherSignoff}' from '{request.SchoolName}'.";
        }
    }
}