using System;
using System.Threading.Tasks;
using Serilog;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Thin orchestrator parallel to <see cref="ReportOrchestratorService"/>: resolves the school's
    /// configured SIS provider, fetches verified stats for one student on demand (data minimisation —
    /// never a bulk roster mirror), falls back to the encrypted local cache when the SIS is
    /// unreachable, and writes an audit line for every access (a DPIA/compliance requirement — see
    /// the School Data Integration Plan, Section 5).
    /// </summary>
    public class SchoolDataOrchestratorService
    {
        private readonly AppStateService _appState;

        public SchoolDataOrchestratorService(AppStateService appState)
        {
            _appState = appState;
        }

        /// <summary>True once the school has chosen a real SIS provider in Settings (i.e. anything
        /// other than the zero-config "Manual Entry" default).</summary>
        public bool IsConnectionConfigured =>
            !string.IsNullOrWhiteSpace(_appState.CurrentSettings.SchoolDataProvider) &&
            _appState.CurrentSettings.SchoolDataProvider != "Manual Entry";

        /// <summary>
        /// Resolves the <see cref="ISchoolDatabaseService"/> for the school's configured provider.
        /// Future connectors (a Wonde implementation for UK schools, a OneRoster/Clever/ClassLink
        /// implementation for US districts) slot in here as additional switch cases, exactly like
        /// AI providers do in <see cref="AiServiceFactory"/>. Only <see cref="ManualEntrySchoolDatabaseService"/>
        /// exists today.
        /// </summary>
        private ISchoolDatabaseService ResolveProvider()
        {
            return _appState.CurrentSettings.SchoolDataProvider switch
            {
                _ => new ManualEntrySchoolDatabaseService(),
            };
        }

        /// <summary>
        /// Fetches verified academic stats for a student, in order of preference: (1) live from the
        /// configured SIS provider, caching the result on success; (2) the encrypted local cache if
        /// the SIS call fails or throws; (3) <c>null</c>, meaning the caller should fall back to the
        /// teacher's manual entry — this always remains a fully supported path, never just an outage fallback.
        /// </summary>
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
