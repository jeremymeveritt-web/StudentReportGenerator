using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StudentReportGenerator.Models
{
    public class ReportRequest
    {
        public string StudentName { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public int WordCount { get; set; } = 150;
        public string RawNotes { get; set; } = string.Empty;
        public string SelectedFramework { get; set; } = string.Empty;
        public string SchoolName { get; set; } = string.Empty;
        public string TeacherSignoff { get; set; } = string.Empty;
        public string SelectedModel { get; set; } = string.Empty;

        // Context Fields for the AI
        public string TargetGrade { get; set; } = string.Empty;
        public string SupportNeeds { get; set; } = string.Empty;
    }

    public class ReportResponse
    {
        public string GeneratedReport { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class SessionRecord
    {
        public string StudentName { get; set; } = string.Empty;
        public string GeneratedReport { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }

        // Fix applied: Dynamically clears out old bracket indicators to keep the display clean
        public string DisplayText
        {
            get
            {
                if (string.IsNullOrWhiteSpace(StudentName)) return $"Report - {Timestamp:HH:mm}";
                string cleanName = Regex.Replace(StudentName, @"\s*[\(\[][^\]\)]+[\)\]]", "");
                return $"{cleanName} - {Timestamp:HH:mm}";
            }
        }
    }

    public class ReportFramework
    {
        public string Name { get; set; } = string.Empty;
        public string Instruction { get; set; } = string.Empty;
    }

    public class StudentProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FullName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string ParentEmail { get; set; } = string.Empty;

        // Expanded Database Fields
        public string TargetGrade { get; set; } = string.Empty;
        public string SupportNeeds { get; set; } = string.Empty;
    }

    public class AppSettings
    {
        public bool HasSeenTutorial { get; set; } = false;
        public bool IsDarkMode { get; set; } = false;
        public string SchoolName { get; set; } = "Enter School Name";
        public string TeacherSignoff { get; set; } = "Mr. / Ms. Teacher";
        public string AiProvider { get; set; } = "NVIDIA NIM (Free)";

        public string GeminiModelTier { get; set; } = "gemini-2.5-flash";
        public string OpenAiModelTier { get; set; } = "gpt-4o-mini";
        public string ClaudeModelTier { get; set; } = "claude-3-haiku-20240307";
        public string NvidiaModelTier { get; set; } = "meta/llama-3.1-405b-instruct";

        public string ThemeColorHex { get; set; } = "#FF392A4C";
        public string SchoolLogoPath { get; set; } = string.Empty;

        public string GeminiApiKey { get; set; } = string.Empty;
        public string OpenAiApiKey { get; set; } = string.Empty;
        public string ClaudeApiKey { get; set; } = string.Empty;
        public string NvidiaApiKey { get; set; } = string.Empty;
        public string MasterPassword { get; set; } = string.Empty;

        public string SmtpServer { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string SmtpEmail { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;

        public List<ReportFramework> CustomFrameworks { get; set; } = new List<ReportFramework>
        {
            new ReportFramework { Name = "Formal & Academic", Instruction = "Use highly formal, academic language." },
            new ReportFramework { Name = "Encouraging & Warm", Instruction = "Use a supportive, gentle tone." }
        };

        public List<string> CurriculumTopics { get; set; } = new List<string>
        {
            "Algebra & Geometry",
            "Creative Writing & Poetry",
            "World War II History",
            "Intro to Physics"
        };

        public int TotalReportsGenerated { get; set; } = 0;
        public long TotalTokensEstimated { get; set; } = 0;

        public int GeminiReportsCount { get; set; } = 0;
        public int OpenAiReportsCount { get; set; } = 0;
        public int ClaudeReportsCount { get; set; } = 0;
        public int NvidiaReportsCount { get; set; } = 0;
    }
}