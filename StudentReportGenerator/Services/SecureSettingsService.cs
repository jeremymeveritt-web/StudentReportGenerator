using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public static class SecureSettingsService
    {
        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.dat");

        // Generates a unique encryption key based on the specific Windows computer and User profile
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(Environment.UserName + Environment.MachineName);

        public static void SaveSettings(AppSettings settings)
        {
            string json = JsonSerializer.Serialize(settings);
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(json);

            // Encrypts the data bound to the current Windows user
            byte[] encryptedBytes = ProtectedData.Protect(plainTextBytes, Entropy, DataProtectionScope.CurrentUser);

            File.WriteAllBytes(SettingsFilePath, encryptedBytes);
        }

        public static AppSettings LoadSettings()
        {
            if (!File.Exists(SettingsFilePath))
            {
                // Fallback: Delete old plain text json if it exists for security
                string oldFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                if (File.Exists(oldFile)) File.Delete(oldFile);

                return new AppSettings();
            }

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(SettingsFilePath);
                byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);

                string json = Encoding.UTF8.GetString(decryptedBytes);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                // If encryption fails (e.g. copied to a new computer), start fresh
                return new AppSettings();
            }
        }
    }
}