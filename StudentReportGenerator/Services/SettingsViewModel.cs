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
    /// <summary>
    /// Backs the entire Settings surface of the app: profile/branding, SMTP email configuration,
    /// AI provider &amp; API key management, master-password protected vault, theme (light/dark),
    /// accessibility preferences, school-data (SIS) connection settings, and the shared framework
    /// library import/export. Exposed to the view via <c>MainViewModel.SettingsVM</c> so both
    /// ViewModels share the same <see cref="AppStateService"/>-backed settings.
    /// </summary>
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
        public ICommand TestApiCommand { get; }
        public ICommand ExportLibraryCommand { get; }
        public ICommand ImportLibraryCommand { get; }
        public ICommand PurgeSisCacheCommand { get; }
        public ICommand SyncNowCommand { get; }

        public SettingsViewModel(AppStateService appState)
        {
            _appState = appState;

            SaveProfileSettingsCommand = new RelayCommand(_ => SaveProfileSettings());
            UnlockSettingsCommand = new RelayCommand(_ => UnlockSettings());
            UploadLogoCommand = new RelayCommand(_ => UploadLogo());
            AddFrameworkCommand = new RelayCommand(_ => AddCustomFrameworkTemplate());
            TestApiCommand = new RelayCommand(_ => TestApiKey(), _ => !IsTestingConnection);
            ExportLibraryCommand = new RelayCommand(_ => ExportSharedLibrary());
            ImportLibraryCommand = new RelayCommand(_ => ImportSharedLibrary());
            PurgeSisCacheCommand = new RelayCommand(_ => PurgeSisCache());
            SyncNowCommand = new RelayCommand(_ => SyncNow());

            InitializeSettings();
        }

        /// <summary>
        /// "Test Connection" handler: makes a lightweight, read-only call to the selected
        /// provider's API (list models, or a minimal POST-endpoint probe for Claude) purely to
        /// confirm the pasted API key is accepted, without spending tokens generating a report.
        /// </summary>
        private async void TestApiKey()
        {
            FlushCurrentApiKey();

            string encryptedKey = string.Empty;
            if (SelectedAiProvider.Contains("NVIDIA")) encryptedKey = _appState.CurrentSettings.NvidiaApiKey;
            else if (SelectedAiProvider.Contains("Gemini")) encryptedKey = _appState.CurrentSettings.GeminiApiKey;
            else if (SelectedAiProvider.Contains("OpenAI")) encryptedKey = _appState.CurrentSettings.OpenAiApiKey;
            else if (SelectedAiProvider.Contains("Claude")) encryptedKey = _appState.CurrentSettings.ClaudeApiKey;

            string key = CryptoService.DecryptSecret(encryptedKey);
            if (string.IsNullOrWhiteSpace(key) || key.Length < 10)
            {
                ApiTestStatus = "⚠️ No API key entered. Please paste your key first.";
                return;
            }

            IsTestingConnection = true;
            ApiTestStatus = "Testing connection to provider...";

            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");

                string testUrl = "https://api.openai.com/v1/models";
                if (SelectedAiProvider.Contains("NVIDIA")) testUrl = "https://integrate.api.nvidia.com/v1/models";
                else if (SelectedAiProvider.Contains("Claude")) { testUrl = "https://api.anthropic.com/v1/messages"; client.DefaultRequestHeaders.Add("x-api-key", key); client.DefaultRequestHeaders.Remove("Authorization"); }
                else if (SelectedAiProvider.Contains("Gemini")) testUrl = $"https://generativelanguage.googleapis.com/v1beta/models?key={key}";

                var response = await client.GetAsync(testUrl);

                // MethodNotAllowed (405) counts as success for Claude (GET on a POST-only endpoint still proves the key is accepted)
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.BadRequest || response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                    ApiTestStatus = "✅ Connection successful! Your API key is working correctly.";
                else
                    ApiTestStatus = $"❌ Connection failed (Error {(int)response.StatusCode}). Check your key.";
            }
            catch (Exception ex)
            {
                ApiTestStatus = $"❌ Network error: {ex.Message}";
            }
            finally
            {
                IsTestingConnection = false;
            }
        }

        /// <summary>Populates every bindable property from the persisted <see cref="AppSettings"/>
        /// when the ViewModel is constructed (i.e. once, at app startup).</summary>
        private void InitializeSettings()
        {
            var settings = _appState.CurrentSettings;

            SettingsSchoolName = settings.SchoolName;
            SettingsTeacherName = settings.TeacherSignoff;
            SettingsSmtpEmail = settings.SmtpEmail;
            DisableMasterPassword = string.IsNullOrEmpty(settings.MasterPassword);

            if (!string.IsNullOrEmpty(settings.SmtpPassword))
                _settingsSmtpSecurePassword = ConvertToSecureString(CryptoService.DecryptSecret(settings.SmtpPassword));

            _isDarkMode = settings.IsDarkMode;
            ApplyTheme(_isDarkMode);
            ApplyFontPreferences();

            ApplyBrandingConfiguration();
            EvaluateAiProviderOptions(settings.AiProvider);
        }

        /// <summary>
        /// Persists the Profile &amp; Branding and AI Provider tabs. The master password is
        /// deliberately hashed with <see cref="CryptoService.HashPassword"/> (one-way) here, never
        /// encrypted with <see cref="CryptoService.EncryptSecret"/> — mixing those two up was the
        /// bug that used to permanently lock teachers out of Settings. See
        /// StudentReportGenerator.Tests.CryptoServiceTests for the regression test.
        /// </summary>
        private void SaveProfileSettings()
        {
            _appState.CurrentSettings.SchoolName = SettingsSchoolName;
            _appState.CurrentSettings.TeacherSignoff = SettingsTeacherName;
            _appState.CurrentSettings.SmtpEmail = SettingsSmtpEmail;

            string plainSmtp = ConvertToPlainString(_settingsSmtpSecurePassword);
            _appState.CurrentSettings.SmtpPassword = CryptoService.EncryptSecret(plainSmtp);

            if (DisableMasterPassword) _appState.CurrentSettings.MasterPassword = string.Empty;
            else if (!string.IsNullOrEmpty(SettingsMasterPassword)) _appState.CurrentSettings.MasterPassword = CryptoService.HashPassword(SettingsMasterPassword);

            FlushCurrentApiKey(); // Only this is needed!

            System.Windows.MessageBox.Show("Configuration updated successfully.", "Saved", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        /// <summary>Attempts to unlock the Settings vault using the entered password. If no master
        /// password has ever been set, Settings are unlocked unconditionally (nothing to protect yet).</summary>
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
        /// <summary>
        /// Encrypts and saves whatever API key is currently held in the (plaintext, in-memory only)
        /// <see cref="DynamicApiKeyPassword"/> field back into the correct provider-specific settings
        /// field. Called before switching providers or saving settings, so a key the teacher just
        /// typed is never lost when the active provider changes.
        /// </summary>
        private void FlushCurrentApiKey()
        {
            if (string.IsNullOrEmpty(_appState.CurrentSettings.AiProvider)) return;

            string plainApiKey = ConvertToPlainString(_dynamicSecureApiKey);
            string currentProvider = _appState.CurrentSettings.AiProvider;

            if (currentProvider.Contains("NVIDIA")) _appState.CurrentSettings.NvidiaApiKey = CryptoService.EncryptSecret(plainApiKey);
            else if (currentProvider.Contains("Gemini")) _appState.CurrentSettings.GeminiApiKey = CryptoService.EncryptSecret(plainApiKey);
            else if (currentProvider.Contains("OpenAI")) _appState.CurrentSettings.OpenAiApiKey = CryptoService.EncryptSecret(plainApiKey);
            else if (currentProvider.Contains("Claude")) _appState.CurrentSettings.ClaudeApiKey = CryptoService.EncryptSecret(plainApiKey);

            _appState.SaveSettings();
        }

        /// <summary>
        /// Rebuilds <see cref="ModelTierOptions"/> and loads the correct decrypted API key for the
        /// newly selected provider. Note: the four <c>if/else if</c> branches below intentionally
        /// mirror the equivalent switch in <see cref="AiServiceFactory.Create"/> — this is UI-side
        /// bookkeeping (which key/model-tier fields to show), while the factory does the actual
        /// runtime provider selection for report generation.
        /// </summary>
        private void EvaluateAiProviderOptions(string provider)
        {
            FlushCurrentApiKey();
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
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Claude Haiku 4.5 (Fast)", Tag = "claude-haiku-4-5-20251001" });
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Claude Sonnet 4.6 (Balanced)", Tag = "claude-sonnet-4-6" });
                SelectedModelTier = _appState.CurrentSettings.ClaudeModelTier;
            }
        }

        /// <summary>Applies the school's saved accent colour, title, and logo to the UI.
        /// Called on startup and whenever the teacher changes any branding setting.</summary>
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

        /// <summary>Lets the teacher pick a logo image, clones it into the app's sandboxed
        /// AppData folder (so it survives the original file being moved/deleted), and applies it.</summary>
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

        /// <summary>Adds a new named tone/style template to the teacher's saved list, if both a
        /// name and instruction have been entered.</summary>
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

        /// <summary>Wraps a plaintext string in a read-only <see cref="SecureString"/> so it's not
        /// held as a plain managed string in memory for longer than necessary.</summary>
        private SecureString ConvertToSecureString(string text)
        {
            var secure = new SecureString();
            if (string.IsNullOrEmpty(text)) return secure;
            foreach (char c in text) secure.AppendChar(c);
            secure.MakeReadOnly();
            return secure;
        }

        /// <summary>Reverses <see cref="ConvertToSecureString"/> only when the plaintext value is
        /// actually needed (e.g. immediately before DPAPI-encrypting for storage).</summary>
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
        private bool _isTestingConnection = false;
        public bool IsTestingConnection { get => _isTestingConnection; set { if (SetProperty(ref _isTestingConnection, value)) System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } }
        private string _apiTestStatus = string.Empty;
        public string ApiTestStatus { get => _apiTestStatus; set => SetProperty(ref _apiTestStatus, value); }
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

        // --- Accessibility & interface preferences ---
        public bool IsDyslexiaFriendlyFont
        {
            get => _appState.CurrentSettings.DyslexiaFriendlyFont;
            set
            {
                if (_appState.CurrentSettings.DyslexiaFriendlyFont != value)
                {
                    _appState.CurrentSettings.DyslexiaFriendlyFont = value;
                    _appState.SaveSettings();
                    ApplyFontPreferences();
                    OnPropertyChanged();
                }
            }
        }

        public double UiTextScale
        {
            get => _appState.CurrentSettings.UiTextScale;
            set
            {
                double clamped = Math.Clamp(value, 0.85, 1.4);
                if (Math.Abs(_appState.CurrentSettings.UiTextScale - clamped) > 0.001)
                {
                    _appState.CurrentSettings.UiTextScale = clamped;
                    _appState.SaveSettings();
                    ApplyFontPreferences();
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSimpleMode
        {
            get => _appState.CurrentSettings.SimpleMode;
            set
            {
                if (_appState.CurrentSettings.SimpleMode != value)
                {
                    _appState.CurrentSettings.SimpleMode = value;
                    _appState.SaveSettings();
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>Pushes the current font family/size choice into the app-wide
        /// <c>AppFontFamily</c>/<c>AppFontSize</c> dynamic resources declared in App.xaml, which
        /// every window and control in the app binds its <c>FontFamily</c>/<c>FontSize</c> to.</summary>
        private void ApplyFontPreferences()
        {
            var res = System.Windows.Application.Current.Resources;
            res["AppFontFamily"] = new System.Windows.Media.FontFamily(
                _appState.CurrentSettings.DyslexiaFriendlyFont ? "Comic Sans MS" : "Segoe UI");
            res["AppFontSize"] = 13.0 * _appState.CurrentSettings.UiTextScale;
        }

        // --- Trust & disclosure ---
        public bool AppendAiDisclosure
        {
            get => _appState.CurrentSettings.AppendAiDisclosure;
            set { if (_appState.CurrentSettings.AppendAiDisclosure != value) { _appState.CurrentSettings.AppendAiDisclosure = value; _appState.SaveSettings(); OnPropertyChanged(); } }
        }

        public bool EnableSafeguardingPrompt
        {
            get => _appState.CurrentSettings.EnableSafeguardingPrompt;
            set { if (_appState.CurrentSettings.EnableSafeguardingPrompt != value) { _appState.CurrentSettings.EnableSafeguardingPrompt = value; _appState.SaveSettings(); OnPropertyChanged(); } }
        }

        // --- School Connection (IT/data-manager tier, behind the master-password lock) ---
        public System.Collections.Generic.List<string> SchoolDataProviderOptions { get; } = new()
        {
            "Manual Entry",
            "Wonde (UK) — coming soon",
            "OneRoster / Clever / ClassLink (US) — coming soon",
        };

        public string SelectedSchoolDataProvider
        {
            get => _appState.CurrentSettings.SchoolDataProvider;
            set
            {
                string clean = SanitizeControlOutput(value);
                // Only Manual Entry is live today; connector choices are visible so IT can
                // see the roadmap, but they cannot be activated until the integration ships
                if (clean.Contains("coming soon"))
                {
                    MessageBox.Show("This SIS connector is on the roadmap but not yet available. The app will continue using manual entry.", "Not Yet Available", MessageBoxButton.OK, MessageBoxImage.Information);
                    clean = "Manual Entry";
                }
                if (_appState.CurrentSettings.SchoolDataProvider != clean)
                {
                    _appState.CurrentSettings.SchoolDataProvider = clean;
                    _appState.SaveSettings();
                }
                OnPropertyChanged();
            }
        }

        public bool IncludeAttendanceInPrompts
        {
            get => _appState.CurrentSettings.IncludeAttendanceInPrompts;
            set { if (_appState.CurrentSettings.IncludeAttendanceInPrompts != value) { _appState.CurrentSettings.IncludeAttendanceInPrompts = value; _appState.SaveSettings(); OnPropertyChanged(); } }
        }

        public bool IncludeBehaviourInPrompts
        {
            get => _appState.CurrentSettings.IncludeBehaviourInPrompts;
            set { if (_appState.CurrentSettings.IncludeBehaviourInPrompts != value) { _appState.CurrentSettings.IncludeBehaviourInPrompts = value; _appState.SaveSettings(); OnPropertyChanged(); } }
        }

        public bool IncludeGradesInPrompts
        {
            get => _appState.CurrentSettings.IncludeGradesInPrompts;
            set { if (_appState.CurrentSettings.IncludeGradesInPrompts != value) { _appState.CurrentSettings.IncludeGradesInPrompts = value; _appState.SaveSettings(); OnPropertyChanged(); } }
        }

        public bool IncludeSupportPlanInPrompts
        {
            get => _appState.CurrentSettings.IncludeSupportPlanInPrompts;
            set { if (_appState.CurrentSettings.IncludeSupportPlanInPrompts != value) { _appState.CurrentSettings.IncludeSupportPlanInPrompts = value; _appState.SaveSettings(); OnPropertyChanged(); } }
        }

        public string LastSisSyncDisplay => _appState.CurrentSettings.LastSisSyncUtc.HasValue
            ? $"Last synced: {_appState.CurrentSettings.LastSisSyncUtc.Value.ToLocalTime():g}"
            : "Never synced (no SIS connection configured).";

        /// <summary>Manual "Sync Now" trigger for the School Connection tab. Placeholder timestamp
        /// update until a real SIS connector (Wonde/OneRoster) is wired up — see <see cref="SchoolDataOrchestratorService"/>.</summary>
        private void SyncNow()
        {
            if (_appState.CurrentSettings.SchoolDataProvider == "Manual Entry")
            {
                MessageBox.Show("No SIS connection is configured, so there is nothing to sync. Choose a school data provider first (connectors arriving in a future update).", "Nothing to Sync", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _appState.CurrentSettings.LastSisSyncUtc = DateTime.UtcNow;
            _appState.SaveSettings();
            OnPropertyChanged(nameof(LastSisSyncDisplay));
        }

        /// <summary>Lets a school's data lead wipe all locally cached SIS data on demand, after
        /// an explicit confirmation — a data-minimisation control from the Integration Plan.</summary>
        private void PurgeSisCache()
        {
            if (MessageBox.Show("Delete all locally cached school data (attendance, behaviour, grades)? Reports will fall back to manual entry until the next sync.", "Purge Cached School Data", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                SchoolDataCacheService.PurgeAll();
                MessageBox.Show("Cached school data deleted.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // --- Shared library (HoD/SLT publish once, teachers import) ---

        /// <summary>Exports the teacher's writing styles and curriculum topics to a JSON file,
        /// for sharing with the rest of a department. See <see cref="FrameworkShareService"/>.</summary>
        private void ExportSharedLibrary()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "FacultyFlow Library (*.json)|*.json", FileName = "facultyflow-library.json" };
            if (dialog.ShowDialog() == true)
            {
                FrameworkShareService.Export(dialog.FileName, _appState.CurrentSettings.CustomFrameworks, _appState.CurrentSettings.CurriculumTopics);
                MessageBox.Show("Writing styles and curriculum topics exported. Share this file with your department so everyone writes in a consistent voice.", "Library Exported", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>Imports a colleague's shared library file, merging (not replacing) it into the
        /// teacher's own writing styles and curriculum topics.</summary>
        private void ImportSharedLibrary()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "FacultyFlow Library (*.json)|*.json" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var (frameworksAdded, topicsAdded) = FrameworkShareService.Import(dialog.FileName, _appState.CurrentSettings.CustomFrameworks, _appState.CurrentSettings.CurriculumTopics);
                    _appState.SaveSettings();
                    OnPropertyChanged(nameof(CustomFrameworks));
                    MessageBox.Show($"Imported {frameworksAdded} writing style(s) and {topicsAdded} curriculum topic(s).", "Library Imported", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch
                {
                    MessageBox.Show("That file could not be read as a FacultyFlow library.", "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    _appState.CurrentSettings.IsDarkMode = value;
                    _appState.SaveSettings();
                    ApplyTheme(value);
                }
            }
        }

        // Light/dark colour palettes for the whole app's theme resources. Every key declared in
        // App.xaml MUST have an entry in both dictionaries below, or Dark Mode will leave that
        // element showing its (wrong) hardcoded XAML default when toggled.
        private static readonly System.Collections.Generic.Dictionary<string, string> LightPalette = new()
        {
            ["ThemeAppBg"] = "#FFFAFAFA",
            ["ThemeCardBg"] = "#FFFFFFFF",
            ["ThemeText"] = "#FF333333",
            ["ThemeMutedText"] = "#FF616161",
            ["ThemeBorder"] = "#FFDDDDDD",
            ["ThemeInputBg"] = "#FFFFFFFF",
            ["ThemePreviewBg"] = "#FFF9F9F9",
            ["ThemeButtonBg"] = "#FFEEEEEE",
            ["ThemeButtonHoverBg"] = "#FFDDDDDD",
            ["ThemePrimaryBtnBg"] = "#FF4CAF50",
            ["ThemePrimaryBtnHoverBg"] = "#FF43A047",
            ["ThemeDangerBg"] = "#FFFFEBEE",
            ["ThemeDangerText"] = "#FFD32F2F",
            ["ThemeDangerStrongBg"] = "#FFF44336",
            ["ThemeInfoBg"] = "#FFE0F7FA",
            ["ThemeInfoText"] = "#FF00838F",
            ["ThemeInfoAccent"] = "#FF1E88E5",
            ["ThemeWarningBg"] = "#FFFFF3E0",
            ["ThemeWarningText"] = "#FFE65100",
            ["ThemeWarningAccent"] = "#FFF57C00",
            ["ThemeSuccessBg"] = "#FFE8F5E9",
            ["ThemeSuccessText"] = "#FF2E7D32",
            ["ThemeSuccessAccent"] = "#FF43A047",
            ["ThemeAccentBlueBg"] = "#FF1976D2",
            ["ThemeMetricCardBg"] = "#FFF5F5F5",
            ["ThemeMetricIndigoBg"] = "#FFE8EAF6",
            ["ThemeMetricNvidiaBg"] = "#FFE1F5FE",
            ["ThemeMetricNvidiaText"] = "#FF01579B",
            ["ThemeMetricGeminiBg"] = "#FFE0F7FA",
            ["ThemeMetricGeminiText"] = "#FF006064",
            ["ThemeMetricOpenAiBg"] = "#FFF3E5F5",
            ["ThemeMetricOpenAiText"] = "#FF4A148C",
            ["ThemeMetricClaudeBg"] = "#FFFFF3E0",
            ["ThemeMetricClaudeText"] = "#FFE65100",
        };

        private static readonly System.Collections.Generic.Dictionary<string, string> DarkPalette = new()
        {
            ["ThemeAppBg"] = "#FF121212",
            ["ThemeCardBg"] = "#FF1E1E1E",
            ["ThemeText"] = "#FFE0E0E0",
            ["ThemeMutedText"] = "#FFAAAAAA",
            ["ThemeBorder"] = "#FF333333",
            ["ThemeInputBg"] = "#FF2D2D2D",
            ["ThemePreviewBg"] = "#FF252525",
            ["ThemeButtonBg"] = "#FF2F2F2F",
            ["ThemeButtonHoverBg"] = "#FF3D3D3D",
            ["ThemePrimaryBtnBg"] = "#FF388E3C",
            ["ThemePrimaryBtnHoverBg"] = "#FF2E7D32",
            ["ThemeDangerBg"] = "#FF3B2226",
            ["ThemeDangerText"] = "#FFEF9A9A",
            ["ThemeDangerStrongBg"] = "#FFD32F2F",
            ["ThemeInfoBg"] = "#FF0F2E33",
            ["ThemeInfoText"] = "#FF4DD0E1",
            ["ThemeInfoAccent"] = "#FF64B5F6",
            ["ThemeWarningBg"] = "#FF3A2A16",
            ["ThemeWarningText"] = "#FFFFB74D",
            ["ThemeWarningAccent"] = "#FFFFB74D",
            ["ThemeSuccessBg"] = "#FF1B2E1F",
            ["ThemeSuccessText"] = "#FF81C784",
            ["ThemeSuccessAccent"] = "#FF81C784",
            ["ThemeAccentBlueBg"] = "#FF1565C0",
            ["ThemeMetricCardBg"] = "#FF232323",
            ["ThemeMetricIndigoBg"] = "#FF23263A",
            ["ThemeMetricNvidiaBg"] = "#FF12293A",
            ["ThemeMetricNvidiaText"] = "#FF81D4FA",
            ["ThemeMetricGeminiBg"] = "#FF103235",
            ["ThemeMetricGeminiText"] = "#FF80DEEA",
            ["ThemeMetricOpenAiBg"] = "#FF2A1D31",
            ["ThemeMetricOpenAiText"] = "#FFCE93D8",
            ["ThemeMetricClaudeBg"] = "#FF33270F",
            ["ThemeMetricClaudeText"] = "#FFFFCC80",
        };

        /// <summary>Overwrites every theme <see cref="SolidColorBrush"/> resource in
        /// <c>Application.Current.Resources</c> with the chosen palette. Because every themed
        /// control in the app binds via <c>DynamicResource</c> (not <c>StaticResource</c>), this
        /// single call is enough to re-theme the entire UI instantly, with no window reload.</summary>
        private void ApplyTheme(bool isDark)
        {
            var palette = isDark ? DarkPalette : LightPalette;
            var res = System.Windows.Application.Current.Resources;
            foreach (var entry in palette)
            {
                res[entry.Key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(entry.Value));
            }
        }
    }
}