using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>OpenAI provider (GPT-4o family). Uses the standard Chat Completions API shape,
    /// which NVIDIA NIM's OpenAI-compatible endpoint also follows almost verbatim — see <see cref="NvidiaReportService"/>.</summary>
    public class OpenAiReportService : BaseAiService
    {
        public OpenAiReportService(HttpClient httpClient, string apiKey) : base(httpClient, apiKey) { }

        protected override HttpRequestMessage BuildRequest(ReportRequest request, bool streaming = false)
        {
            var parts = PromptBuilderService.BuildPromptParts(request);
            var payload = new
            {
                model = request.SelectedModel,
                messages = new[]
                {
                    new { role = "system", content = parts.SystemInstructions },
                    new { role = "user", content = parts.UserContent }
                },
                max_tokens = Math.Max(1200, request.WordCount * 2),
                temperature = request.Temperature ?? 0.7,
                stream = streaming
            };

            var msg = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            msg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            return msg;
        }

        protected override string ParseResponse(string responseBody)
        {
            // Chat Completions response shape: choices[0].message.content
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }

        protected override string? ParseStreamEvent(string eventJson) => ParseChatCompletionsDelta(eventJson);

        /// <summary>Chat Completions SSE delta: choices[0].delta.content. Role-only and finish
        /// chunks have no content property, so every step is TryGetProperty-guarded. Shared with
        /// <see cref="NvidiaReportService"/>, whose endpoint is OpenAI-compatible.</summary>
        internal static string? ParseChatCompletionsDelta(string eventJson)
        {
            using var doc = JsonDocument.Parse(eventJson);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0) return null;
            if (choices[0].TryGetProperty("delta", out var delta) &&
                delta.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.String)
                return content.GetString();
            return null;
        }
    }
}
