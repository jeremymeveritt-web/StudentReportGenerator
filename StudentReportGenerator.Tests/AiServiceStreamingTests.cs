using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StudentReportGenerator.Models;
using StudentReportGenerator.Services;
using Xunit;

namespace StudentReportGenerator.Tests
{
    public class AiServiceStreamingTests
    {
        private static ReportRequest Request() => new()
        {
            StudentName = "Test Student",
            Subject = "Science",
            RawNotes = "Great progress",
            WordCount = 100,
            SelectedModel = "test-model",
        };

        // --- OpenAI / NVIDIA (Chat Completions SSE) ---

        private const string ChatCompletionsSse =
            "data: {\"choices\":[{\"delta\":{\"role\":\"assistant\"}}]}\n" +
            "\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"Hello \"}}]}\n" +
            "data: {\"choices\":[{\"delta\":{\"content\":\"world\"}}]}\n" +
            "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n" +
            "data: [DONE]\n";

        [Fact]
        public async Task OpenAi_Stream_AssemblesDeltasAndSkipsRoleAndFinishChunks()
        {
            var handler = new FakeHttpMessageHandler { Responder = (_, _) => Task.FromResult(CannedResponses.Sse(ChatCompletionsSse)) };
            var service = new OpenAiReportService(handler.CreateClient(), "key");
            var deltas = new List<string>();

            var response = await service.GenerateReportStreamAsync(Request(), deltas.Add);

            Assert.True(response.IsSuccess);
            Assert.Equal("Hello world", response.GeneratedReport);
            Assert.Equal(new[] { "Hello ", "world" }, deltas);
            Assert.Contains("\"stream\":true", handler.RequestBodies[0]);
        }

        [Fact]
        public async Task Nvidia_Stream_UsesSameChatCompletionsShape()
        {
            var handler = new FakeHttpMessageHandler { Responder = (_, _) => Task.FromResult(CannedResponses.Sse(ChatCompletionsSse)) };
            var service = new NvidiaReportService(handler.CreateClient(), "key");

            var response = await service.GenerateReportStreamAsync(Request(), _ => { });

            Assert.True(response.IsSuccess);
            Assert.Equal("Hello world", response.GeneratedReport);
        }

        // --- Claude (Messages API SSE) ---

        [Fact]
        public async Task Claude_Stream_ReadsOnlyContentBlockDeltas()
        {
            string sse =
                "event: message_start\n" +
                "data: {\"type\":\"message_start\",\"message\":{\"id\":\"m1\"}}\n" +
                "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"Dear \"}}\n" +
                "data: {\"type\":\"ping\"}\n" +
                "data: {\"type\":\"content_block_delta\",\"delta\":{\"type\":\"text_delta\",\"text\":\"parent\"}}\n" +
                "data: {\"type\":\"content_block_stop\"}\n" +
                "data: {\"type\":\"message_stop\"}\n";
            var handler = new FakeHttpMessageHandler { Responder = (_, _) => Task.FromResult(CannedResponses.Sse(sse)) };
            var service = new ClaudeReportService(handler.CreateClient(), "key");
            var deltas = new List<string>();

            var response = await service.GenerateReportStreamAsync(Request(), deltas.Add);

            Assert.True(response.IsSuccess);
            Assert.Equal("Dear parent", response.GeneratedReport);
            Assert.Equal(2, deltas.Count);
        }

        // --- Gemini (streamGenerateContent?alt=sse) ---

        [Fact]
        public async Task Gemini_Stream_ReadsCandidateParts_AndUsesStreamingEndpoint()
        {
            string sse =
                "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"Part one \"}]}}]}\n" +
                "data: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"part two\"}]}}]}\n" +
                "data: {\"candidates\":[{\"finishReason\":\"STOP\"}]}\n";
            var handler = new FakeHttpMessageHandler { Responder = (_, _) => Task.FromResult(CannedResponses.Sse(sse)) };
            var service = new GeminiReportService(handler.CreateClient(), "key");

            var response = await service.GenerateReportStreamAsync(Request(), _ => { });

            Assert.True(response.IsSuccess);
            Assert.Equal("Part one part two", response.GeneratedReport);
            Assert.Contains(":streamGenerateContent?alt=sse", handler.Requests[0].RequestUri!.ToString());
        }

        // --- Fallback & failure contract ---

        [Fact]
        public async Task Stream_ErrorStatus_FallsBackToNonStreamingCall()
        {
            int call = 0;
            var handler = new FakeHttpMessageHandler
            {
                Responder = (_, _) => Task.FromResult(++call == 1
                    ? CannedResponses.Json(HttpStatusCode.InternalServerError, "{}")
                    : CannedResponses.Json(HttpStatusCode.OK, """{"choices":[{"message":{"content":"Full report"}}]}""")),
            };
            var service = new OpenAiReportService(handler.CreateClient(), "key");

            var response = await service.GenerateReportStreamAsync(Request(), _ => { });

            Assert.True(response.IsSuccess);
            Assert.Equal("Full report", response.GeneratedReport);
            Assert.Equal(2, handler.Requests.Count);
            Assert.DoesNotContain("\"stream\":true", handler.RequestBodies[1]);
        }

        [Fact]
        public async Task UserCancellation_PropagatesAsOperationCanceled()
        {
            var handler = new FakeHttpMessageHandler
            {
                Responder = async (_, ct) => { await Task.Delay(5000, ct); return new HttpResponseMessage(HttpStatusCode.OK); },
            };
            var service = new OpenAiReportService(handler.CreateClient(), "key");
            using var cts = new CancellationTokenSource(50);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => service.GenerateReportAsync(Request(), cts.Token));
        }

        [Fact]
        public async Task HttpTimeout_BecomesIsTimeoutResponse_NotException()
        {
            // A TaskCanceledException whose token is NOT the caller's = HttpClient timeout
            var handler = new FakeHttpMessageHandler
            {
                Responder = (_, _) => throw new TaskCanceledException("timed out"),
            };
            var service = new OpenAiReportService(handler.CreateClient(), "key");

            var response = await service.GenerateReportAsync(Request());

            Assert.False(response.IsSuccess);
            Assert.True(response.IsTimeout);
        }

        // --- Request shape: system prompts & temperature ---

        [Fact]
        public async Task Claude_Request_CarriesSystemPromptAndTemperature()
        {
            var handler = new FakeHttpMessageHandler
            {
                Responder = (_, _) => Task.FromResult(CannedResponses.Json(HttpStatusCode.OK, """{"content":[{"text":"ok"}]}""")),
            };
            var service = new ClaudeReportService(handler.CreateClient(), "key");
            var request = Request();
            request.Temperature = 0.3;

            await service.GenerateReportAsync(request);

            using var doc = JsonDocument.Parse(handler.RequestBodies[0]);
            Assert.Contains("educational assistant", doc.RootElement.GetProperty("system").GetString());
            Assert.Equal(0.3, doc.RootElement.GetProperty("temperature").GetDouble());
            Assert.Contains("<student_data>", doc.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
        }

        [Fact]
        public async Task OpenAi_Request_UsesSystemRoleMessage()
        {
            var handler = new FakeHttpMessageHandler
            {
                Responder = (_, _) => Task.FromResult(CannedResponses.Json(HttpStatusCode.OK, """{"choices":[{"message":{"content":"ok"}}]}""")),
            };
            await new OpenAiReportService(handler.CreateClient(), "key").GenerateReportAsync(Request());

            using var doc = JsonDocument.Parse(handler.RequestBodies[0]);
            var messages = doc.RootElement.GetProperty("messages");
            Assert.Equal("system", messages[0].GetProperty("role").GetString());
            Assert.Equal("user", messages[1].GetProperty("role").GetString());
        }

        [Fact]
        public async Task Gemini_Request_UsesSystemInstructionField()
        {
            var handler = new FakeHttpMessageHandler
            {
                Responder = (_, _) => Task.FromResult(CannedResponses.Json(HttpStatusCode.OK,
                    """{"candidates":[{"content":{"parts":[{"text":"ok"}]}}]}""")),
            };
            await new GeminiReportService(handler.CreateClient(), "key").GenerateReportAsync(Request());

            using var doc = JsonDocument.Parse(handler.RequestBodies[0]);
            Assert.Contains("educational assistant",
                doc.RootElement.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        }
    }

    public class PromptPartsTests
    {
        [Fact]
        public void BuildSecurePrompt_EqualsSystemPlusUserConcatenation()
        {
            var request = new ReportRequest { StudentName = "Ana", Subject = "Maths", RawNotes = "notes", Pronouns = "She/Her" };

            var parts = PromptBuilderService.BuildPromptParts(request);

            Assert.Equal(parts.SystemInstructions + parts.UserContent, PromptBuilderService.BuildSecurePrompt(request));
            Assert.Contains("educational assistant", parts.SystemInstructions);
            Assert.Contains("<student_data>", parts.UserContent);
            Assert.DoesNotContain("<student_data>", parts.SystemInstructions);
        }

        [Fact]
        public void StyleExemplars_AppearInSystemPrompt_CappedAtMax()
        {
            var request = new ReportRequest
            {
                StudentName = "Ana",
                Subject = "Maths",
                RawNotes = "notes",
                StyleExemplars = Enumerable.Range(1, 20).Select(i => $"Phrase number {i}").ToList(),
            };

            var parts = PromptBuilderService.BuildPromptParts(request);

            Assert.Contains("STYLE EXEMPLARS", parts.SystemInstructions);
            Assert.Contains("Phrase number 8", parts.SystemInstructions);
            Assert.DoesNotContain("Phrase number 9", parts.SystemInstructions);
        }

        [Fact]
        public void NoExemplars_NoExemplarBlock()
        {
            var parts = PromptBuilderService.BuildPromptParts(new ReportRequest { StudentName = "Ana", Subject = "Maths", RawNotes = "n" });

            Assert.DoesNotContain("STYLE EXEMPLARS", parts.SystemInstructions);
        }

        [Fact]
        public void UtilityMode_SplitsInstructionFromQuarantinedContent()
        {
            var request = new ReportRequest { UtilityInstruction = "Translate to Polish.", RawNotes = "Report body" };

            var parts = PromptBuilderService.BuildPromptParts(request);

            Assert.Equal("Translate to Polish.", parts.SystemInstructions);
            Assert.Contains("<content>", parts.UserContent);
            Assert.Contains("Report body", parts.UserContent);
        }
    }
}
