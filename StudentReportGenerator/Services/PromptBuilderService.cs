using System;
using System.Linq;
using System.Text;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Assembles the final prompt text sent to whichever AI provider is active. Centralising prompt
    /// construction here (rather than in each provider class) means every provider gets identical
    /// instructions, the same "quarantined" data wrapping (see below), and the same safety rules,
    /// regardless of which of the four APIs ultimately receives it.
    /// </summary>
    public static class PromptBuilderService
    {
        /// <summary>Maximum number of comment-bank phrases injected as style exemplars — enough to
        /// establish a voice without bloating every prompt with the teacher's whole bank.</summary>
        public const int MaxStyleExemplars = 8;

        /// <summary>
        /// The prompt split into the two roles modern chat APIs expect: durable instructions
        /// (system) and the per-student data (user). Keeping instructions in the system role makes
        /// providers follow them more reliably and further separates them from the quarantined
        /// free-text teacher notes — a strengthening of the existing prompt-injection mitigation.
        /// </summary>
        public readonly record struct PromptParts(string SystemInstructions, string UserContent);

        /// <summary>
        /// Builds either a full report-generation prompt or, when <see cref="ReportRequest.UtilityInstruction"/>
        /// is set, a one-off utility prompt (used for "Simplify for Parents", "Translate", and the
        /// tone-balance audit) that bypasses all report framing entirely.
        /// </summary>
        /// <remarks>
        /// Student-identifying data (name, subject, grade, support needs, teacher notes, and any
        /// SIS-verified facts) is wrapped in an <c>&lt;student_data&gt;</c> tag. This is a prompt-injection
        /// mitigation: instructions embedded in free-text teacher notes are less likely to be
        /// interpreted as commands by the model when clearly demarcated as data rather than instructions.
        /// </remarks>
        public static PromptParts BuildPromptParts(ReportRequest request)
        {
            // Utility mode: simplify/translate/tone-audit calls bypass the report framing
            // entirely and run the given instruction against quarantined content.
            if (!string.IsNullOrWhiteSpace(request.UtilityInstruction))
            {
                var usb = new StringBuilder();
                usb.AppendLine("\n<content>");
                usb.AppendLine(request.RawNotes);
                usb.AppendLine("</content>");
                return new PromptParts(request.UtilityInstruction, usb.ToString());
            }

            // 1. SYSTEM CONTEXT (Absolute Rules)
            var sys = new StringBuilder();
            sys.AppendLine("You are an expert, professional educational assistant. Your ONLY job is to write a student performance report.");
            sys.AppendLine($"Student Pronouns: {request.Pronouns}. CRITICAL: You must use these exact pronouns when referring to the student.");
            sys.AppendLine($"Subject / Curriculum Topic: {request.Subject}");


            // 2. THE CHOSEN FRAMEWORK
            if (!string.IsNullOrWhiteSpace(request.SelectedFramework))
            {
                sys.AppendLine($"\nTONE AND STYLE FRAMEWORK: {request.SelectedFramework}");
            }

            // 3. HARD CONSTRAINTS
            sys.AppendLine($"TARGET WORD COUNT: Approximately {request.WordCount} words.");
            sys.AppendLine("DO NOT include any placeholder text like [Insert Name].");
            // Guard against every child's report sounding identical when a whole school
            // shares the same frameworks and providers
            sys.AppendLine("Vary your sentence structure and opening phrases naturally; do not reuse stock phrasing from typical AI-written reports.");

            if (!string.IsNullOrWhiteSpace(request.TeacherSignoff) && request.TeacherSignoff != "Mr. / Ms. Teacher")
            {
                sys.AppendLine($"Sign off the report cleanly with: {request.TeacherSignoff}");
            }

            // 3b. STYLE EXEMPLARS — samples of the teacher's own voice from their comment bank,
            // capped so a large bank can't crowd out the actual instructions.
            var exemplars = request.StyleExemplars?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Take(MaxStyleExemplars)
                .ToList();
            if (exemplars is { Count: > 0 })
            {
                sys.AppendLine("\nSTYLE EXEMPLARS — phrases written in this teacher's own voice. Match their style, vocabulary and warmth; never copy any phrase verbatim:");
                foreach (var phrase in exemplars) sys.AppendLine($"- {phrase}");
            }

            // 4. THE QUARANTINED DATA (XML Wrapped)
            var usr = new StringBuilder();
            usr.AppendLine("\n<student_data>");
            usr.AppendLine($"Name: {request.StudentName}");
            usr.AppendLine($"Subject: {request.Subject}");

            if (!string.IsNullOrWhiteSpace(request.TargetGrade))
                usr.AppendLine($"Target Grade: {request.TargetGrade}");

            if (!string.IsNullOrWhiteSpace(request.SupportNeeds))
                usr.AppendLine($"Special Educational Needs / Support: {request.SupportNeeds}");

            // SIS-grounded facts (only present when the school has opted the category in)
            if (request.AttendancePercent.HasValue)
                usr.AppendLine($"Verified Attendance This Term: {request.AttendancePercent.Value:0.#}%");

            if (request.BehaviourPoints.HasValue)
                usr.AppendLine($"Verified Behaviour Points This Term: {request.BehaviourPoints.Value}");

            if (!string.IsNullOrWhiteSpace(request.RecentGradesSummary))
                usr.AppendLine($"Verified Recent Assessment Grades: {request.RecentGradesSummary}");

            usr.AppendLine($"\nTeacher Notes & Performance Data:\n{request.RawNotes}");
            usr.AppendLine("</student_data>");

            // 5. FINAL TRIGGER
            usr.AppendLine("\nBegin generating the professional report now:");

            return new PromptParts(sys.ToString(), usr.ToString());
        }

        /// <summary>Single-string form of <see cref="BuildPromptParts"/> (system + user concatenated),
        /// kept for callers and providers that don't use a separate system role.</summary>
        public static string BuildSecurePrompt(ReportRequest request)
        {
            var parts = BuildPromptParts(request);
            return parts.SystemInstructions + parts.UserContent;
        }
    }
}
