using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public static class HistoryDatabaseService
    {
        private static readonly string FilePath = FileSandboxService.GetSafeFilePath("report_history_db.dat");
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(Environment.UserName + Environment.MachineName + "_History_Secure_V2");

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

        public static void SaveHistory(ObservableCollection<SessionRecord> history)
        {
            string json = JsonSerializer.Serialize(history);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, encryptedBytes);
        }

        private static void MigratePlaintextDatabase()
        {
            try
            {
                string oldJson = File.ReadAllText("report_history_db.json");
                var oldData = JsonSerializer.Deserialize<ObservableCollection<SessionRecord>>(oldJson) ?? new ObservableCollection<SessionRecord>();
                SaveHistory(oldData);
                File.Delete("report_history_db.json"); // Destroy plaintext
            }
            catch { }
        }
    }
}