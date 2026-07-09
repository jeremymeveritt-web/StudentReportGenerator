using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
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
        /// <summary>Name of the named <see cref="HttpClient"/> registered via <c>IHttpClientFactory</c>
        /// in App.xaml.cs, used by live SIS connectors (currently Wonde).</summary>
        public const string SchoolDataHttpClientName = "SchoolData";

        private readonly AppStateService _appState;
        private readonly IHttpClientFactory _httpClientFactory;

        public SchoolDataOrchestratorService(AppStateService appState, IHttpClientFactory httpClientFactory)
        {
            _appState = appState;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>True once the school has chosen a real SIS provider in Settings (i.e. anything
        /// other than the zero-config "Manual Entry" default).</summary>
        public bool IsConnectionConfigured =>
            !string.IsNullOrWhiteSpace(_appState.CurrentSettings.SchoolDataProvider) &&
            _appState.CurrentSettings.SchoolDataProvider != "Manual Entry";

        /// <summary>
        /// Resolves the <see cref="ISchoolDatabaseService"/> for the school's configured provider,
        /// exactly like AI providers resolve in <see cref="AiServiceFactory"/>: Wonde for UK schools
        /// (live REST, credentials from Settings), the CSV import connector as the universal offline
        /// path, and the manual-entry null object otherwise.
        /// </summary>
        private ISchoolDatabaseService ResolveProvider()
        {
            var settings = _appState.CurrentSettings;
            return settings.SchoolDataProvider switch
            {
                var p when p != null && p.Contains("Wonde") => new WondeSchoolDatabaseService(
                    _httpClientFactory.CreateClient(SchoolDataHttpClientName),
                    CryptoService.DecryptSecret(settings.WondeApiToken),
                    settings.WondeSchoolId),
                var p when p != null && (p.Contains("OneRoster") || p.Contains("CSV")) =>
                    new OneRosterCsvSchoolDatabaseService(settings.SisCacheRetentionDays),
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
                    // Preserve the original sync time on cache-backed providers (CSV import): a
                    // read must not refresh retention, or imported data would never expire.
                    if (stats.LastSyncedUtc == default) stats.LastSyncedUtc = DateTime.UtcNow;
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

        /// <summary>
        /// "Sync Now" for live connectors: refreshes cached stats for the given roster students only
        /// (those with a matched <see cref="StudentProfile.ExternalStudentId"/>) — never a
        /// whole-school mirror, per the data-minimisation stance. Failures on individual students
        /// are counted, not fatal, so one bad record can't abort a class-wide refresh.
        /// </summary>
        public async Task<(int Refreshed, int Failed)> RefreshAllAsync(
            IReadOnlyList<StudentProfile> studentsWithIds,
            IProgress<(int Done, int Total)>? progress,
            CancellationToken ct = default)
        {
            int refreshed = 0, failed = 0, done = 0;
            foreach (var student in studentsWithIds)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var stats = await GetStatsForStudentAsync(student);
                    if (stats != null) refreshed++; else failed++;
                }
                catch
                {
                    failed++;
                }
                progress?.Report((++done, studentsWithIds.Count));
                if (done < studentsWithIds.Count) await Task.Delay(250, ct); // gentle API pacing
            }
            return (refreshed, failed);
        }
    }
}
