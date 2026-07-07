namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Lightweight display/value pair bound to the AI Model Quality ComboBox in
    /// SettingsViewModel.ModelTierOptions. <see cref="Content"/> is the human-readable label shown
    /// to the teacher (e.g. "Claude Sonnet 4.6 (Balanced)"); <see cref="Tag"/> is the underlying
    /// provider model identifier sent to the AI API (e.g. "claude-sonnet-4-6").
    /// </summary>
    public class ComboBoxItemWrapper
    {
        public string Content { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
    }
}