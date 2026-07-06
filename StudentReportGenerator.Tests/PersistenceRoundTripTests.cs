using System.Collections.ObjectModel;
using System.IO;
using StudentReportGenerator.Models;
using StudentReportGenerator.Services;
using Xunit;

namespace StudentReportGenerator.Tests
{
    /// <summary>
    /// The persistence services write to fixed files under %AppData%\FacultyFlow, so these
    /// tests back up any real data files before running and restore them afterwards.
    /// </summary>
    public abstract class DataFileBackupFixture : IDisposable
    {
        private readonly string _path;
        private readonly byte[]? _originalContent;

        protected DataFileBackupFixture(string fileName)
        {
            _path = FileSandboxService.GetSafeFilePath(fileName);
            _originalContent = File.Exists(_path) ? File.ReadAllBytes(_path) : null;
        }

        public void Dispose()
        {
            if (_originalContent != null) File.WriteAllBytes(_path, _originalContent);
            else if (File.Exists(_path)) File.Delete(_path);
        }
    }

    public class SecureSettingsServiceTests : DataFileBackupFixture
    {
        public SecureSettingsServiceTests() : base("settings.dat") { }

        [Fact]
        public void SaveSettings_ThenLoad_RoundTrips()
        {
            var settings = new AppSettings
            {
                SchoolName = "Test Academy",
                TeacherSignoff = "Ms. Roundtrip",
                AiProvider = "Claude (Anthropic)",
                IsDarkMode = true,
                TotalReportsGenerated = 42
            };

            SecureSettingsService.SaveSettings(settings);
            var loaded = SecureSettingsService.LoadSettings();

            Assert.Equal("Test Academy", loaded.SchoolName);
            Assert.Equal("Ms. Roundtrip", loaded.TeacherSignoff);
            Assert.Equal("Claude (Anthropic)", loaded.AiProvider);
            Assert.True(loaded.IsDarkMode);
            Assert.Equal(42, loaded.TotalReportsGenerated);
        }

        [Fact]
        public void MasterPassword_SetSaveReloadVerify_Succeeds()
        {
            // End-to-end regression test for the Section 2 lockout bug: hash the master
            // password the way SaveProfileSettings now does, persist it, reload it, and
            // confirm UnlockSettings' VerifyPassword call would succeed.
            var settings = new AppSettings
            {
                MasterPassword = CryptoService.HashPassword("Unlock-Me-2026")
            };

            SecureSettingsService.SaveSettings(settings);
            var loaded = SecureSettingsService.LoadSettings();

            Assert.True(CryptoService.VerifyPassword("Unlock-Me-2026", loaded.MasterPassword));
            Assert.False(CryptoService.VerifyPassword("wrong-guess", loaded.MasterPassword));
        }
    }

    public class HistoryDatabaseServiceTests : DataFileBackupFixture
    {
        public HistoryDatabaseServiceTests() : base("report_history_db.dat") { }

        [Fact]
        public void SaveHistory_ThenLoad_RoundTrips()
        {
            var history = new ObservableCollection<SessionRecord>
            {
                new SessionRecord { StudentName = "Alice Example", GeneratedReport = "Alice did well.", Timestamp = new DateTime(2026, 7, 6, 9, 30, 0) },
                new SessionRecord { StudentName = "Bob Example", GeneratedReport = "Bob tried hard.", Timestamp = new DateTime(2026, 7, 6, 10, 0, 0) }
            };

            HistoryDatabaseService.SaveHistory(history);
            var loaded = HistoryDatabaseService.LoadHistory();

            Assert.Equal(2, loaded.Count);
            Assert.Equal("Alice Example", loaded[0].StudentName);
            Assert.Equal("Alice did well.", loaded[0].GeneratedReport);
            Assert.Equal(history[0].Id, loaded[0].Id);
            Assert.Equal("Bob Example", loaded[1].StudentName);
        }

        [Fact]
        public void SaveHistory_EncryptsFileOnDisk()
        {
            var history = new ObservableCollection<SessionRecord>
            {
                new SessionRecord { StudentName = "Sensitive Name", GeneratedReport = "Sensitive report body." }
            };

            HistoryDatabaseService.SaveHistory(history);
            string rawFileText = File.ReadAllText(FileSandboxService.GetSafeFilePath("report_history_db.dat"));

            Assert.DoesNotContain("Sensitive Name", rawFileText);
            Assert.DoesNotContain("Sensitive report body.", rawFileText);
        }
    }
}
