using System.Net.Http;
using System.Text;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public class GeminiReportService : BaseAiService
    {
        public GeminiReportService(HttpClient httpClient, string apiKey) : base(httpClient, apiKey) { }

        protected override HttpRequestMessage BuildRequest(ReportRequest request)
        {
            var prompt = PromptBuilderService.BuildSecurePrompt(request);

            // Gemini has a highly nested JSON requirement
            var payload = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    max_tokens = Math.Max(1200, request.WordCount * 2),
                    temperature = 0.7
                }
            };

            // Gemini URL format requires the model name directly inside the URL path
            var msg = new HttpRequestMessage(HttpMethod.Post, $"https://generativelanguage.googleapis.com/v1beta/models/{request.SelectedModel}:generateContent");

            // Gemini uses a custom Google header instead of standard Bearer auth
            msg.Headers.Add("x-goog-api-key", _apiKey);
            msg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            return msg;
        }

        protected override string ParseResponse(string responseBody)
        {
            // Traverse Gemini's unique response tree: candidates[0].content.parts[0].text
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement
                      .GetProperty("candidates")[0]
                      .GetProperty("content")
                      .GetProperty("parts")[0]
                      .GetProperty("text")
                      .GetString() ?? string.Empty;
        }
    }
}