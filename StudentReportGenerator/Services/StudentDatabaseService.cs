using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public static class StudentDatabaseService
    {
        private static readonly string FilePath = FileSandboxService.GetSafeFilePath("students_db.dat");
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(Environment.UserName + Environment.MachineName + "_Student_Secure_V2");

        public static List<StudentProfile> LoadStudents()
        {
            // Legacy plaintext migration check
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
                return new List<StudentProfile>(); // Return empty if corrupted or unauthorized
            }
        }

        public static void SaveStudents(List<StudentProfile> students)
        {
            string json = JsonSerializer.Serialize(students);
            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            byte[] encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, encryptedBytes);
        }

        private static void MigratePlaintextDatabase()
        {
            try
            {
                string oldJson = File.ReadAllText("students_db.json");
                var oldData = JsonSerializer.Deserialize<List<StudentProfile>>(oldJson) ?? new List<StudentProfile>();
                SaveStudents(oldData);
                File.Delete("students_db.json"); // Destroy the vulnerable plaintext file
            }
            catch { /* Ignore migration errors, proceed fresh */ }
        }
    }
}