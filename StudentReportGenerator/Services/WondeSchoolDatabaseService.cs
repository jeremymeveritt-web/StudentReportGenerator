using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Live Wonde (UK) connector: fetches one student's verified stats on demand from
    /// <c>https://api.wonde.com/v1.0/schools/{schoolId}/students/{studentId}</c> with the school's
    /// bearer token. Wonde aggregates the major UK MIS platforms (SIMS, Arbor, Bromcom,
    /// ScholarPack), so this single connector covers most UK schools once a Data Processing
    /// Agreement is signed and the school approves the token's scopes.
    /// Auth/match failures (401/404) return <c>null</c> — the caller falls back to manual entry;
    /// transient failures throw so <see cref="SchoolDataOrchestratorService"/> falls back to the
    /// encrypted local cache instead.
    /// </summary>
    public class WondeSchoolDatabaseService : ISchoolDatabaseService
    {
        public const string BaseUrl = "https://api.wonde.com/v1.0";

        private readonly HttpClient _httpClient;
        private readonly string _token;
        private readonly string _schoolId;

        public WondeSchoolDatabaseService(HttpClient httpClient, string token, string schoolId)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _token = token;
            _schoolId = schoolId;
        }

        public string ProviderName => "Wonde (UK)";

        public async Task<StudentAcademicStats?> GetStudentStatsAsync(string studentId)
        {
            if (string.IsNullOrWhiteSpace(_token) || string.IsNullOrWhiteSpace(_schoolId)) return null;

            string url = $"{BaseUrl}/schools/{Uri.EscapeDataString(_schoolId)}/students/{Uri.EscapeDataString(studentId)}" +
                         "?include=attendance_summary,behaviours,extended_details";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_token}");

            var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                Log.Warning("Wonde rejected the API token for school {SchoolId} (HTTP {Status}) — falling back to manual entry", _schoolId, (int)response.StatusCode);
                return null;
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Log.Information("Wonde has no student with id {StudentId} in school {SchoolId}", studentId, _schoolId);
                return null;
            }

            // Other failures (5xx, rate limits, proxies) are transient: throw so the orchestrator
            // serves the last-synced cache instead of losing the data entirely.
            response.EnsureSuccessStatusCode();

            string body = await response.Content.ReadAsStringAsync();
            return WondeJsonMapper.MapStudentJson(body, studentId);
        }
    }

    /// <summary>
    /// Pure JSON → <see cref="StudentAcademicStats"/> mapping for Wonde responses, kept separate
    /// from the HTTP plumbing so it is unit-testable with canned payloads. Every traversal is
    /// defensive (<c>TryGetProperty</c>): a payload shaped differently than expected yields partial
    /// or <c>null</c> stats, never an exception.
    /// </summary>
    public static class WondeJsonMapper
    {
        public static StudentAcademicStats? MapStudentJson(string json, string externalId)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("data", out var data)) return null;

                var stats = new StudentAcademicStats { ExternalStudentId = externalId };

                // Attendance: data.attendance_summary(.data).details.percentage_attendance
                if (TryUnwrap(data, "attendance_summary", out var attendance) &&
                    attendance.TryGetProperty("details", out var details) &&
                    TryReadDouble(details, "percentage_attendance", out double pct))
                {
                    stats.AttendancePercent = pct;
                }

                // Behaviour: sum of points across data.behaviours(.data)[]
                if (TryUnwrap(data, "behaviours", out var behaviours) && behaviours.ValueKind == JsonValueKind.Array)
                {
                    int total = 0;
                    bool any = false;
                    foreach (var item in behaviours.EnumerateArray())
                    {
                        if (TryReadDouble(item, "points", out double points)) { total += (int)points; any = true; }
                    }
                    if (any) stats.BehaviourPoints = total;
                }

                // Target grade: data.extended_details(.data).target_grade
                if (TryUnwrap(data, "extended_details", out var extended) &&
                    extended.TryGetProperty("target_grade", out var target) &&
                    target.ValueKind == JsonValueKind.String)
                {
                    stats.TargetGrade = target.GetString() ?? string.Empty;
                }

                return stats;
            }
            catch (JsonException)
            {
                Log.Warning("Wonde response was not valid JSON — ignoring");
                return null;
            }
        }

        /// <summary>Wonde wraps most sub-resources in an extra <c>{"data": ...}</c> envelope when
        /// they are included via <c>?include=</c>; this reads <paramref name="name"/> and unwraps
        /// that envelope if present.</summary>
        private static bool TryUnwrap(JsonElement parent, string name, out JsonElement value)
        {
            value = default;
            if (!parent.TryGetProperty(name, out var element)) return false;
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("data", out var inner))
                element = inner;
            value = element;
            return true;
        }

        private static bool TryReadDouble(JsonElement parent, string name, out double value)
        {
            value = 0;
            if (!parent.TryGetProperty(name, out var element)) return false;
            if (element.ValueKind == JsonValueKind.Number) { value = element.GetDouble(); return true; }
            return element.ValueKind == JsonValueKind.String &&
                   double.TryParse(element.GetString(), System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out value);
        }
    }
}
