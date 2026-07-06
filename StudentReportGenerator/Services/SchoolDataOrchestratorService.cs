using System;
using System.Threading.Tasks;
using Serilog;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    // Thin orchestrator parallel to ReportOrchestratorService: resolves the configured
    // SIS provider, fetches stats for one student on demand (data minimisation — never
    // a bulk roster mirror), falls back to the encrypted local cache when the SIS is
    // unreachable, and writes an audit line for every access.
    public class SchoolDataOrchestratorService
    {
        private readonly AppStateService _appState;

        public SchoolDataOrchestratorService(AppStateService appState)
        {
            _appState = appState;
        }

        public bool IsConnectionConfigured =>
            !string.IsNullOrWhiteSpace(_appState.CurrentSettings.SchoolDataProvider) &&
            _appState.CurrentSettings.SchoolDataProvider != "Manual Entry";

        // Future connectors (WondeSchoolDatabaseService, OneRosterSchoolDatabaseService, ...)
        // slot in here exactly like AI providers do in AiServiceFactory.
        private ISchoolDatabaseService ResolveProvider()
        {
            return _appState.CurrentSettings.SchoolDataProvider switch
            {
                _ => new ManualEntrySchoolDatabaseService(),
            };
        }

        public async Task<StudentAcademicStats?> GetStatsForStudentAsync(StudentProfile student)
        {
            if (!IsConnectionConfigured) return null;
            if (string.IsNullOrWhiteSpace(student.ExternalStudentId)) return null; // unmatched student → manual entry path

            int retention = _appState.CurrentSettings.SisCacheRetentionDays;
            var provider = ResolveProvider();

            try
            {
                var stats = await provider.GetStudentStatsAsync(student.ExternalStudentId);
                if (stats != null)
                {
                    stats.LastSyncedUtc = DateTime.UtcNow;
                    SchoolDataCacheService.UpsertStats(stats, retention);
                    Log.Information("SIS data accessed: provider={Provider} user={User} studentId={StudentId} source=live",
                        provider.ProviderName, Environment.UserName, student.ExternalStudentId);
                    return stats;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SIS provider {Provider} unreachable; falling back to cache", provider.ProviderName);
            }

            // Silent fallback to last-synced values — never block report generation on an outage
            var cache = SchoolDataCacheService.LoadCache(retention);
            if (cache.TryGetValue(student.ExternalStudentId, out var cached))
            {
                Log.Information("SIS data accessed: provider={Provider} user={User} studentId={StudentId} source=cache lastSynced={LastSynced}",
                    provider.ProviderName, Environment.UserName, student.ExternalStudentId, cached.LastSyncedUtc);
                return cached;
            }

            return null;
        }
    }
}
