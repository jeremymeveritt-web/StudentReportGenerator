using System.Threading.Tasks;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// The universal offline connector: schools export a CSV from their MIS/SIS (a OneRoster
    /// extract or the app's documented flat format — see <see cref="SisCsvImportService"/>), the
    /// data lead imports it via "Sync Now" in Settings, and rows land in the encrypted local cache.
    /// This provider then serves each student's stats straight from that cache, so report
    /// generation works identically to a live connector but with zero network dependency —
    /// exactly the "CSV import as universal fallback" path the Integration Plan keeps first-class.
    /// </summary>
    public class OneRosterCsvSchoolDatabaseService : ISchoolDatabaseService
    {
        private readonly int _retentionDays;

        public OneRosterCsvSchoolDatabaseService(int retentionDays)
        {
            _retentionDays = retentionDays;
        }

        public string ProviderName => "OneRoster / CSV import";

        public Task<StudentAcademicStats?> GetStudentStatsAsync(string studentId)
        {
            var cache = SchoolDataCacheService.LoadCache(_retentionDays);
            return Task.FromResult(cache.TryGetValue(studentId, out var stats) ? stats : null);
        }
    }
}
