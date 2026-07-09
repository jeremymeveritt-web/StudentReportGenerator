using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Shared HTTP plumbing for every <see cref="IAiService"/> provider implementation: retry-with-
    /// backoff on rate limiting, uniform error handling, SSE streaming with automatic non-streaming
    /// fallback, and a template-method split between building the provider-specific HTTP request
    /// (<see cref="BuildRequest"/>) and parsing its response (<see cref="ParseResponse"/> /
    /// <see cref="ParseStreamEvent"/>). Concrete providers (Claude/Gemini/OpenAI/NVIDIA) only need
    /// those methods — timeouts, 429 backoff, cancellation and exception safety are handled once, here.
    /// </summary>
    /// <remarks>
    /// Cancellation contract, shared by both generate methods: user cancellation (the caller's token
    /// fired) propagates as <see cref="OperationCanceledException"/>; an HTTP timeout (which also
    /// surfaces as <see cref="TaskCanceledException"/>, but without the token being cancelled) is
    /// converted to a failed <see cref="ReportResponse"/> with <see cref="ReportResponse.IsTimeout"/>
    /// set, so callers can queue it for automatic retry without ever confusing the two.
    /// </remarks>
    public abstract class BaseAiService : IAiService
    {
        protected readonly HttpClient _httpClient;
        protected readonly string _apiKey;

        protected BaseAiService(HttpClient httpClient, string apiKey)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _apiKey = apiKey;
        }

        /// <summary>
        /// Sends the request with automatic retry on HTTP 429 (rate limited), using exponential
        /// backoff starting at 2 seconds and doubling each attempt, up to 3 retries. All failure
        /// modes except user cancellation (timeout, non-2xx response, network/parsing exception)
        /// are converted into a <see cref="ReportResponse"/> with <see cref="ReportResponse.IsSuccess"/>
        /// = false rather than thrown.
        /// </summary>
        public async Task<ReportResponse> GenerateReportAsync(ReportRequest request, CancellationToken ct = default)
        {
            int maxRetries = 3;
            int delayMs = 2000;

            for (int i = 0; i <= maxRetries; i++)
            {
                try
                {
                    var httpRequest = BuildRequest(request, streaming: false);
                    var response = await _httpClient.SendAsync(httpRequest, ct);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync(ct);
                        string generatedText = ParseResponse(responseBody);
                        return new ReportResponse { IsSuccess = true, GeneratedReport = generatedText.Trim() };
                    }

                    // Exponential backoff for rate limiting (HTTP 429): 2s, 4s, 8s before giving up.
                    if (response.StatusCode == (System.Net.HttpStatusCode)429 && i < maxRetries)
                    {
                        await Task.Delay(delayMs, ct);
                        delayMs *= 2;
                        continue;
                    }

                    string errBody = await response.Content.ReadAsStringAsync(ct);
                    return new ReportResponse { IsSuccess = false, ErrorMessage = $"API Error ({response.StatusCode}): {errBody}" };
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw; // user cancellation — the caller handles it explicitly
                }
                catch (TaskCanceledException)
                {
                    return new ReportResponse { IsSuccess = false, IsTimeout = true, ErrorMessage = "CONNECTION TIMEOUT: The downstream system connection window expired." };
                }
                catch (Exception ex)
                {
                    return new ReportResponse { IsSuccess = false, ErrorMessage = $"Network/Parsing Error: {ex.Message}" };
                }
            }

            return new ReportResponse { IsSuccess = false, ErrorMessage = "API Error: Maximum retry attempts exceeded." };
        }

        /// <summary>
        /// Streams the report as server-sent events, invoking <paramref name="onDelta"/> per text
        /// fragment. Any failure to establish or read the stream (including a 429, whose backoff
        /// logic lives in the non-streaming path) falls back to <see cref="GenerateReportAsync"/> —
        /// the teacher just gets the report all at once instead of progressively, never an extra error.
        /// </summary>
        public async Task<ReportResponse> GenerateReportStreamAsync(ReportRequest request, Action<string> onDelta, CancellationToken ct = default)
        {
            if (!SupportsStreaming) return await GenerateReportAsync(request, ct);

            try
            {
                var httpRequest = BuildRequest(request, streaming: true);
                var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                    return await GenerateReportAsync(request, ct);

                var full = new StringBuilder();
                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream);

                while (await reader.ReadLineAsync(ct) is { } line)
                {
                    if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                    string data = line["data:".Length..].Trim();
                    if (data == "[DONE]") break;

                    string? delta = ParseStreamEvent(data);
                    if (!string.IsNullOrEmpty(delta))
                    {
                        full.Append(delta);
                        onDelta(delta);
                    }
                }

                // An empty stream (e.g. a proxy returned 200 with a non-SSE body) is a failure —
                // fall back rather than handing the teacher a blank report.
                if (full.Length == 0) return await GenerateReportAsync(request, ct);

                return new ReportResponse { IsSuccess = true, GeneratedReport = full.ToString().Trim() };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (TaskCanceledException)
            {
                return new ReportResponse { IsSuccess = false, IsTimeout = true, ErrorMessage = "CONNECTION TIMEOUT: The downstream system connection window expired." };
            }
            catch (Exception)
            {
                return await GenerateReportAsync(request, ct);
            }
        }

        /// <summary>Providers that can't stream override this to false; their
        /// <see cref="GenerateReportStreamAsync"/> then delegates straight to the non-streaming call.</summary>
        protected virtual bool SupportsStreaming => true;

        /// <summary>Builds the provider-specific HTTP request (endpoint, headers, JSON payload) from
        /// the generic <see cref="ReportRequest"/>, using <see cref="PromptBuilderService"/> to assemble
        /// the prompt text. When <paramref name="streaming"/> is true the payload/URL must request SSE.</summary>
        protected abstract HttpRequestMessage BuildRequest(ReportRequest request, bool streaming = false);

        /// <summary>Extracts the generated report text from the provider's raw JSON response body.</summary>
        protected abstract string ParseResponse(string responseBody);

        /// <summary>Extracts the text fragment from one SSE <c>data:</c> event's JSON, or null for
        /// events that carry no text (pings, role announcements, stop events).</summary>
        protected virtual string? ParseStreamEvent(string eventJson) => null;
    }
}
