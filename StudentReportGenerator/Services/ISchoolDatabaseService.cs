using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Deliberately mirrors the <see cref="IAiService"/> pattern: one small interface, with
    /// interchangeable provider implementations (a future Wonde connector for UK schools,
    /// OneRoster/Clever/ClassLink for US districts) resolved at runtime by
    /// <see cref="SchoolDataOrchestratorService"/>. Only <see cref="ManualEntrySchoolDatabaseService"/>
    /// is implemented today — see the School Data Integration Plan for the phased rollout of real
    /// SIS/MIS connectors, which require a signed Data Processing Agreement per school before going live.
    /// </summary>
    public interface ISchoolDatabaseService
    {
        /// <summary>Human-readable name shown in Settings (e.g. "Manual Entry", "Wonde (UK)").</summary>
        string ProviderName { get; }

        /// <summary>
        /// Fetches verified academic stats for one student, identified only by
        /// <paramref name="studentId"/> — the SIS's own stable pupil identifier (UPN in the UK,
        /// State Student ID in the US), never the app's internal <see cref="Models.StudentProfile.Id"/>
        /// GUID. Called on-demand per student at report-generation time; implementations must never
        /// bulk-mirror an entire roster (data minimisation).
        /// </summary>
        /// <returns>The student's stats, or <c>null</c> if the student cannot be matched in the SIS
        /// (e.g. a new starter not yet synced) — callers fall back to manual entry in that case.</returns>
        Task<StudentAcademicStats?> GetStudentStatsAsync(string studentId);
    }

    /// <summary>
    /// Null-object default provider: today's zero-configuration behaviour for any school without a
    /// SIS connection. Always returns <c>null</c>, so every field in a report stays teacher-entered —
    /// exactly as if no school-data integration existed at all.
    /// </summary>
    public class ManualEntrySchoolDatabaseService : ISchoolDatabaseService
    {
        public string ProviderName => "Manual Entry";

        public Task<StudentAcademicStats?> GetStudentStatsAsync(string studentId)
            => Task.FromResult<StudentAcademicStats?>(null);
    }
}
