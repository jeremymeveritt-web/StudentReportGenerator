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
    // Encrypted local cache of last-synced SIS stats, so reports still generate when the
    // school's SIS or aggregator is briefly unreachable. Same DPAPI pattern as the
    // settings/history services. Entries older than the configured retention window are
    // purged on load (data minimisation: never keep SIS data indefinitely).
    public static class SchoolDataCacheService
    {
        private static readonly string FilePath = FileSandboxService.GetSafeFilePath("sis_stats_cache.dat");
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(Environment.UserName + Environment.MachineName + "_SisCache_V1");

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

        public static void SaveCache(Dictionary<string, StudentAcademicStats> cache)
        {
            string json = JsonSerializer.Serialize(cache);
            byte[] encryptedBytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, encryptedBytes);
        }

        public static void UpsertStats(StudentAcademicStats stats, int retentionDays)
        {
            if (string.IsNullOrWhiteSpace(stats.ExternalStudentId)) return;
            var cache = LoadCache(retentionDays);
            cache[stats.ExternalStudentId] = stats;
            SaveCache(cache);
        }

        public static void PurgeAll()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
