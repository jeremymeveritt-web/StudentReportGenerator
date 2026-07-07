using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StudentReportGenerator.Models
{
    /// <summary>
    /// Everything an <see cref="Services.IAiService"/> provider needs to generate one report (or,
    /// when <see cref="UtilityInstruction"/> is set, to perform a one-off utility call such as
    /// simplify/translate/tone-audit). Built by <c>MainViewModel</c> and consumed by
    /// <see cref="Services.PromptBuilderService"/>.
    /// </summary>
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
        public string TargetGrade { get; set; } = string.Empty;
        public string SupportNeeds { get; set; } = string.Empty;
        public string Pronouns { get; set; } = "They/Them";

        // SIS-grounded facts (populated from the school data orchestrator when a connection exists)
        public double? AttendancePercent { get; set; }
        public int? BehaviourPoints { get; set; }
        public string RecentGradesSummary { get; set; } = string.Empty;

        // When set, the prompt builder ignores the report framing entirely and issues this
        // instruction against RawNotes instead (used for simplify/translate/tone-audit calls)
        public string UtilityInstruction { get; set; } = string.Empty;
    }

    /// <summary>Verified facts pulled from the school's SIS/MIS (or its local encrypted cache via
    /// <see cref="Services.SchoolDataCacheService"/>), keyed by <see cref="ExternalStudentId"/>.</summary>
    public class StudentAcademicStats
    {
        public string ExternalStudentId { get; set; } = string.Empty;
        public double? AttendancePercent { get; set; }
        public int? BehaviourPoints { get; set; }
        public Dictionary<string, string> RecentGrades { get; set; } = new Dictionary<string, string>();
        public string SupportPlanSummary { get; set; } = string.Empty;
        public string TargetGrade { get; set; } = string.Empty;
        public DateTime LastSyncedUtc { get; set; }
    }

    /// <summary>The outcome of an AI generation call. Exactly one of <see cref="GeneratedReport"/>
    /// (on success) or <see cref="ErrorMessage"/> (on failure) is meaningful, indicated by <see cref="IsSuccess"/>.</summary>
    public class ReportResponse
    {
        private string _generatedReport = string.Empty;
        public string GeneratedReport
        {
            get => _generatedReport;
            set => _generatedReport = value ?? string.Empty;
        }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    /// <summary>One entry in the History Log: a generated report and its metadata, persisted via
    /// <see cref="Services.HistoryDatabaseService"/>.</summary>
    public class SessionRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        private string _generatedReport = string.Empty;
        public string StudentName { get; set; } = string.Empty;

        // The untouched AI draft as first generated, so a teacher can always get back to it
        public string OriginalDraft { get; set; } = string.Empty;

        public string GeneratedReport
        {
            get => _generatedReport;
            set => _generatedReport = value ?? string.Empty;
        }
        public DateTime Timestamp { get; set; }

        /// <summary>Label shown in the History Log ListBox: student name (with any trailing
        /// parenthetical/bracketed annotation stripped, e.g. "Tom (Yr 9)" → "Tom") plus the time generated.</summary>
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

    /// <summary>School letterhead details (logo, name, accent colour) applied to Word/PDF exports
    /// so reports look like they belong to the school, not to a generic app. See <see cref="Services.WordExportService"/>
    /// and <see cref="Services.PdfExportService"/>.</summary>
    public class SchoolBranding
    {
        public string SchoolName { get; set; } = string.Empty;
        public string LogoPath { get; set; } = string.Empty;
        public string AccentColorHex { get; set; } = "#FF392A4C";
    }

    /// <summary>A named tone/style template (e.g. "Formal &amp; Academic") that supplies the
    /// <see cref="ReportRequest.SelectedFramework"/> instruction fed into the AI prompt.</summary>
    public class ReportFramework
    {
        public string Name { get; set; } = string.Empty;
        public string Instruction { get; set; } = string.Empty;
    }

    /// <summary>A single student's roster entry: contact/context details a teacher maintains once
    /// and reuses across every report for that student. Persisted via <see cref="Services.StudentDatabaseService"/>.</summary>
    public class StudentProfile
    {
        /// <summary>Internal app identifier — a random GUID, unrelated to any school system.
        /// Use <see cref="ExternalStudentId"/> when matching against a SIS/MIS.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>The SIS's own stable pupil identifier (UPN in the UK, State Student ID in the US).
        /// Empty until a school data connection is configured; becomes the matching key once one is,
        /// so that a student who changes their preferred name (or shares a name with another student)
        /// is never confused with someone else.</summary>
        public string ExternalStudentId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string ParentEmail { get; set; } = string.Empty;
        public string TargetGrade { get; set; } = string.Empty;
        public string SupportNeeds { get; set; } = string.Empty;
        public string Pronouns { get; set; } = "They/Them";
    }

    /// <summary>
    /// The single settings object persisted (encrypted) via <see cref="Services.SecureSettingsService"/>
    /// and shared by every ViewModel through <see cref="Services.AppStateService.CurrentSettings"/>.
    /// Covers branding, AI provider configuration, security, school-data integration, accessibility,
    /// and running usage totals — effectively the entire configuration surface of the app in one class.
    /// </summary>
    public class AppSettings
    {
        public bool HasSeenTutorial { get; set; } = false;
        public bool IsDarkMode { get; set; } = false;
        public string SchoolName { get; set; } = "Enter School Name";
        public string TeacherSignoff { get; set; } = "Mr. / Ms. Teacher";
        public string AiProvider { get; set; } = "NVIDIA NIM (Free)";
        public string GeminiModelTier { get; set; } = "gemini-2.5-flash";
        public string OpenAiModelTier { get; set; } = "gpt-4o-mini";
        public string ClaudeModelTier { get; set; } = "claude-haiku-4-5-20251001";
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

        // --- School data (SIS/MIS) integration ---
        public string SchoolDataProvider { get; set; } = "Manual Entry";
        // Per-category opt-in: what a school's DPO allows to leave the building and reach an AI provider
        public bool IncludeAttendanceInPrompts { get; set; } = false;
        public bool IncludeBehaviourInPrompts { get; set; } = false;
        public bool IncludeGradesInPrompts { get; set; } = false;
        public bool IncludeSupportPlanInPrompts { get; set; } = false;
        public int SisCacheRetentionDays { get; set; } = 120;
        public DateTime? LastSisSyncUtc { get; set; }

        // --- Trust & disclosure ---
        public bool AppendAiDisclosure { get; set; } = true;
        public bool EnableSafeguardingPrompt { get; set; } = true;

        // --- Accessibility & interface ---
        public bool DyslexiaFriendlyFont { get; set; } = false;
        public double UiTextScale { get; set; } = 1.0;
        public bool SimpleMode { get; set; } = false;

        public int TotalReportsGenerated { get; set; } = 0;
        public long TotalTokensEstimated { get; set; } = 0;
        public int GeminiReportsCount { get; set; } = 0;
        public int OpenAiReportsCount { get; set; } = 0;
        public int ClaudeReportsCount { get; set; } = 0;
        public int NvidiaReportsCount { get; set; } = 0;
    }
}