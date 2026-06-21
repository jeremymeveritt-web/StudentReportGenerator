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
        private static readonly string SettingsPath = FileSandboxService.GetSafeFilePath("settings.dat");

        // Resolves Bug #3: Appends UserDomainName to form an enterprise-grade cryptographic validation anchor
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(Environment.UserName + Environment.UserDomainName + Environment.MachineName);

        public static void SaveSettings(AppSettings settings)
        {
            try
            {
                string json = JsonSerializer.Serialize(settings);
                byte[] plaintextBytes = Encoding.UTF8.GetBytes(json);
                byte[] encryptedBytes = ProtectedData.Protect(plaintextBytes, Entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(SettingsPath, encryptedBytes);
            }
            catch (Exception ex)
            {
                
                System.Windows.MessageBox.Show($"Failed to save settings to disk. Please ensure you have sufficient disk space and permissions. Error: {ex.Message}", "Save Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public static AppSettings LoadSettings()
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(SettingsPath);
                byte[] plaintextBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(plaintextBytes);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }
    }
}