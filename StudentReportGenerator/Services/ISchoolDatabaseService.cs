using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    // Mirrors the IAiService pattern: one small interface, interchangeable provider
    // implementations (Wonde for the UK, OneRoster/Clever/ClassLink for the US),
    // selected at runtime by SchoolDataOrchestratorService.
    public interface ISchoolDatabaseService
    {
        string ProviderName { get; }

        // studentId is the SIS's own stable pupil identifier (UPN / State Student ID),
        // never the app's internal Guid. Returns null when the student cannot be matched.
        Task<StudentAcademicStats?> GetStudentStatsAsync(string studentId);
    }

    // Null-object default: today's behaviour, kept as the zero-config option for schools
    // with no SIS connection. Every field stays teacher-entered.
    public class ManualEntrySchoolDatabaseService : ISchoolDatabaseService
    {
        public string ProviderName => "Manual Entry";

        public Task<StudentAcademicStats?> GetStudentStatsAsync(string studentId)
            => Task.FromResult<StudentAcademicStats?>(null);
    }
}
