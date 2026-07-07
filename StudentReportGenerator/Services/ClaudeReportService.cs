using System.Net.Http;
using System.Text;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>Anthropic Claude provider. Talks to the Messages API; only <see cref="BuildRequest"/>
    /// and <see cref="ParseResponse"/> are provider-specific — retry/timeout handling lives in <see cref="BaseAiService"/>.</summary>
    public class ClaudeReportService : BaseAiService
    {
        public ClaudeReportService(HttpClient httpClient, string apiKey) : base(httpClient, apiKey) { }

        protected override HttpRequestMessage BuildRequest(ReportRequest request)
        {
            var prompt = PromptBuilderService.BuildSecurePrompt(request);
            var payload = new
            {
                model = request.SelectedModel,
                max_tokens = Math.Max(1200, request.WordCount * 2),
                messages = new[] { new { role = "user", content = prompt } }
            };

            var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");

            // Anthropic strictly requires these two custom headers
            msg.Headers.Add("x-api-key", _apiKey);
            msg.Headers.Add("anthropic-version", "2023-06-01");
            msg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            return msg;
        }

        protected override string ParseResponse(string responseBody)
        {
            // Traverse Anthropic's response tree: content[0].text
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement
                      .GetProperty("content")[0]
                      .GetProperty("text")
                      .GetString() ?? string.Empty;
        }
    }
}