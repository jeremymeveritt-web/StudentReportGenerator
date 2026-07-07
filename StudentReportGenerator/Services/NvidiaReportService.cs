using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>NVIDIA NIM provider — the app's free-tier default. Exposes an OpenAI-compatible Chat
    /// Completions endpoint, so the request/response shape here is intentionally identical to <see cref="OpenAiReportService"/>.</summary>
    public class NvidiaReportService : BaseAiService
    {
        public NvidiaReportService(HttpClient httpClient, string apiKey) : base(httpClient, apiKey) { }

        protected override HttpRequestMessage BuildRequest(ReportRequest request)
        {
            var prompt = PromptBuilderService.BuildSecurePrompt(request);
            var payload = new
            {
                model = request.SelectedModel,
                messages = new[] { new { role = "user", content = prompt } },
                max_tokens = Math.Max(1200, request.WordCount * 2),
                temperature = 0.7
            };

            var msg = new HttpRequestMessage(HttpMethod.Post, "https://integrate.api.nvidia.com/v1/chat/completions");
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            msg.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            return msg;
        }

        protected override string ParseResponse(string responseBody)
        {
            // OpenAI-compatible response shape: choices[0].message.content
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }
    }
}