using System.Net.Http;
using System.Text;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>Anthropic Claude provider. Talks to the Messages API; only the request building and
    /// response/stream parsing are provider-specific — retry/timeout/streaming plumbing lives in <see cref="BaseAiService"/>.</summary>
    public class ClaudeReportService : BaseAiService
    {
        public ClaudeReportService(HttpClient httpClient, string apiKey) : base(httpClient, apiKey) { }

        protected override HttpRequestMessage BuildRequest(ReportRequest request, bool streaming = false)
        {
            var parts = PromptBuilderService.BuildPromptParts(request);
            var payload = new
            {
                model = request.SelectedModel,
                max_tokens = Math.Max(1200, request.WordCount * 2),
                temperature = request.Temperature ?? 0.7,
                system = parts.SystemInstructions,
                messages = new[] { new { role = "user", content = parts.UserContent } },
                stream = streaming
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

        protected override string? ParseStreamEvent(string eventJson)
        {
            // Text arrives only in content_block_delta events (delta.text); everything else
            // (message_start, ping, message_delta, content_block_stop) carries no report text.
            using var doc = JsonDocument.Parse(eventJson);
            if (!doc.RootElement.TryGetProperty("type", out var type) ||
                type.GetString() != "content_block_delta") return null;
            if (doc.RootElement.TryGetProperty("delta", out var delta) &&
                delta.TryGetProperty("text", out var text))
                return text.GetString();
            return null;
        }
    }
}
