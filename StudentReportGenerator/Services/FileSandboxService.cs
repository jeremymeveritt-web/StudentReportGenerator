using System;
using System.IO;

namespace StudentReportGenerator.Services
{
    public static class FileSandboxService
    {
        // Points to: C:\Users\[Name]\AppData\Roaming\FacultyFlow
        private static readonly string _appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FacultyFlow");

        public static void EnsureSandboxExists()
        {
            if (!Directory.Exists(_appDataFolder))
            {
                Directory.CreateDirectory(_appDataFolder);
            }
        }

        // Gets the safe path for databases and settings
        public static string GetSafeFilePath(string fileName)
        {
            EnsureSandboxExists();
            return Path.Combine(_appDataFolder, fileName);
        }

        // Safely clones uploaded images into the sandbox so they aren't lost if the user deletes the original
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