using System;
using System.IO;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Centralises every path the app writes to, all under a single per-user, per-machine
    /// folder: <c>%AppData%\FacultyFlow</c>. All persistence services (settings, history,
    /// student roster, SIS cache, logs, comment bank) route through <see cref="GetSafeFilePath"/>
    /// so there is exactly one place that decides where app data lives on disk.
    /// </summary>
    public static class FileSandboxService
    {
        /// <summary>Resolves to %AppData%\FacultyFlow, e.g. C:\Users\{name}\AppData\Roaming\FacultyFlow.</summary>
        private static readonly string _appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FacultyFlow");

        /// <summary>Creates the FacultyFlow AppData folder if it doesn't already exist. Idempotent.</summary>
        public static void EnsureSandboxExists()
        {
            if (!Directory.Exists(_appDataFolder))
            {
                Directory.CreateDirectory(_appDataFolder);
            }
        }

        /// <summary>Returns the full path for a named file inside the app's sandbox, creating the folder on demand.</summary>
        public static string GetSafeFilePath(string fileName)
        {
            EnsureSandboxExists();
            return Path.Combine(_appDataFolder, fileName);
        }

        /// <summary>
        /// Copies a user-picked asset (e.g. a school logo) into the sandbox so the app doesn't
        /// depend on a file the user might later move, rename, or delete from its original location.
        /// </summary>
        /// <returns>The new sandboxed path on success, or the original path if the copy failed
        /// (e.g. the source file is locked) so the caller can still use *something*.</returns>
        public static string CloneAssetToSandbox(string originalFilePath, string newFileName)
        {
            EnsureSandboxExists();
            string safePath = Path.Combine(_appDataFolder, newFileName);

            try
            {
                File.Copy(originalFilePath, safePath, overwrite: true);
                return safePath;
            }
            catch
            {
                return originalFilePath; // Fallback to original if locked
            }
        }
    }
}