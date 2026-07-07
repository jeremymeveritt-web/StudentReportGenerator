using System;
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
        public static string BuildSecurePrompt(ReportRequest request)
        {
            // Utility mode: simplify/translate/tone-audit calls bypass the report framing
            // entirely and run the given instruction against quarantined content.
            if (!string.IsNullOrWhiteSpace(request.UtilityInstruction))
            {
                var usb = new StringBuilder();
                usb.AppendLine(request.UtilityInstruction);
                usb.AppendLine("\n<content>");
                usb.AppendLine(request.RawNotes);
                usb.AppendLine("</content>");
                return usb.ToString();
            }

            var sb = new StringBuilder();

            // 1. SYSTEM CONTEXT (Absolute Rules)
            sb.AppendLine("You are an expert, professional educational assistant. Your ONLY job is to write a student performance report.");
            sb.AppendLine($"Student Pronouns: {request.Pronouns}. CRITICAL: You must use these exact pronouns when referring to the student.");
            sb.AppendLine($"Subject / Curriculum Topic: {request.Subject}");


            // 2. THE CHOSEN FRAMEWORK
            if (!string.IsNullOrWhiteSpace(request.SelectedFramework))
            {
                sb.AppendLine($"\nTONE AND STYLE FRAMEWORK: {request.SelectedFramework}");
            }

            // 3. HARD CONSTRAINTS
            sb.AppendLine($"TARGET WORD COUNT: Approximately {request.WordCount} words.");
            sb.AppendLine("DO NOT include any placeholder text like [Insert Name].");
            // Guard against every child's report sounding identical when a whole school
            // shares the same frameworks and providers
            sb.AppendLine("Vary your sentence structure and opening phrases naturally; do not reuse stock phrasing from typical AI-written reports.");

            if (!string.IsNullOrWhiteSpace(request.TeacherSignoff) && request.TeacherSignoff != "Mr. / Ms. Teacher")
            {
                sb.AppendLine($"Sign off the report cleanly with: {request.TeacherSignoff}");
            }

            // 4. THE QUARANTINED DATA (XML Wrapped)
            sb.AppendLine("\n<student_data>");
            sb.AppendLine($"Name: {request.StudentName}");
            sb.AppendLine($"Subject: {request.Subject}");

            if (!string.IsNullOrWhiteSpace(request.TargetGrade))
                sb.AppendLine($"Target Grade: {request.TargetGrade}");

            if (!string.IsNullOrWhiteSpace(request.SupportNeeds))
                sb.AppendLine($"Special Educational Needs / Support: {request.SupportNeeds}");

            // SIS-grounded facts (only present when the school has opted the category in)
            if (request.AttendancePercent.HasValue)
                sb.AppendLine($"Verified Attendance This Term: {request.AttendancePercent.Value:0.#}%");

            if (request.BehaviourPoints.HasValue)
                sb.AppendLine($"Verified Behaviour Points This Term: {request.BehaviourPoints.Value}");

            if (!string.IsNullOrWhiteSpace(request.RecentGradesSummary))
                sb.AppendLine($"Verified Recent Assessment Grades: {request.RecentGradesSummary}");

            sb.AppendLine($"\nTeacher Notes & Performance Data:\n{request.RawNotes}");
            sb.AppendLine("</student_data>");

            // 5. FINAL TRIGGER
            sb.AppendLine("\nBegin generating the professional report now:");

            return sb.ToString();
        }
    }
}