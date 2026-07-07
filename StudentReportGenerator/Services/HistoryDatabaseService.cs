using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Persists the report history log (every generated report, used for the History panel,
    /// re-editing, and the "previous term's report" continuity feature) to an encrypted file at
    /// <c>%AppData%\FacultyFlow\report_history_db.dat</c>.
    /// </summary>
    /// <remarks>
    /// On first load after an upgrade, transparently migrates any legacy plaintext
    /// <c>report_history_db.json</c> found in the working directory into the new encrypted
    /// location and deletes the plaintext original. This runs once per machine; after migration
    /// the legacy file no longer exists, so the check is a cheap no-op on every subsequent launch.
    /// </remarks>
    public static class HistoryDatabaseService
    {
        private static readonly string FilePath = FileSandboxService.GetSafeFilePath("report_history_db.dat");
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(Environment.UserName + Environment.MachineName + "_History_Secure_V2");

        /// <summary>Loads and decrypts the history log. Returns an empty collection (never null)
        /// if no history exists yet or the file can't be read/decrypted.</summary>
        public static ObservableCollection<SessionRecord> LoadHistory()
        {
            if (File.Exists("report_history_db.json") && !File.Exists(FilePath))
            {
                MigratePlaintextDatabase();
            }

            if (!File.Exists(FilePath)) return new ObservableCollection<SessionRecord>();

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(FilePath);
                byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(decryptedBytes);
                return JsonSerializer.Deserialize<ObservableCollection<SessionRecord>>(json) ?? new ObservableCollection<SessionRecord>();
            }
            catch
            {
                return new ObservableCollection<SessionRecord>();
            }
        }

        /// <summary>Serializes and DPAPI-encrypts the full history log to disk, overwriting the previous file.</summary>
        public static void SaveHistory(ObservableCollection<SessionRecord> history)
        {
            string json = JsonSerializer.Serialize(history);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, encryptedBytes);
        }

        /// <summary>One-time upgrade path: reads the old unencrypted history file, re-saves it
        /// through the encrypted path, then deletes the plaintext copy. Failures are swallowed
        /// deliberately — if migration can't complete, the app simply starts with empty history
        /// rather than crashing on launch.</summary>
        private static void MigratePlaintextDatabase()
        {
            try
            {
                string oldJson = File.ReadAllText("report_history_db.json");
                var oldData = JsonSerializer.Deserialize<ObservableCollection<SessionRecord>>(oldJson) ?? new ObservableCollection<SessionRecord>();
                SaveHistory(oldData);
                File.Delete("report_history_db.json"); // Destroy the plaintext copy now that it's safely encrypted
            }
            catch
            {
                // Best-effort migration only — an empty history is preferable to a crash on startup.
            }
        }
    }
}