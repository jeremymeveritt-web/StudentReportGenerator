using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StudentReportGenerator.Tests
{
    /// <summary>
    /// Hand-rolled HttpMessageHandler stub so AI-provider and SIS-connector services can be tested
    /// without any network access or mocking library. Assign <see cref="Responder"/> to script the
    /// responses; every request (and its body, captured before disposal) is recorded for assertions.
    /// </summary>
    public sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Responder { get; set; }
            = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> RequestBodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestBodies.Add(request.Content != null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : string.Empty);
            return await Responder(request, cancellationToken);
        }

        public HttpClient CreateClient() => new(this) { Timeout = TimeSpan.FromSeconds(10) };
    }

    /// <summary>Builders for canned HTTP responses (SSE streams and plain JSON bodies).</summary>
    public static class CannedResponses
    {
        /// <summary>Wraps raw SSE text (lines like "data: {...}") in a 200 response served from a stream.</summary>
        public static HttpResponseMessage Sse(string sseBody)
        {
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(sseBody));
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
            return response;
        }

        public static HttpResponseMessage Json(HttpStatusCode code, string json) => new(code)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}
