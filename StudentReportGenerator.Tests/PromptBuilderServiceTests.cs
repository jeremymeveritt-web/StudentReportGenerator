using StudentReportGenerator.Models;
using StudentReportGenerator.Services;
using Xunit;

namespace StudentReportGenerator.Tests
{
    public class PromptBuilderServiceTests
    {
        private static ReportRequest MakeRequest() => new ReportRequest
        {
            StudentName = "Charlie Example",
            Subject = "Intro to Physics",
            WordCount = 500,
            RawNotes = "Excellent lab work this term.",
            Pronouns = "She/Her",
            TargetGrade = "A",
            SupportNeeds = "Extra reading time",
            TeacherSignoff = "Dr. Example"
        };

        [Fact]
        public void BuildSecurePrompt_IncludesCoreRequestFields()
        {
            string prompt = PromptBuilderService.BuildSecurePrompt(MakeRequest());

            Assert.Contains("Charlie Example", prompt);
            Assert.Contains("Intro to Physics", prompt);
            Assert.Contains("She/Her", prompt);
            Assert.Contains("Approximately 500 words", prompt);
            Assert.Contains("Excellent lab work this term.", prompt);
            Assert.Contains("Target Grade: A", prompt);
            Assert.Contains("Extra reading time", prompt);
            Assert.Contains("Sign off the report cleanly with: Dr. Example", prompt);
        }

        [Fact]
        public void BuildSecurePrompt_WrapsStudentDataInQuarantineTags()
        {
            string prompt = PromptBuilderService.BuildSecurePrompt(MakeRequest());

            int open = prompt.IndexOf("<student_data>", StringComparison.Ordinal);
            int name = prompt.IndexOf("Name: Charlie Example", StringComparison.Ordinal);
            int close = prompt.IndexOf("</student_data>", StringComparison.Ordinal);

            Assert.True(open >= 0 && name > open && close > name, "student data must sit inside the quarantine tags");
        }

        [Fact]
        public void BuildSecurePrompt_OmitsOptionalSectionsWhenEmpty()
        {
            var request = MakeRequest();
            request.SelectedFramework = string.Empty;
            request.TargetGrade = string.Empty;
            request.SupportNeeds = string.Empty;
            request.TeacherSignoff = "Mr. / Ms. Teacher"; // placeholder default must not be signed

            string prompt = PromptBuilderService.BuildSecurePrompt(request);

            Assert.DoesNotContain("TONE AND STYLE FRAMEWORK", prompt);
            Assert.DoesNotContain("Target Grade:", prompt);
            Assert.DoesNotContain("Special Educational Needs", prompt);
            Assert.DoesNotContain("Sign off the report cleanly", prompt);
        }

        [Fact]
        public void BuildSecurePrompt_IncludesFrameworkWhenSet()
        {
            var request = MakeRequest();
            request.SelectedFramework = "Use a supportive, gentle tone.";

            string prompt = PromptBuilderService.BuildSecurePrompt(request);

            Assert.Contains("TONE AND STYLE FRAMEWORK: Use a supportive, gentle tone.", prompt);
        }
    }
}
