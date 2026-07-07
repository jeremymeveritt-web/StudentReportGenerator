using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Single source of truth for application-wide state, registered as a DI singleton
    /// (see App.xaml.cs) so every ViewModel shares the exact same <see cref="AppSettings"/>
    /// instance in memory. This is the only class that should read from or write to
    /// <see cref="SecureSettingsService"/> — ViewModels mutate <see cref="CurrentSettings"/>
    /// directly and then call <see cref="SaveSettings"/> to persist.
    /// </summary>
    public class AppStateService
    {
        public AppSettings CurrentSettings { get; private set; }

        public AppStateService()
        {
            // Load settings from disk exactly once, at app startup.
            CurrentSettings = SecureSettingsService.LoadSettings() ?? new AppSettings();
        }

        /// <summary>Persists the current in-memory settings to disk. Call after any mutation of <see cref="CurrentSettings"/>.</summary>
        public void SaveSettings()
        {
            SecureSettingsService.SaveSettings(CurrentSettings);
        }

        /// <summary>Discards in-memory settings and re-reads from disk. Currently unused by the UI but
        /// available for scenarios like reacting to an external settings file change.</summary>
        public void ReloadSettings()
        {
            CurrentSettings = SecureSettingsService.LoadSettings() ?? new AppSettings();
        }
    }
}