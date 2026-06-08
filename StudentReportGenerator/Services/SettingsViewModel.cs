using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly AppStateService _appState;

        // UI State Fields
        private bool _isSettingsUnlocked = false;
        private string _settingsUnlockPassword = string.Empty;
        private string _settingsMasterPassword = string.Empty;
        private bool _disableMasterPassword = false;

        private string _settingsSchoolName = string.Empty;
        private string _settingsTeacherName = string.Empty;
        private string _settingsSmtpEmail = string.Empty;
        private SecureString _settingsSmtpSecurePassword = new SecureString();

        private Brush _navBarBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF392A4C"));
        private string _mainAppTitle = "AI STUDENT REPORT GENERATOR";
        private ImageSource? _schoolLogoImage;
        private bool _isLogoVisible = false;

        private string _dynamicApiKeyLabel = "API Key:";
        private SecureString _dynamicSecureApiKey = new SecureString();
        private ObservableCollection<ComboBoxItemWrapper> _modelTierOptions = new ObservableCollection<ComboBoxItemWrapper>();
        private string _selectedModelTier = string.Empty;

        private string _settingsNewFrameworkName = string.Empty;
        private string _settingsNewFrameworkInstruction = string.Empty;

        public ICommand SaveProfileSettingsCommand { get; }
        public ICommand UnlockSettingsCommand { get; }
        public ICommand UploadLogoCommand { get; }
        public ICommand AddFrameworkCommand { get; }

        public SettingsViewModel(AppStateService appState)
        {
            _appState = appState;

            SaveProfileSettingsCommand = new RelayCommand(_ => SaveProfileSettings());
            UnlockSettingsCommand = new RelayCommand(_ => UnlockSettings());
            UploadLogoCommand = new RelayCommand(_ => UploadLogo());
            AddFrameworkCommand = new RelayCommand(_ => AddCustomFrameworkTemplate());

            InitializeSettings();
        }

        private void InitializeSettings()
        {
            var settings = _appState.CurrentSettings;

            SettingsSchoolName = settings.SchoolName;
            SettingsTeacherName = settings.TeacherSignoff;
            SettingsSmtpEmail = settings.SmtpEmail;
            DisableMasterPassword = string.IsNullOrEmpty(settings.MasterPassword);

            _settingsSmtpSecurePassword = ConvertToSecureString(CryptoService.DecryptSecret(settings.SmtpPassword));

            ApplyBrandingConfiguration();
            EvaluateAiProviderOptions(settings.AiProvider);
        }

        private void SaveProfileSettings()
        {
            _appState.CurrentSettings.SchoolName = SettingsSchoolName;
            _appState.CurrentSettings.TeacherSignoff = SettingsTeacherName;
            _appState.CurrentSettings.SmtpEmail = SettingsSmtpEmail;

            _appState.CurrentSettings.SmtpPassword = CryptoService.EncryptSecret(ConvertToPlainString(_settingsSmtpSecurePassword));

            if (DisableMasterPassword)
            {
                _appState.CurrentSettings.MasterPassword = string.Empty;
                SettingsMasterPassword = string.Empty;
            }
            else if (!string.IsNullOrEmpty(SettingsMasterPassword))
            {
                _appState.CurrentSettings.MasterPassword = CryptoService.HashPassword(SettingsMasterPassword);
            }

            // Save the dynamic API key properly based on current provider
            string plainApiKey = ConvertToPlainString(_dynamicSecureApiKey);
            string provider = _appState.CurrentSettings.AiProvider;

            if (provider.Contains("NVIDIA")) _appState.CurrentSettings.NvidiaApiKey = CryptoService.EncryptSecret(plainApiKey);
            else if (provider.Contains("Gemini")) _appState.CurrentSettings.GeminiApiKey = CryptoService.EncryptSecret(plainApiKey);
            else if (provider.Contains("OpenAI")) _appState.CurrentSettings.OpenAiApiKey = CryptoService.EncryptSecret(plainApiKey);
            else if (provider.Contains("Claude")) _appState.CurrentSettings.ClaudeApiKey = CryptoService.EncryptSecret(plainApiKey);

            _appState.SaveSettings();
            ApplyBrandingConfiguration();

            MessageBox.Show("System configurations updated successfully.", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void UnlockSettings()
        {
            if (string.IsNullOrEmpty(_appState.CurrentSettings.MasterPassword))
            {
                IsSettingsUnlocked = true;
                return;
            }

            if (CryptoService.VerifyPassword(SettingsUnlockPassword, _appState.CurrentSettings.MasterPassword))
            {
                IsSettingsUnlocked = true;
                SettingsUnlockPassword = string.Empty;
            }
            else
            {
                MessageBox.Show("Incorrect validation password key.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                SettingsUnlockPassword = string.Empty;
            }
        }

        private void EvaluateAiProviderOptions(string provider)
        {
            string cleanProvider = SanitizeControlOutput(provider);
            ModelTierOptions.Clear();
            if (cleanProvider.Contains("NVIDIA"))
            {
                DynamicApiKeyLabel = "NVIDIA Key:";
                _dynamicSecureApiKey = ConvertToSecureString(CryptoService.DecryptSecret(_appState.CurrentSettings.NvidiaApiKey));
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Llama 3.1 405B (Smarter)", Tag = "meta/llama-3.1-405b-instruct" });
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Llama 3.1 70B (Balanced)", Tag = "meta/llama-3.1-70b-instruct" });
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Nemotron 70B (NVIDIA)", Tag = "nvidia/nemotron-4-340b-instruct" });
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Mistral Large (Fast)", Tag = "mistralai/mistral-large-2-instruct" });
                SelectedModelTier = _appState.CurrentSettings.NvidiaModelTier;
            }
            else if (cleanProvider.Contains("Gemini"))
            {
                DynamicApiKeyLabel = "Gemini Key:";
                _dynamicSecureApiKey = ConvertToSecureString(CryptoService.DecryptSecret(_appState.CurrentSettings.GeminiApiKey));
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Gemini 2.5 Flash", Tag = "gemini-2.5-flash" });
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Gemini 2.5 Pro", Tag = "gemini-2.5-pro" });
                SelectedModelTier = _appState.CurrentSettings.GeminiModelTier;
            }
            else if (cleanProvider.Contains("OpenAI"))
            {
                DynamicApiKeyLabel = "OpenAI Key:";
                _dynamicSecureApiKey = ConvertToSecureString(CryptoService.DecryptSecret(_appState.CurrentSettings.OpenAiApiKey));
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "GPT-4o Mini", Tag = "gpt-4o-mini" });
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "GPT-4o", Tag = "gpt-4o" });
                SelectedModelTier = _appState.CurrentSettings.OpenAiModelTier;
            }
            else if (cleanProvider.Contains("Claude"))
            {
                DynamicApiKeyLabel = "Claude Key:";
                _dynamicSecureApiKey = ConvertToSecureString(CryptoService.DecryptSecret(_appState.CurrentSettings.ClaudeApiKey));
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Claude 3 Haiku", Tag = "claude-3-haiku-20240307" });
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Claude 3.5 Sonnet", Tag = "claude-3-5-sonnet-20240620" });
                SelectedModelTier = _appState.CurrentSettings.ClaudeModelTier;
            }
        }

        private void ApplyBrandingConfiguration()
        {
            if (!string.IsNullOrWhiteSpace(_appState.CurrentSettings.ThemeColorHex))
            {
                try
                {
                    var convertedBrush = new BrushConverter().ConvertFrom(_appState.CurrentSettings.ThemeColorHex) as SolidColorBrush;
                    if (convertedBrush != null)
                    {
                        NavBarBackground = convertedBrush;
                    }
                }
                catch { NavBarBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF392A4C")); }
            }

            if (!string.IsNullOrWhiteSpace(_appState.CurrentSettings.SchoolName) && _appState.CurrentSettings.SchoolName != "Enter School Name")
                MainAppTitle = _appState.CurrentSettings.SchoolName.ToUpper() + " REPORT GENERATOR";

            if (!string.IsNullOrWhiteSpace(_appState.CurrentSettings.SchoolLogoPath) && File.Exists(_appState.CurrentSettings.SchoolLogoPath))
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_appState.CurrentSettings.SchoolLogoPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    SchoolLogoImage = bitmap;
                    IsLogoVisible = true;
                }
                catch { IsLogoVisible = false; }
            }
            else { IsLogoVisible = false; }
        }

        private void UploadLogo()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Images (*.png;*.jpg)|*.png;*.jpg" };
            if (dialog.ShowDialog() == true)
            {
               
                string extension = Path.GetExtension(dialog.FileName);
                string clonedPath = FileSandboxService.CloneAssetToSandbox(dialog.FileName, $"school_logo{extension}");

                _appState.CurrentSettings.SchoolLogoPath = clonedPath;
                _appState.SaveSettings();
                ApplyBrandingConfiguration();
            }
        }

        private void AddCustomFrameworkTemplate()
        {
            if (!string.IsNullOrWhiteSpace(SettingsNewFrameworkName) && !string.IsNullOrWhiteSpace(SettingsNewFrameworkInstruction))
            {
                _appState.CurrentSettings.CustomFrameworks.Add(new ReportFramework { Name = SettingsNewFrameworkName, Instruction = SettingsNewFrameworkInstruction });
                _appState.SaveSettings();
                OnPropertyChanged(nameof(CustomFrameworks));
                SettingsNewFrameworkName = string.Empty;
                SettingsNewFrameworkInstruction = string.Empty;
            }
        }

        private string SanitizeControlOutput(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return string.Empty;
            if (source.Contains("System.Windows.Controls.ComboBoxItem:"))
                return source.Replace("System.Windows.Controls.ComboBoxItem:", "").Trim();
            return source.Trim();
        }

        private SecureString ConvertToSecureString(string text)
        {
            var secure = new SecureString();
            if (string.IsNullOrEmpty(text)) return secure;
            foreach (char c in text) secure.AppendChar(c);
            secure.MakeReadOnly();
            return secure;
        }

        private string ConvertToPlainString(SecureString secure)
        {
            if (secure == null || secure.Length == 0) return string.Empty;
            IntPtr pointer = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(secure);
            try { return System.Runtime.InteropServices.Marshal.PtrToStringBSTR(pointer); }
            finally { System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(pointer); }
        }

        // --- PUBLIC PROPERTIES ---
        public bool IsSettingsUnlocked { get => _isSettingsUnlocked; set => SetProperty(ref _isSettingsUnlocked, value); }
        public string SettingsUnlockPassword { get => _settingsUnlockPassword; set => SetProperty(ref _settingsUnlockPassword, value); }
        public string SettingsMasterPassword { get => _settingsMasterPassword; set => SetProperty(ref _settingsMasterPassword, value); }
        public bool DisableMasterPassword { get => _disableMasterPassword; set => SetProperty(ref _disableMasterPassword, value); }
        public string SettingsSchoolName { get => _settingsSchoolName; set => SetProperty(ref _settingsSchoolName, value); }
        public string SettingsTeacherName { get => _settingsTeacherName; set => SetProperty(ref _settingsTeacherName, value); }
        public string SettingsSmtpEmail { get => _settingsSmtpEmail; set => SetProperty(ref _settingsSmtpEmail, value); }
        public string SettingsSmtpPassword { get => ConvertToPlainString(_settingsSmtpSecurePassword); set { _settingsSmtpSecurePassword = ConvertToSecureString(value); OnPropertyChanged(); } }
        public Brush NavBarBackground { get => _navBarBackground; set => SetProperty(ref _navBarBackground, value); }
        public string MainAppTitle { get => _mainAppTitle; set => SetProperty(ref _mainAppTitle, value); }
        public ImageSource? SchoolLogoImage { get => _schoolLogoImage; set => SetProperty(ref _schoolLogoImage, value); }
        public bool IsLogoVisible { get => _isLogoVisible; set => SetProperty(ref _isLogoVisible, value); }
        public string DynamicApiKeyLabel { get => _dynamicApiKeyLabel; set => SetProperty(ref _dynamicApiKeyLabel, value); }
        public ObservableCollection<ComboBoxItemWrapper> ModelTierOptions { get => _modelTierOptions; set => SetProperty(ref _modelTierOptions, value); }
        public string SettingsNewFrameworkName { get => _settingsNewFrameworkName; set => SetProperty(ref _settingsNewFrameworkName, value); }
        public string SettingsNewFrameworkInstruction { get => _settingsNewFrameworkInstruction; set => SetProperty(ref _settingsNewFrameworkInstruction, value); }
        public System.Collections.Generic.List<ReportFramework> CustomFrameworks => _appState.CurrentSettings.CustomFrameworks;

        public string DynamicApiKeyPassword
        {
            get => ConvertToPlainString(_dynamicSecureApiKey);
            set { _dynamicSecureApiKey = ConvertToSecureString(value); OnPropertyChanged(); }
        }

        public string SelectedAiProvider
        {
            get => _appState.CurrentSettings.AiProvider;
            set
            {
                string clean = SanitizeControlOutput(value);
                if (_appState.CurrentSettings.AiProvider != clean)
                {
                    _appState.CurrentSettings.AiProvider = clean;
                    OnPropertyChanged();
                    EvaluateAiProviderOptions(clean);
                    _appState.SaveSettings();
                }
            }
        }

        public string SelectedModelTier
        {
            get => _selectedModelTier;
            set
            {
                string clean = SanitizeControlOutput(value);
                if (SetProperty(ref _selectedModelTier, clean))
                {
                    string p = SanitizeControlOutput(_appState.CurrentSettings.AiProvider);
                    if (p.Contains("NVIDIA")) _appState.CurrentSettings.NvidiaModelTier = clean;
                    else if (p.Contains("Gemini")) _appState.CurrentSettings.GeminiModelTier = clean;
                    else if (p.Contains("OpenAI")) _appState.CurrentSettings.OpenAiModelTier = clean;
                    else if (p.Contains("Claude")) _appState.CurrentSettings.ClaudeModelTier = clean;
                    _appState.SaveSettings();
                }
            }
        }

        public string SelectedThemeColorHex
        {
            get => _appState.CurrentSettings.ThemeColorHex;
            set
            {
                string cleanHex = SanitizeControlOutput(value);
                if (!string.IsNullOrWhiteSpace(cleanHex) && _appState.CurrentSettings.ThemeColorHex != cleanHex)
                {
                    _appState.CurrentSettings.ThemeColorHex = cleanHex;
                    OnPropertyChanged();
                    ApplyBrandingConfiguration();
                    _appState.SaveSettings();
                }
            }
        }

        public bool IsDarkMode
        {
            get => _appState.CurrentSettings.IsDarkMode;
            set
            {
                if (_appState.CurrentSettings.IsDarkMode != value)
                {
                    _appState.CurrentSettings.IsDarkMode = value;
                    OnPropertyChanged();
                    var win = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    if (win != null)
                    {
                        var method = win.GetType().GetMethod("ApplyDarkMode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        method?.Invoke(win, new object[] { value });
                    }
                    _appState.SaveSettings();
                }
            }
        }
    }
}