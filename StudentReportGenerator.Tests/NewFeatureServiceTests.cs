using System.IO;
using StudentReportGenerator.Models;
using StudentReportGenerator.Services;
using Xunit;

namespace StudentReportGenerator.Tests
{
    public class ReadabilityServiceTests
    {
        [Fact]
        public void SimpleText_ScoresEasierThanDenseText()
        {
            string simple = "Tom did well this term. He works hard. He is kind to his friends.";
            string dense = "Notwithstanding considerable multidisciplinary pedagogical differentiation, the aforementioned individual demonstrated intermittently satisfactory metacognitive self-regulation capabilities.";

            Assert.True(ReadabilityService.FleschReadingEase(simple) > ReadabilityService.FleschReadingEase(dense));
        }

        [Fact]
        public void EmptyText_ReturnsEmptyDescription()
        {
            Assert.Equal(string.Empty, ReadabilityService.DescribeReadingLevel(""));
            Assert.Equal(string.Empty, ReadabilityService.DescribeReadingLevel("   "));
        }

        [Fact]
        public void Description_ContainsFleschScore()
        {
            string result = ReadabilityService.DescribeReadingLevel("The cat sat on the mat. It was warm.");
            Assert.Contains("Reading level:", result);
            Assert.Contains("Flesch", result);
        }
    }

    public class SafeguardingScanServiceTests
    {
        [Fact]
        public void NotesWithSafeguardingLanguage_AreFlagged()
        {
            var hits = SafeguardingScanService.Scan("He disclosed that he is scared of going home and has bruises on his arm.");
            Assert.NotEmpty(hits);
            Assert.Contains(hits, h => h.Contains("disclosed"));
            Assert.Contains(hits, h => h.Contains("scared of"));
        }

        [Fact]
        public void OrdinaryTeachingNotes_AreNotFlagged()
        {
            var hits = SafeguardingScanService.Scan("Sarah has settled well this term and contributes enthusiastically in class discussions.");
            Assert.Empty(hits);
        }

        [Fact]
        public void NullOrEmptyNotes_ReturnNoHits()
        {
            Assert.Empty(SafeguardingScanService.Scan(null));
            Assert.Empty(SafeguardingScanService.Scan(""));
        }
    }

    public class CommentBankServiceTests
    {
        [Fact]
        public void Suggest_FiltersCaseInsensitively()
        {
            var phrases = new System.Collections.Generic.List<string> { "has settled well this term", "checks work carefully", "reads widely" };

            var filtered = CommentBankService.Suggest(phrases, "SETTLED");

            Assert.Single(filtered);
            Assert.Equal("has settled well this term", filtered[0]);
        }

        [Fact]
        public void Suggest_EmptyFilter_ReturnsEverything()
        {
            var phrases = new System.Collections.Generic.List<string> { "a", "b" };
            Assert.Equal(2, CommentBankService.Suggest(phrases, "").Count);
        }
    }

    public class CostEstimatorServiceTests
    {
        [Fact]
        public void NoUsage_ReportsZero()
        {
            Assert.Contains("$0.00", CostEstimatorService.EstimateCostSummary(0, 0, 0, 0, 0));
        }

        [Fact]
        public void NvidiaOnlyUsage_CostsNothing()
        {
            string summary = CostEstimatorService.EstimateCostSummary(1_000_000, nvidiaReports: 10, geminiReports: 0, openAiReports: 0, claudeReports: 0);
            Assert.Contains("$0.00", summary);
        }

        [Fact]
        public void PaidProviderUsage_ProducesNonZeroEstimate()
        {
            string summary = CostEstimatorService.EstimateCostSummary(10_000_000, 0, 0, 0, claudeReports: 10);
            Assert.DoesNotContain("~$0.00", summary);
            Assert.Contains("Estimated AI running cost", summary);
        }
    }

    [Collection("SisCacheFile")] // serialised with SisCacheTimestampTests — both touch sis_stats_cache.dat
    public class SchoolDataCacheTests : DataFileBackupFixture
    {
        public SchoolDataCacheTests() : base("sis_stats_cache.dat") { }

        [Fact]
        public void UpsertAndLoad_RoundTripsEncrypted()
        {
            var stats = new StudentAcademicStats
            {
                ExternalStudentId = "UPN-TEST-001",
                AttendancePercent = 94.5,
                BehaviourPoints = 3,
                TargetGrade = "7",
                SupportPlanSummary = "Extra reading time",
                LastSyncedUtc = DateTime.UtcNow
            };
            stats.RecentGrades["Maths"] = "6";

            SchoolDataCacheService.UpsertStats(stats, retentionDays: 30);
            var loaded = SchoolDataCacheService.LoadCache(30);

            Assert.True(loaded.ContainsKey("UPN-TEST-001"));
            Assert.Equal(94.5, loaded["UPN-TEST-001"].AttendancePercent);
            Assert.Equal("6", loaded["UPN-TEST-001"].RecentGrades["Maths"]);

            // Encrypted at rest: raw file must not contain the plaintext identifiers
            string raw = File.ReadAllText(FileSandboxService.GetSafeFilePath("sis_stats_cache.dat"));
            Assert.DoesNotContain("UPN-TEST-001", raw);
        }

        [Fact]
        public void ExpiredEntries_ArePurgedOnLoad()
        {
            var stale = new StudentAcademicStats { ExternalStudentId = "UPN-STALE", LastSyncedUtc = DateTime.UtcNow.AddDays(-200) };
            var fresh = new StudentAcademicStats { ExternalStudentId = "UPN-FRESH", LastSyncedUtc = DateTime.UtcNow };
            SchoolDataCacheService.SaveCache(new Dictionary<string, StudentAcademicStats>
            {
                ["UPN-STALE"] = stale,
                ["UPN-FRESH"] = fresh,
            });

            var loaded = SchoolDataCacheService.LoadCache(retentionDays: 120);

            Assert.False(loaded.ContainsKey("UPN-STALE"));
            Assert.True(loaded.ContainsKey("UPN-FRESH"));
        }
    }

    public class PromptBuilderNewBehaviourTests
    {
        [Fact]
        public void UtilityInstruction_BypassesReportFraming()
        {
            var request = new ReportRequest
            {
                UtilityInstruction = "Translate the following into Polish.",
                RawNotes = "Tom did well this term.",
                StudentName = "Utility"
            };

            string prompt = PromptBuilderService.BuildSecurePrompt(request);

            Assert.Contains("Translate the following into Polish.", prompt);
            Assert.Contains("<content>", prompt);
            Assert.DoesNotContain("student performance report", prompt);
        }

        [Fact]
        public void SisGroundedFacts_AppearOnlyWhenSet()
        {
            var bare = new ReportRequest { StudentName = "A", Subject = "Maths", RawNotes = "notes" };
            string barePrompt = PromptBuilderService.BuildSecurePrompt(bare);
            Assert.DoesNotContain("Verified Attendance", barePrompt);
            Assert.DoesNotContain("Verified Behaviour", barePrompt);

            var grounded = new ReportRequest
            {
                StudentName = "A",
                Subject = "Maths",
                RawNotes = "notes",
                AttendancePercent = 88.2,
                BehaviourPoints = 4,
                RecentGradesSummary = "Maths: 6; Science: 7"
            };
            string groundedPrompt = PromptBuilderService.BuildSecurePrompt(grounded);
            Assert.Contains("Verified Attendance This Term: 88.2%", groundedPrompt);
            Assert.Contains("Verified Behaviour Points This Term: 4", groundedPrompt);
            Assert.Contains("Maths: 6; Science: 7", groundedPrompt);
        }

        [Fact]
        public void ReportPrompt_IncludesPhrasingVariationInstruction()
        {
            var request = new ReportRequest { StudentName = "A", Subject = "Maths", RawNotes = "notes" };
            Assert.Contains("Vary your sentence structure", PromptBuilderService.BuildSecurePrompt(request));
        }
    }

    public class FrameworkShareServiceTests : IDisposable
    {
        private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"ff-library-{Guid.NewGuid():N}.json");

        public void Dispose()
        {
            if (File.Exists(_tempFile)) File.Delete(_tempFile);
        }

        [Fact]
        public void ExportThenImport_MergesWithoutDuplicates()
        {
            var sourceFrameworks = new List<ReportFramework>
            {
                new ReportFramework { Name = "Formal", Instruction = "Formal tone." },
                new ReportFramework { Name = "Warm", Instruction = "Warm tone." },
            };
            var sourceTopics = new List<string> { "Algebra", "Poetry" };
            FrameworkShareService.Export(_tempFile, sourceFrameworks, sourceTopics);

            var targetFrameworks = new List<ReportFramework> { new ReportFramework { Name = "Formal", Instruction = "Existing copy." } };
            var targetTopics = new List<string> { "Algebra" };

            var (frameworksAdded, topicsAdded) = FrameworkShareService.Import(_tempFile, targetFrameworks, targetTopics);

            Assert.Equal(1, frameworksAdded); // only "Warm" is new
            Assert.Equal(1, topicsAdded);     // only "Poetry" is new
            Assert.Equal(2, targetFrameworks.Count);
            Assert.Equal(2, targetTopics.Count);
        }
    }
}
