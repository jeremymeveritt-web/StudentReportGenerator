using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using StudentReportGenerator.Models;
using StudentReportGenerator.Services;
using Xunit;

namespace StudentReportGenerator.Tests
{
    public class SisCsvImportServiceTests
    {
        [Fact]
        public void Parse_HeaderColumnsInAnyOrder_MapsAllFields()
        {
            var lines = new[]
            {
                "AttendancePercent,Name,BehaviourPoints,ExternalStudentId,Grades,SupportPlan,TargetGrade",
                "94.5,Amelia Khan,3,UPN001,\"Maths=6; Science=7\",Extra reading time,7",
            };

            var result = SisCsvImportService.Parse(lines);

            Assert.Empty(result.Warnings);
            var row = Assert.Single(result.Rows);
            Assert.Equal("UPN001", row.Stats.ExternalStudentId);
            Assert.Equal("Amelia Khan", row.Name);
            Assert.Equal(94.5, row.Stats.AttendancePercent);
            Assert.Equal(3, row.Stats.BehaviourPoints);
            Assert.Equal("6", row.Stats.RecentGrades["Maths"]);
            Assert.Equal("7", row.Stats.RecentGrades["Science"]);
            Assert.Equal("Extra reading time", row.Stats.SupportPlanSummary);
            Assert.Equal("7", row.Stats.TargetGrade);
        }

        [Fact]
        public void Parse_HeaderAliases_AreRecognised()
        {
            var lines = new[] { "UPN,Full Name,Attendance", "X123,Ben Ode,88" };

            var result = SisCsvImportService.Parse(lines);

            var row = Assert.Single(result.Rows);
            Assert.Equal("X123", row.Stats.ExternalStudentId);
            Assert.Equal("Ben Ode", row.Name);
            Assert.Equal(88, row.Stats.AttendancePercent);
        }

        [Fact]
        public void Parse_QuotedCommasInsideCells_DoNotSplit()
        {
            var lines = new[]
            {
                "ExternalStudentId,Name,SupportPlan",
                "U1,\"Khan, Amelia\",\"Dyslexia, coloured overlays\"",
            };

            var result = SisCsvImportService.Parse(lines);

            var row = Assert.Single(result.Rows);
            Assert.Equal("Khan, Amelia", row.Name);
            Assert.Equal("Dyslexia, coloured overlays", row.Stats.SupportPlanSummary);
        }

        [Fact]
        public void Parse_MissingIdColumn_ReturnsWarningAndNoRows()
        {
            var result = SisCsvImportService.Parse(new[] { "Name,Class", "Tom,9A" });

            Assert.Empty(result.Rows);
            Assert.Contains(result.Warnings, w => w.Contains("ExternalStudentId"));
        }

        [Fact]
        public void Parse_BadNumbers_WarnButDoNotThrow()
        {
            var lines = new[]
            {
                "ExternalStudentId,AttendancePercent,BehaviourPoints",
                "U1,ninety,two",
            };

            var result = SisCsvImportService.Parse(lines);

            var row = Assert.Single(result.Rows);
            Assert.Null(row.Stats.AttendancePercent);
            Assert.Null(row.Stats.BehaviourPoints);
            Assert.Equal(2, result.Warnings.Count);
        }

        [Fact]
        public void Parse_RowWithoutId_IsSkippedWithWarning()
        {
            var lines = new[] { "ExternalStudentId,Name", "U1,Ada", ",NoId Kid" };

            var result = SisCsvImportService.Parse(lines);

            Assert.Single(result.Rows);
            Assert.Contains(result.Warnings, w => w.Contains("no student ID"));
        }

        [Fact]
        public void ParseGrades_RoundTripsAndIgnoresJunk()
        {
            var grades = SisCsvImportService.ParseGrades("Maths=6; Science=7 ; junk ; English = A*");

            Assert.Equal(3, grades.Count);
            Assert.Equal("A*", grades["English"]);
        }

        [Fact]
        public void MatchRoster_FillsEmptyIdsByNameOnly()
        {
            var rows = SisCsvImportService.Parse(new[]
            {
                "ExternalStudentId,Name",
                "U1,Amelia Khan",
                "U2,Ben Ode",
                "U3,Unknown Student",
            }).Rows;
            var roster = new List<StudentProfile>
            {
                new() { FullName = "amelia khan" },                              // matches case-insensitively
                new() { FullName = "Ben Ode", ExternalStudentId = "KEEP-ME" },   // already matched — untouched
                new() { FullName = "Cara Diaz" },                                // not in the CSV
            };

            int matched = SisCsvImportService.MatchRoster(rows, roster);

            Assert.Equal(1, matched);
            Assert.Equal("U1", roster[0].ExternalStudentId);
            Assert.Equal("KEEP-ME", roster[1].ExternalStudentId);
            Assert.Equal(string.Empty, roster[2].ExternalStudentId);
        }
    }

    public class WondeJsonMapperTests
    {
        [Fact]
        public void MapStudentJson_FullPayload_MapsAttendanceBehaviourAndTarget()
        {
            string json = """
            {
              "data": {
                "id": "B1234",
                "attendance_summary": { "data": { "details": { "percentage_attendance": 96.2 } } },
                "behaviours": { "data": [ { "points": 2 }, { "points": -1 } ] },
                "extended_details": { "data": { "target_grade": "8" } }
              }
            }
            """;

            var stats = WondeJsonMapper.MapStudentJson(json, "B1234");

            Assert.NotNull(stats);
            Assert.Equal("B1234", stats!.ExternalStudentId);
            Assert.Equal(96.2, stats.AttendancePercent);
            Assert.Equal(1, stats.BehaviourPoints);
            Assert.Equal("8", stats.TargetGrade);
        }

        [Fact]
        public void MapStudentJson_MinimalPayload_ReturnsPartialStatsNotThrow()
        {
            var stats = WondeJsonMapper.MapStudentJson("""{ "data": { "id": "B1" } }""", "B1");

            Assert.NotNull(stats);
            Assert.Null(stats!.AttendancePercent);
            Assert.Null(stats.BehaviourPoints);
            Assert.Equal(string.Empty, stats.TargetGrade);
        }

        [Fact]
        public void MapStudentJson_InvalidJsonOrShape_ReturnsNull()
        {
            Assert.Null(WondeJsonMapper.MapStudentJson("not json at all", "B1"));
            Assert.Null(WondeJsonMapper.MapStudentJson("""{ "error": "nope" }""", "B1"));
        }
    }

    public class WondeSchoolDatabaseServiceTests
    {
        private static WondeSchoolDatabaseService Create(FakeHttpMessageHandler handler,
            string token = "tok_test", string schoolId = "A193")
            => new(handler.CreateClient(), token, schoolId);

        [Fact]
        public async Task GetStudentStats_Success_ParsesAndSendsBearerAuth()
        {
            var handler = new FakeHttpMessageHandler
            {
                Responder = (_, _) => Task.FromResult(CannedResponses.Json(HttpStatusCode.OK, """
                    { "data": { "attendance_summary": { "data": { "details": { "percentage_attendance": 91 } } } } }
                    """)),
            };

            var stats = await Create(handler).GetStudentStatsAsync("B77");

            Assert.NotNull(stats);
            Assert.Equal(91, stats!.AttendancePercent);
            var request = Assert.Single(handler.Requests);
            Assert.Equal("Bearer tok_test", request.Headers.GetValues("Authorization").Single());
            Assert.Contains("/schools/A193/students/B77", request.RequestUri!.ToString());
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.NotFound)]
        public async Task GetStudentStats_AuthOrMatchFailure_ReturnsNull(HttpStatusCode code)
        {
            var handler = new FakeHttpMessageHandler
            {
                Responder = (_, _) => Task.FromResult(CannedResponses.Json(code, "{}")),
            };

            Assert.Null(await Create(handler).GetStudentStatsAsync("B77"));
        }

        [Fact]
        public async Task GetStudentStats_ServerError_ThrowsForCacheFallback()
        {
            var handler = new FakeHttpMessageHandler
            {
                Responder = (_, _) => Task.FromResult(CannedResponses.Json(HttpStatusCode.InternalServerError, "{}")),
            };

            await Assert.ThrowsAsync<HttpRequestException>(() => Create(handler).GetStudentStatsAsync("B77"));
        }

        [Fact]
        public async Task GetStudentStats_MissingCredentials_ReturnsNullWithoutHttpCall()
        {
            var handler = new FakeHttpMessageHandler();

            Assert.Null(await Create(handler, token: "").GetStudentStatsAsync("B77"));
            Assert.Empty(handler.Requests);
        }
    }

    /// <summary>Regression guard for the retention fix: a cache-backed read must not refresh
    /// <see cref="StudentAcademicStats.LastSyncedUtc"/>, or imported CSV data would never expire.</summary>
    [Collection("SisCacheFile")] // serialised with SchoolDataCacheTests — both touch sis_stats_cache.dat
    public class SisCacheTimestampTests : DataFileBackupFixture
    {
        public SisCacheTimestampTests() : base("sis_stats_cache.dat") { }

        [Fact]
        public void ImportTimestamp_SurvivesUpsertOfCachedStats()
        {
            var imported = new StudentAcademicStats
            {
                ExternalStudentId = "CSV-1",
                AttendancePercent = 90,
                LastSyncedUtc = DateTime.UtcNow.AddDays(-30),
            };
            SchoolDataCacheService.UpsertStats(imported, retentionDays: 120);

            // Simulates the orchestrator re-upserting what a cache-backed provider returned:
            // the original timestamp is already set, so it must be preserved.
            var readBack = SchoolDataCacheService.LoadCache(120)["CSV-1"];
            if (readBack.LastSyncedUtc == default) readBack.LastSyncedUtc = DateTime.UtcNow;
            SchoolDataCacheService.UpsertStats(readBack, retentionDays: 120);

            var final = SchoolDataCacheService.LoadCache(120)["CSV-1"];
            Assert.True(final.LastSyncedUtc < DateTime.UtcNow.AddDays(-29),
                "LastSyncedUtc was refreshed by a read — retention would never expire imported data.");
        }
    }
}
