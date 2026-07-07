using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Persists <see cref="AppSettings"/> (API keys, SMTP credentials, master password hash, theme,
    /// usage counters, etc.) to a single encrypted file: <c>%AppData%\FacultyFlow\settings.dat</c>.
    /// The whole settings object is serialized to JSON and then DPAPI-encrypted as one blob — simpler
    /// than field-level encryption, but it does mean the file is only readable by whichever Windows
    /// user account originally saved it (see <see cref="Entropy"/>).
    /// </summary>
    public static class SecureSettingsService
    {
        private static readonly string SettingsPath = FileSandboxService.GetSafeFilePath("settings.dat");

        /// <summary>
        /// DPAPI entropy derived from the current user, domain, and machine name. Binding entropy to
        /// all three (rather than just the username) ties decryption to this specific Windows
        /// installation, so the file cannot be silently reused if profiles are cloned across machines.
        /// </summary>
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(Environment.UserName + Environment.UserDomainName + Environment.MachineName);

        /// <summary>Serializes and DPAPI-encrypts <paramref name="settings"/> to disk. Shows a
        /// message box (rather than throwing) on failure, since this is typically called from
        /// UI-triggered save actions where the teacher needs to know immediately if it didn't work.</summary>
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
                System.Windows.MessageBox.Show(
                    $"Settings could not be saved. Check you have write access to the application folder.\nError: {ex.Message}",
                    "Save Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>Loads and decrypts settings from disk. Returns a fresh default <see cref="AppSettings"/>
        /// (never null, never throws) when the file doesn't exist yet or can't be decrypted/parsed —
        /// e.g. first run, or the file was copied from a different machine/user account.</summary>
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