using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Encrypted local cache of last-synced SIS/MIS stats, keyed by external student ID, so reports
    /// still generate (using slightly stale data) when the school's SIS or aggregator is briefly
    /// unreachable. Uses the same DPAPI pattern as the settings/history services. Entries older
    /// than the school's configured retention window are purged on every load — a data-minimisation
    /// requirement from the School Data Integration Plan: never keep SIS data indefinitely.
    /// </summary>
    public static class SchoolDataCacheService
    {
        private static readonly string FilePath = FileSandboxService.GetSafeFilePath("sis_stats_cache.dat");
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(Environment.UserName + Environment.MachineName + "_SisCache_V1");

        /// <summary>Loads the cache, silently purging (and re-saving) any entries whose
        /// <see cref="StudentAcademicStats.LastSyncedUtc"/> is older than <paramref name="retentionDays"/>.</summary>
        public static Dictionary<string, StudentAcademicStats> LoadCache(int retentionDays)
        {
            if (!File.Exists(FilePath)) return new Dictionary<string, StudentAcademicStats>();

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(FilePath);
                byte[] plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
                var cache = JsonSerializer.Deserialize<Dictionary<string, StudentAcademicStats>>(Encoding.UTF8.GetString(plainBytes))
                            ?? new Dictionary<string, StudentAcademicStats>();

                var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, retentionDays));
                var expired = cache.Where(kv => kv.Value.LastSyncedUtc < cutoff).Select(kv => kv.Key).ToList();
                if (expired.Count > 0)
                {
                    foreach (var key in expired) cache.Remove(key);
                    SaveCache(cache);
                }

                return cache;
            }
            catch
            {
                return new Dictionary<string, StudentAcademicStats>();
            }
        }

        /// <summary>Serializes and DPAPI-encrypts the entire cache to disk, overwriting the previous file.</summary>
        public static void SaveCache(Dictionary<string, StudentAcademicStats> cache)
        {
            string json = JsonSerializer.Serialize(cache);
            byte[] encryptedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, encryptedBytes);
        }

        /// <summary>Inserts or replaces the cached stats for one student (keyed by
        /// <see cref="StudentAcademicStats.ExternalStudentId"/>) and persists immediately. No-op if
        /// the stats have no external ID to key on.</summary>
        public static void UpsertStats(StudentAcademicStats stats, int retentionDays)
        {
            if (string.IsNullOrWhiteSpace(stats.ExternalStudentId)) return;
            var cache = LoadCache(retentionDays);
            cache[stats.ExternalStudentId] = stats;
            SaveCache(cache);
        }

        /// <summary>Deletes the entire cache file. Used by the "Purge Cached Data" settings action
        /// so a school's data lead can wipe locally cached SIS data on demand.</summary>
        public static void PurgeAll()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
