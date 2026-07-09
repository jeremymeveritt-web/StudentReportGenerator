using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>Quote-aware CSV line splitting, shared by every CSV import path in the app
    /// (batch notes, roster, and school-data imports).</summary>
    public static class CsvUtil
    {
        private static readonly Regex Splitter = new(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");

        /// <summary>Splits one CSV line on commas, respecting double-quoted fields, and trims
        /// surrounding quotes/whitespace from each cell.</summary>
        public static string[] SplitLine(string line) =>
            Splitter.Split(line).Select(s => s.Trim('"', ' ')).ToArray();
    }

    /// <summary>
    /// Parses the app's documented school-data CSV format (the universal offline fallback from the
    /// School Data Integration Plan — any MIS can export it, including OneRoster-derived extracts).
    /// Header-driven: columns may appear in any order, unknown columns are ignored, and only
    /// <c>ExternalStudentId</c> is required per row. Recognised headers (case-insensitive):
    /// ExternalStudentId/UPN/StudentId/SourcedId, Name, Class, AttendancePercent/Attendance,
    /// BehaviourPoints/Behaviour, Grades ("Maths=6; Science=7"), SupportPlan, TargetGrade.
    /// Pure and file-system free so it is fully unit-testable.
    /// </summary>
    public static class SisCsvImportService
    {
        /// <summary>One parsed CSV row: the stats destined for the encrypted cache, plus the
        /// student name (if the export included one) used to auto-match roster profiles.</summary>
        public sealed class CsvRow
        {
            public StudentAcademicStats Stats { get; init; } = new();
            public string Name { get; init; } = string.Empty;
        }

        public sealed class CsvImportResult
        {
            public List<CsvRow> Rows { get; } = new();
            public List<string> Warnings { get; } = new();
        }

        // Header aliases → canonical column names
        private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["externalstudentid"] = "id",
            ["upn"] = "id",
            ["studentid"] = "id",
            ["sourcedid"] = "id",
            ["name"] = "name",
            ["fullname"] = "name",
            ["class"] = "class",
            ["attendancepercent"] = "attendance",
            ["attendance"] = "attendance",
            ["behaviourpoints"] = "behaviour",
            ["behaviour"] = "behaviour",
            ["behaviorpoints"] = "behaviour",
            ["grades"] = "grades",
            ["supportplan"] = "support",
            ["targetgrade"] = "target",
        };

        /// <summary>Parses the CSV lines. Never throws: malformed rows produce a warning and are
        /// skipped, malformed numeric cells produce a warning and leave that field unset.</summary>
        public static CsvImportResult Parse(IReadOnlyList<string> lines)
        {
            var result = new CsvImportResult();
            if (lines == null || lines.Count == 0)
            {
                result.Warnings.Add("The file is empty.");
                return result;
            }

            // Locate the header row (first non-blank line) and map recognised columns to indices
            int headerIndex = -1;
            var columns = new Dictionary<string, int>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cells = CsvUtil.SplitLine(lines[i]);
                for (int c = 0; c < cells.Length; c++)
                {
                    string key = cells[c].Replace(" ", "").Replace("_", "");
                    if (HeaderAliases.TryGetValue(key, out string? canonical) && !columns.ContainsKey(canonical))
                        columns[canonical] = c;
                }
                headerIndex = i;
                break;
            }

            if (!columns.ContainsKey("id"))
            {
                result.Warnings.Add("No 'ExternalStudentId' (or UPN / StudentId / SourcedId) column found in the header row — nothing can be imported without it.");
                return result;
            }

            for (int i = headerIndex + 1; i < lines.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                var cells = CsvUtil.SplitLine(lines[i]);

                string Cell(string canonical) =>
                    columns.TryGetValue(canonical, out int idx) && idx < cells.Length ? cells[idx].Trim() : string.Empty;

                string id = Cell("id");
                if (string.IsNullOrWhiteSpace(id))
                {
                    result.Warnings.Add($"Row {i + 1}: skipped (no student ID).");
                    continue;
                }

                var stats = new StudentAcademicStats { ExternalStudentId = id };

                string attendance = Cell("attendance");
                if (!string.IsNullOrEmpty(attendance))
                {
                    if (double.TryParse(attendance.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
                        stats.AttendancePercent = pct;
                    else
                        result.Warnings.Add($"Row {i + 1}: attendance '{attendance}' is not a number — left blank.");
                }

                string behaviour = Cell("behaviour");
                if (!string.IsNullOrEmpty(behaviour))
                {
                    if (int.TryParse(behaviour, NumberStyles.Integer, CultureInfo.InvariantCulture, out int points))
                        stats.BehaviourPoints = points;
                    else
                        result.Warnings.Add($"Row {i + 1}: behaviour points '{behaviour}' is not a whole number — left blank.");
                }

                stats.RecentGrades = ParseGrades(Cell("grades"));
                stats.SupportPlanSummary = Cell("support");
                stats.TargetGrade = Cell("target");

                result.Rows.Add(new CsvRow { Stats = stats, Name = Cell("name") });
            }

            if (result.Rows.Count == 0 && result.Warnings.Count == 0)
                result.Warnings.Add("No data rows found beneath the header.");

            return result;
        }

        /// <summary>Decodes the compact grades encoding "Maths=6; Science=7" into a dictionary.
        /// Entries without an '=' are ignored.</summary>
        public static Dictionary<string, string> ParseGrades(string encoded)
        {
            var grades = new Dictionary<string, string>();
            if (string.IsNullOrWhiteSpace(encoded)) return grades;

            foreach (var entry in encoded.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                int eq = entry.IndexOf('=');
                if (eq <= 0) continue;
                string subject = entry[..eq].Trim();
                string grade = entry[(eq + 1)..].Trim();
                if (subject.Length > 0 && grade.Length > 0) grades[subject] = grade;
            }
            return grades;
        }

        /// <summary>
        /// Auto-matches imported rows onto the teacher's roster: any profile whose
        /// <see cref="StudentProfile.FullName"/> equals a row's Name (case-insensitive) and whose
        /// <see cref="StudentProfile.ExternalStudentId"/> is still empty gets the row's ID.
        /// Profiles that already have an ID are never overwritten.
        /// </summary>
        /// <returns>The number of roster profiles that were newly matched.</returns>
        public static int MatchRoster(IReadOnlyList<CsvRow> rows, List<StudentProfile> roster)
        {
            int matched = 0;
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Name)) continue;
                var profile = roster.FirstOrDefault(p =>
                    string.IsNullOrWhiteSpace(p.ExternalStudentId) &&
                    p.FullName.Equals(row.Name, StringComparison.OrdinalIgnoreCase));
                if (profile != null)
                {
                    profile.ExternalStudentId = row.Stats.ExternalStudentId;
                    matched++;
                }
            }
            return matched;
        }
    }
}
