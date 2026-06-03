using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    // This class will act as the single source of truth for the entire application's state
    public class AppStateService
    {
        public AppSettings CurrentSettings { get; private set; }

        public AppStateService()
        {
            // Load the settings from disk exactly once when the app boots
            CurrentSettings = SecureSettingsService.LoadSettings() ?? new AppSettings();
        }

        public void SaveSettings()
        {
            // Any ViewModel can call this to save the global state to disk safely
            SecureSettingsService.SaveSettings(CurrentSettings);
        }

        public void ReloadSettings()
        {
            CurrentSettings = SecureSettingsService.LoadSettings() ?? new AppSettings();
        }
    }
}