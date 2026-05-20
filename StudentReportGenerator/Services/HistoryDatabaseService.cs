using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public static class HistoryDatabaseService
    {
        private static readonly string DbFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "report_history_db.json");

        public static ObservableCollection<SessionRecord> LoadHistory()
        {
            if (!File.Exists(DbFilePath)) return new ObservableCollection<SessionRecord>();

            try
            {
                string json = File.ReadAllText(DbFilePath);
                var loadedList = JsonSerializer.Deserialize<ObservableCollection<SessionRecord>>(json);
                return loadedList ?? new ObservableCollection<SessionRecord>();
            }
            catch
            {
                return new ObservableCollection<SessionRecord>();
            }
        }

        public static void SaveHistory(ObservableCollection<SessionRecord> history)
        {
            // We limit history to the last 200 reports to prevent the JSON file from becoming massive
            var historyToSave = history;
            if (history.Count > 200)
            {
                historyToSave = new ObservableCollection<SessionRecord>(history.Take(200));
            }

            string json = JsonSerializer.Serialize(historyToSave, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DbFilePath, json);
        }
    }
}