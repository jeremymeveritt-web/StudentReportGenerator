using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Persists the teacher's student roster (name, class, parent email, target grade, support
    /// needs, and — once a SIS connection exists — <see cref="StudentProfile.ExternalStudentId"/>)
    /// to an encrypted file at <c>%AppData%\FacultyFlow\students_db.dat</c>.
    /// </summary>
    /// <remarks>
    /// Follows the same pattern as <see cref="HistoryDatabaseService"/>: a one-time transparent
    /// migration from a legacy plaintext <c>students_db.json</c> file, then DPAPI encryption for
    /// everything going forward.
    /// </remarks>
    public static class StudentDatabaseService
    {
        private static readonly string FilePath = FileSandboxService.GetSafeFilePath("students_db.dat");
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(Environment.UserName + Environment.MachineName + "_Student_Secure_V2");

        /// <summary>Loads and decrypts the student roster. Returns an empty list (never null)
        /// if no roster exists yet or the file can't be read/decrypted.</summary>
        public static List<StudentProfile> LoadStudents()
        {
            if (File.Exists("students_db.json") && !File.Exists(FilePath))
            {
                MigratePlaintextDatabase();
            }

            if (!File.Exists(FilePath)) return new List<StudentProfile>();

            try
            {
                byte[] encryptedBytes = File.ReadAllBytes(FilePath);
                byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
                string json = Encoding.UTF8.GetString(decryptedBytes);
                return JsonSerializer.Deserialize<List<StudentProfile>>(json) ?? new List<StudentProfile>();
            }
            catch
            {
                return new List<StudentProfile>(); // Corrupted file or wrong Windows account — start fresh rather than crash
            }
        }

        /// <summary>Serializes and DPAPI-encrypts the full student roster to disk, overwriting the previous file.</summary>
        public static void SaveStudents(List<StudentProfile> students)
        {
            string json = JsonSerializer.Serialize(students);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, encryptedBytes);
        }

        /// <summary>One-time upgrade path: reads the old unencrypted roster file, re-saves it
        /// through the encrypted path, then deletes the plaintext copy. Failures are swallowed
        /// deliberately so a corrupt legacy file can never block the app from starting.</summary>
        private static void MigratePlaintextDatabase()
        {
            try
            {
                string oldJson = File.ReadAllText("students_db.json");
                var oldData = JsonSerializer.Deserialize<List<StudentProfile>>(oldJson) ?? new List<StudentProfile>();
                SaveStudents(oldData);
                File.Delete("students_db.json"); // Destroy the vulnerable plaintext file
            }
            catch
            {
                // Best-effort migration only — proceed with a fresh, empty roster rather than crash on startup.
            }
        }
    }
}