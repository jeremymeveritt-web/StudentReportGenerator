using System.Net.Http;
using System.Text;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>Google Gemini provider. Has the most divergent request/response shape of the four
    /// providers (nested "contents/parts" body, model name embedded in the URL, custom auth header),
    /// which is exactly the kind of variance <see cref="IAiService"/> is designed to hide from callers.</summary>
    public class GeminiReportService : BaseAiService
    {
        public GeminiReportService(HttpClient httpClient, string apiKey) : base(httpClient, apiKey) { }

        protected override HttpRequestMessage BuildRequest(ReportRequest request, bool streaming = false)
        {
            var parts = PromptBuilderService.BuildPromptParts(request);

            // Gemini has a highly nested JSON requirement; the system prompt goes in its
            // dedicated systemInstruction field rather than a message role.
            var payload = new
            {
                systemInstruction = new { parts = new[] { new { text = parts.SystemInstructions } } },
                contents = new[]
                {
                    new { parts = new[] { new { text = parts.UserContent } } }
                },
                generationConfig = new
                {
                    maxOutputTokens = Math.Max(1200, request.WordCount * 2),
                    temperature = request.Temperature ?? 0.7
                }
            };

            // Gemini URL format requires the model name directly inside the URL path; streaming
            // uses a different method plus ?alt=sse to get standard SSE framing.
            string method = streaming ? "streamGenerateContent?alt=sse" : "generateContent";
            var msg = new HttpRequestMessage(HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{request.SelectedModel}:{method}");

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

        protected override string? ParseStreamEvent(string eventJson)
        {
            // Each SSE chunk is a full response fragment with the same candidates/parts shape,
            // TryGetProperty-guarded because finish chunks may omit parts entirely.
            using var doc = JsonDocument.Parse(eventJson);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0) return null;
            if (candidates[0].TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array && parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var text) &&
                text.ValueKind == JsonValueKind.String)
                return text.GetString();
            return null;
        }
    }
}
