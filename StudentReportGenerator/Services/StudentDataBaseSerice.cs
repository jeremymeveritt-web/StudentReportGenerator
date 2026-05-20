using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public static class StudentDatabaseService
    {
        private static readonly string DbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "students_db.json");

        public static List<StudentProfile> LoadStudents()
        {
            if (!File.Exists(DbFilePath)) return new List<StudentProfile>();

            try
            {
                string json = File.ReadAllText(DbFilePath);
                return JsonSerializer.Deserialize<List<StudentProfile>>(json) ?? new List<StudentProfile>();
            }
            catch
            {
                return new List<StudentProfile>();
            }
        }

        public static void SaveStudents(List<StudentProfile> students)
        {
            string json = JsonSerializer.Serialize(students, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DbFilePath, json);
        }
    }
}