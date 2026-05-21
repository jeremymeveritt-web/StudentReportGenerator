using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public class NvidiaReportService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public NvidiaReportService(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey;
        }

        public async Task<ReportResponse> GenerateReportAsync(ReportRequest request)
        {
            try
            {
                string endpoint = "https://integrate.api.nvidia.com/v1/chat/completions";
                string activeModel = string.IsNullOrWhiteSpace(request.SelectedModel) ? "meta/llama-3.1-405b-instruct" : request.SelectedModel;
                string prompt = BuildPrompt(request);

                var payload = new
                {
                    model = activeModel,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 1024,
                    temperature = 0.5,
                    top_p = 1,
                    stream = false
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
                            string? generatedText = doc.RootElement.GetProperty("choices")[0]
                                                      .GetProperty("message")
                                                      .GetProperty("content").GetString();

                            return new ReportResponse { IsSuccess = true, GeneratedReport = generatedText?.Trim() ?? string.Empty };
                        }
                    }

                    return new ReportResponse { IsSuccess = false, ErrorMessage = $"NVIDIA Error: {response.StatusCode}" };
                }
            }
            catch (Exception ex)
            {
                return new ReportResponse { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        private string BuildPrompt(ReportRequest request)
        {
            return $"You are a teacher writing an academic update directly TO the parents of a student. " +
                   $"MANDATORY VIEWPOINT: Address the parent directly regarding their child. Do not address the student. Use wording such as 'Your child, {request.StudentName}, has made progress...', 'In class, {request.StudentName} is...'.\n\n" +
                   $"Student Name: {request.StudentName}\n" +
                   $"Subject: {request.Subject}\n" +
                   $"Target Grade: {request.TargetGrade}\n" +
                   $"Support Needs/SEN: {request.SupportNeeds}\n" +
                   $"Tone Template: {request.SelectedFramework}\n" +
                   $"Teacher Notes: {request.RawNotes}\n\n" +
                   $"Write a ~{request.WordCount} word update report. Sign off as '{request.TeacherSignoff}' from '{request.SchoolName}'.";
        }
    }
}