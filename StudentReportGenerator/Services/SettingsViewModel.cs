using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
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
        private readonly SchoolDataOrchestratorService _schoolData;

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
        public ICommand TestSisConnectionCommand { get; }
        public ICommand SelectAccentSwatchCommand { get; }

        public SettingsViewModel(AppStateService appState, SchoolDataOrchestratorService schoolData)
        {
            _appState = appState;
            _schoolData = schoolData;

            SaveProfileSettingsCommand = new RelayCommand(_ => SaveProfileSettings());
            UnlockSettingsCommand = new RelayCommand(_ => UnlockSettings());
            UploadLogoCommand = new RelayCommand(_ => UploadLogo());
            AddFrameworkCommand = new RelayCommand(_ => AddCustomFrameworkTemplate());
            TestApiCommand = new RelayCommand(_ => TestApiKey(), _ => !IsTestingConnection);
            ExportLibraryCommand = new RelayCommand(_ => ExportSharedLibrary());
            ImportLibraryCommand = new RelayCommand(_ => ImportSharedLibrary());
            PurgeSisCacheCommand = new RelayCommand(_ => PurgeSisCache());
            SyncNowCommand = new AsyncRelayCommand(_ => SyncNowAsync(), _ => !IsSyncingSis);
            TestSisConnectionCommand = new AsyncRelayCommand(_ => TestSisConnectionAsync(), _ => !IsSyncingSis);
            SelectAccentSwatchCommand = new RelayCommand(hex => { if (hex is string h && !string.IsNullOrWhiteSpace(h)) SelectedThemeColorHex = h; });

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

            if (!string.IsNullOrEmpty(settings.WondeApiToken))
                _wondeSecureToken = ConvertToSecureString(CryptoService.DecryptSecret(settings.WondeApiToken));

            _isDarkMode = settings.IsDarkMode;
            ApplyTheme(_isDarkMode);
            ApplyInterfacePreferences();

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

            FlushWondeCredentials();
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
                    OnPropertyChanged(nameof(CustomAccentHex));
                    ApplyBrandingConfiguration();
                    ApplyTheme(IsDarkMode); // accent family + primary buttons follow immediately
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
                    // Keep the checkbox and the font ComboBox coherent: ticking it selects the
                    // dyslexia-friendly face, unticking returns to the default.
                    _appState.CurrentSettings.FontFamilyName = value ? "Comic Sans MS" : "Segoe UI";
                    _appState.SaveSettings();
                    ApplyInterfacePreferences();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedFontFamilyName));
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
                    ApplyInterfacePreferences();
                    OnPropertyChanged();
                }
            }
        }

        // --- Appearance personalisation (accent swatches, custom hex, fonts, density, tint) ---

        /// <summary>Curated swatch grid: the original five dark branding colours plus brighter
        /// options that read well as an accent in both light and dark mode (contrast-adjusted by
        /// <see cref="ThemePaletteService.BuildAccentPalette"/> either way).</summary>
        public System.Collections.Generic.List<string> AccentSwatchOptions { get; } = new()
        {
            "#FF392A4C", "#FF0A192F", "#FF1B3822", "#FF4A1525", "#FF263238",
            "#FF00695C", "#FF1565C0", "#FF6A1B9A", "#FFC2185B", "#FFEF6C00",
            "#FF2E7D32", "#FF5D4037",
        };

        /// <summary>Bound to the free-entry hex box. Invalid input never crashes theming: it just
        /// sets <see cref="AccentHexStatus"/> and keeps the previous colour until the hex parses.</summary>
        public string CustomAccentHex
        {
            get => _appState.CurrentSettings.ThemeColorHex;
            set
            {
                string clean = SanitizeControlOutput(value).Trim();
                if (ThemePaletteService.TryParseHex(clean) is null)
                {
                    AccentHexStatus = string.IsNullOrEmpty(clean) ? string.Empty : "Enter a colour like #1565C0";
                    return;
                }
                AccentHexStatus = string.Empty;
                SelectedThemeColorHex = clean;
            }
        }

        private string _accentHexStatus = string.Empty;
        public string AccentHexStatus { get => _accentHexStatus; set => SetProperty(ref _accentHexStatus, value); }

        public System.Collections.Generic.List<string> FontFamilyOptions { get; } = BuildFontFamilyOptions();

        private static System.Collections.Generic.List<string> BuildFontFamilyOptions()
        {
            var options = new System.Collections.Generic.List<string> { "Segoe UI", "Verdana", "Calibri", "Georgia", "Comic Sans MS" };
            // OpenDyslexic is only offered when actually installed on this machine
            if (Fonts.SystemFontFamilies.Any(f => f.Source.Contains("OpenDyslexic", StringComparison.OrdinalIgnoreCase)))
                options.Add("OpenDyslexic");
            return options;
        }

        public string SelectedFontFamilyName
        {
            get => string.IsNullOrWhiteSpace(_appState.CurrentSettings.FontFamilyName)
                ? (_appState.CurrentSettings.DyslexiaFriendlyFont ? "Comic Sans MS" : "Segoe UI")
                : _appState.CurrentSettings.FontFamilyName;
            set
            {
                string clean = SanitizeControlOutput(value);
                if (string.IsNullOrWhiteSpace(clean) || _appState.CurrentSettings.FontFamilyName == clean) return;
                _appState.CurrentSettings.FontFamilyName = clean;
                // Keep the legacy dyslexia checkbox coherent with an explicit font choice
                _appState.CurrentSettings.DyslexiaFriendlyFont = clean is "Comic Sans MS" or "OpenDyslexic";
                _appState.SaveSettings();
                ApplyInterfacePreferences();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDyslexiaFriendlyFont));
            }
        }

        public System.Collections.Generic.List<string> LayoutDensityOptions { get; } = new() { "Comfortable", "Compact" };

        public string SelectedLayoutDensity
        {
            get => _appState.CurrentSettings.LayoutDensity;
            set
            {
                string clean = SanitizeControlOutput(value);
                if (string.IsNullOrWhiteSpace(clean) || _appState.CurrentSettings.LayoutDensity == clean) return;
                _appState.CurrentSettings.LayoutDensity = clean;
                _appState.SaveSettings();
                ApplyInterfacePreferences();
                OnPropertyChanged();
            }
        }

        public System.Collections.ObjectModel.ObservableCollection<ComboBoxItemWrapper> BackgroundTintOptions { get; } = new()
        {
            new ComboBoxItemWrapper { Content = "None (default)", Tag = "" },
            new ComboBoxItemWrapper { Content = "Warm cream", Tag = "#FFB98A3C" },
            new ComboBoxItemWrapper { Content = "Cool blue", Tag = "#FF3C78B9" },
            new ComboBoxItemWrapper { Content = "Soft green", Tag = "#FF3CB964" },
            new ComboBoxItemWrapper { Content = "Lavender", Tag = "#FF7A5FBF" },
        };

        public string SelectedBackgroundTintHex
        {
            get => _appState.CurrentSettings.BackgroundTintHex;
            set
            {
                string clean = SanitizeControlOutput(value);
                if (_appState.CurrentSettings.BackgroundTintHex == clean) return;
                _appState.CurrentSettings.BackgroundTintHex = clean;
                _appState.SaveSettings();
                ApplyTheme(IsDarkMode);
                OnPropertyChanged();
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

        /// <summary>
        /// Pushes the current font family, text scale, and layout density into the app-wide dynamic
        /// resources declared in App.xaml (<c>AppFontFamily</c>, <c>AppFontSize</c>,
        /// <c>AppFontSizeSmall</c>, <c>AppControlHeight</c>, <c>AppControlPadding</c>), which every
        /// window and control binds to. An explicit <see cref="AppSettings.FontFamilyName"/> wins;
        /// otherwise the dyslexia-friendly toggle decides, exactly as before that setting existed.
        /// </summary>
        private void ApplyInterfacePreferences()
        {
            var settings = _appState.CurrentSettings;
            var res = System.Windows.Application.Current.Resources;

            string family = !string.IsNullOrWhiteSpace(settings.FontFamilyName)
                ? settings.FontFamilyName
                : (settings.DyslexiaFriendlyFont ? "Comic Sans MS" : "Segoe UI");
            res["AppFontFamily"] = new System.Windows.Media.FontFamily(family);
            res["AppFontSize"] = 13.0 * settings.UiTextScale;
            res["AppFontSizeSmall"] = 11.0 * settings.UiTextScale;

            bool compact = settings.LayoutDensity == "Compact";
            res["AppControlHeight"] = compact ? 29.0 : 35.0;
            res["AppControlPadding"] = new Thickness(compact ? 5 : 8);
        }

        // --- AI generation quality ---
        public System.Collections.Generic.List<string> CreativityOptions { get; } = new() { "Low", "Balanced", "High" };

        /// <summary>"Low" keeps every report measured and consistent; "High" varies phrasing more.
        /// Mapped to the provider sampling temperature (0.3 / 0.7 / 0.95) in MainViewModel.</summary>
        public string SelectedCreativityLevel
        {
            get => _appState.CurrentSettings.CreativityLevel;
            set
            {
                string clean = SanitizeControlOutput(value);
                if (!string.IsNullOrWhiteSpace(clean) && _appState.CurrentSettings.CreativityLevel != clean)
                {
                    _appState.CurrentSettings.CreativityLevel = clean;
                    _appState.SaveSettings();
                    OnPropertyChanged();
                }
            }
        }

        public bool UseCommentBankStyle
        {
            get => _appState.CurrentSettings.UseCommentBankStyle;
            set { if (_appState.CurrentSettings.UseCommentBankStyle != value) { _appState.CurrentSettings.UseCommentBankStyle = value; _appState.SaveSettings(); OnPropertyChanged(); } }
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
            "Wonde (UK)",
            "OneRoster / CSV import",
        };

        public string SelectedSchoolDataProvider
        {
            get => _appState.CurrentSettings.SchoolDataProvider;
            set
            {
                string clean = SanitizeControlOutput(value);
                if (_appState.CurrentSettings.SchoolDataProvider != clean)
                {
                    _appState.CurrentSettings.SchoolDataProvider = clean;
                    _appState.SaveSettings();
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWondeSelected));
                OnPropertyChanged(nameof(IsCsvProviderSelected));
                SisTestStatus = string.Empty;
            }
        }

        public bool IsWondeSelected => _appState.CurrentSettings.SchoolDataProvider?.Contains("Wonde") == true;
        public bool IsCsvProviderSelected
        {
            get
            {
                string p = _appState.CurrentSettings.SchoolDataProvider ?? string.Empty;
                return p.Contains("OneRoster") || p.Contains("CSV");
            }
        }

        // Wonde credentials: the token is held in-memory as a SecureString (fed from a PasswordBox
        // in code-behind, like the SMTP password) and DPAPI-encrypted on save.
        private SecureString _wondeSecureToken = new SecureString();
        public string WondeApiTokenInput
        {
            get => ConvertToPlainString(_wondeSecureToken);
            set { _wondeSecureToken = ConvertToSecureString(value); OnPropertyChanged(); }
        }

        public string WondeSchoolId
        {
            get => _appState.CurrentSettings.WondeSchoolId;
            set
            {
                string clean = SanitizeControlOutput(value).Trim();
                if (_appState.CurrentSettings.WondeSchoolId != clean)
                {
                    _appState.CurrentSettings.WondeSchoolId = clean;
                    _appState.SaveSettings();
                    OnPropertyChanged();
                }
            }
        }

        public string SisCsvLastImportDisplay => string.IsNullOrEmpty(_appState.CurrentSettings.SisCsvLastImportFile)
            ? "No school data file imported yet."
            : $"Last import: {_appState.CurrentSettings.SisCsvLastImportFile}";

        private bool _isSyncingSis;
        public bool IsSyncingSis
        {
            get => _isSyncingSis;
            set { if (SetProperty(ref _isSyncingSis, value)) System.Windows.Input.CommandManager.InvalidateRequerySuggested(); }
        }

        private string _sisTestStatus = string.Empty;
        public string SisTestStatus { get => _sisTestStatus; set => SetProperty(ref _sisTestStatus, value); }

        /// <summary>Encrypts and persists the Wonde token currently held in memory. Called before
        /// saving settings, testing the connection, or syncing, so a freshly pasted token is never lost.</summary>
        private void FlushWondeCredentials()
        {
            _appState.CurrentSettings.WondeApiToken = CryptoService.EncryptSecret(ConvertToPlainString(_wondeSecureToken));
            _appState.SaveSettings();
        }

        /// <summary>
        /// "Test Connection" for the Wonde connector: one lightweight, read-only call
        /// (<c>GET /schools/{id}/students?per_page=1</c>) purely to prove the token and school ID
        /// are accepted — mirroring <see cref="TestApiKey"/> for AI providers.
        /// </summary>
        private async Task TestSisConnectionAsync()
        {
            FlushWondeCredentials();
            string token = ConvertToPlainString(_wondeSecureToken);

            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(WondeSchoolId))
            {
                SisTestStatus = "⚠️ Enter both the Wonde API token and your school ID first.";
                return;
            }

            IsSyncingSis = true;
            SisTestStatus = "Testing connection to Wonde...";
            try
            {
                using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {token}");
                var response = await client.GetAsync(
                    $"{WondeSchoolDatabaseService.BaseUrl}/schools/{Uri.EscapeDataString(WondeSchoolId)}/students?per_page=1");

                SisTestStatus = response.IsSuccessStatusCode
                    ? "✅ Connected — Wonde accepted the token for this school."
                    : $"❌ Wonde rejected the request (HTTP {(int)response.StatusCode}). Check the token, school ID, and that your school has approved access.";
            }
            catch (Exception ex)
            {
                SisTestStatus = $"❌ Network error: {ex.Message}";
            }
            finally
            {
                IsSyncingSis = false;
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

        /// <summary>
        /// Manual "Sync Now" for the School Connection tab. For the CSV connector this *is* the
        /// import: pick the school's exported file, parse it, load every row into the encrypted
        /// cache, and auto-match roster profiles by name. For Wonde it refreshes cached stats for
        /// roster students that already have a matched external ID (never a whole-school mirror).
        /// </summary>
        private async Task SyncNowAsync()
        {
            if (IsCsvProviderSelected)
            {
                ImportSchoolDataCsv();
                return;
            }

            if (!IsWondeSelected)
            {
                MessageBox.Show("No SIS connection is configured, so there is nothing to sync. Choose a school data provider first.", "Nothing to Sync", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            FlushWondeCredentials();
            var studentsWithIds = (StudentDatabaseService.LoadStudents() ?? new System.Collections.Generic.List<StudentProfile>())
                .Where(s => !string.IsNullOrWhiteSpace(s.ExternalStudentId))
                .ToList();

            if (studentsWithIds.Count == 0)
            {
                MessageBox.Show("None of your roster students have an External ID (Wonde student ID) yet. Add IDs on the student profiles (or import a roster CSV with an ID column) so the app knows which records to fetch.", "No Matched Students", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            IsSyncingSis = true;
            SisTestStatus = $"Syncing 0 of {studentsWithIds.Count}...";
            try
            {
                var progress = new Progress<(int Done, int Total)>(p => SisTestStatus = $"Syncing {p.Done} of {p.Total}...");
                var (refreshed, failed) = await _schoolData.RefreshAllAsync(studentsWithIds, progress);

                _appState.CurrentSettings.LastSisSyncUtc = DateTime.UtcNow;
                _appState.SaveSettings();
                OnPropertyChanged(nameof(LastSisSyncDisplay));
                SisTestStatus = failed == 0
                    ? $"✅ Synced {refreshed} student(s) from Wonde."
                    : $"Synced {refreshed} student(s); {failed} could not be fetched (see the log for details).";
            }
            catch (Exception ex)
            {
                SisTestStatus = $"❌ Sync failed: {ex.Message}";
            }
            finally
            {
                IsSyncingSis = false;
            }
        }

        /// <summary>
        /// CSV-connector import: parses the picked file via <see cref="SisCsvImportService"/>,
        /// upserts every row into the DPAPI-encrypted cache stamped with the import time, and
        /// auto-fills <see cref="StudentProfile.ExternalStudentId"/> on roster profiles whose name
        /// matches a row — so reports can be grounded in the data immediately.
        /// </summary>
        private void ImportSchoolDataCsv()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "CSV Files (*.csv)|*.csv" };
            if (dialog.ShowDialog() != true) return;

            try
            {
                var lines = File.ReadAllLines(dialog.FileName);
                var parsed = SisCsvImportService.Parse(lines);

                if (parsed.Rows.Count == 0)
                {
                    MessageBox.Show(
                        "No usable rows were found.\n\n" + string.Join("\n", parsed.Warnings.Take(6)) +
                        "\n\nExpected columns (any order): ExternalStudentId (required), Name, AttendancePercent, BehaviourPoints, Grades (e.g. \"Maths=6; Science=7\"), SupportPlan, TargetGrade.",
                        "Import Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int retention = _appState.CurrentSettings.SisCacheRetentionDays;
                var importTime = DateTime.UtcNow;
                foreach (var row in parsed.Rows)
                {
                    row.Stats.LastSyncedUtc = importTime;
                    SchoolDataCacheService.UpsertStats(row.Stats, retention);
                }

                var roster = StudentDatabaseService.LoadStudents() ?? new System.Collections.Generic.List<StudentProfile>();
                int matched = SisCsvImportService.MatchRoster(parsed.Rows, roster);
                if (matched > 0) StudentDatabaseService.SaveStudents(roster);

                _appState.CurrentSettings.LastSisSyncUtc = importTime;
                _appState.CurrentSettings.SisCsvLastImportFile = Path.GetFileName(dialog.FileName);
                _appState.SaveSettings();
                OnPropertyChanged(nameof(LastSisSyncDisplay));
                OnPropertyChanged(nameof(SisCsvLastImportDisplay));

                string summary = $"Imported {parsed.Rows.Count} student record(s) into the encrypted local cache.\n" +
                                 $"Auto-matched {matched} roster profile(s) by name.";
                if (parsed.Warnings.Count > 0)
                    summary += $"\n\n{parsed.Warnings.Count} warning(s):\n" + string.Join("\n", parsed.Warnings.Take(6));
                MessageBox.Show(summary, "School Data Imported", MessageBoxButton.OK, MessageBoxImage.Information);
                Serilog.Log.Information("SIS CSV import: file={File} rows={Rows} matched={Matched} user={User}",
                    Path.GetFileName(dialog.FileName), parsed.Rows.Count, matched, Environment.UserName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"The file could not be read: {ex.Message}", "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        /// <summary>
        /// Overwrites every theme <see cref="SolidColorBrush"/> resource in
        /// <c>Application.Current.Resources</c> with the chosen palette (base palettes live in
        /// <see cref="ThemePaletteService"/>), then layers on the personalisation: the optional
        /// background tint mixed into app/card backgrounds, and the accent family derived from the
        /// teacher's chosen colour — which also re-skins the primary buttons. Because every themed
        /// control binds via <c>DynamicResource</c> (not <c>StaticResource</c>), this single call
        /// re-themes the entire UI instantly, with no window reload.
        /// </summary>
        private void ApplyTheme(bool isDark)
        {
            var settings = _appState.CurrentSettings;
            var palette = isDark ? ThemePaletteService.DarkPalette : ThemePaletteService.LightPalette;
            var res = System.Windows.Application.Current.Resources;
            var tint = ThemePaletteService.TryParseHex(settings.BackgroundTintHex);

            foreach (var entry in palette)
            {
                var color = (Color)ColorConverter.ConvertFromString(entry.Value);
                if (tint.HasValue && entry.Key is "ThemeAppBg" or "ThemeCardBg" or "ThemePreviewBg")
                    color = ThemePaletteService.ApplyBackgroundTint(color, tint.Value);
                res[entry.Key] = new SolidColorBrush(color);
            }

            // Accent family: fall back to the default purple if the stored hex is ever corrupt
            var accent = ThemePaletteService.TryParseHex(settings.ThemeColorHex)
                         ?? (Color)ColorConverter.ConvertFromString("#FF392A4C");
            var accents = ThemePaletteService.BuildAccentPalette(accent, isDark);
            foreach (var entry in accents)
                res[entry.Key] = new SolidColorBrush(entry.Value);

            // Primary buttons follow the accent rather than staying app-green
            res["ThemePrimaryBtnBg"] = new SolidColorBrush(accents["ThemeAccent"]);
            res["ThemePrimaryBtnHoverBg"] = new SolidColorBrush(accents["ThemeAccentHover"]);
        }
    }
}