using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public class ComboBoxItemWrapper
    {
        public string Content { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public override string ToString() => Content;
    }

    public class MainViewModel : ViewModelBase
    {
        private static readonly HttpClient _sharedHttpClient = new HttpClient();
        private CancellationTokenSource? _batchCancellationTokenSource;

        // Core State Properties
        private AppSettings _currentSettings = new AppSettings();
        private ObservableCollection<SessionRecord> _sessionHistory = new ObservableCollection<SessionRecord>();
        private List<StudentProfile> _studentDatabase = new List<StudentProfile>();
        private ObservableCollection<string> _studentNames = new ObservableCollection<string>();
        private bool _isSettingsUnlocked = false;
        private string _statusText = "Ready.";
        private bool _isGenerating = false;
        private SessionRecord? _selectedHistoryItem;

        // Form Fields (Single Report)
        private string _selectedStudentName = string.Empty;
        private string _studentClass = string.Empty;
        private string _targetGrade = string.Empty;
        private string _supportNeeds = string.Empty;
        private string _parentEmail = string.Empty;
        private int _targetWordCount = 150;
        private ReportFramework? _selectedFramework;
        private string _selectedCurriculumTopic = string.Empty;
        private string _customNotes = string.Empty;
        private string _generatedReportOutput = string.Empty;

        // Attendance & Contributions Form State
        private bool _isTimekeepingPerfect = true;
        private bool _isTimekeepingGood = false;
        private bool _isTimekeepingPoor = false;

        private bool _isContributionEnthusiastic = true;
        private bool _isContributionOccasional = false;
        private bool _isContributionRare = false;
        private bool _isContributionNever = false;

        // Batch Mode Form State
        private string _batchDataInput = string.Empty;
        private bool _isBatchModeActive = false;

        // Compare Suite Form State
        private string _compareStudentName = string.Empty;
        private string _compareNotes = string.Empty;
        private string _compareProvider1 = "Google Gemini";
        private string _compareProvider2 = "NVIDIA NIM (Free)";
        private string _compareOutputRight = string.Empty;
        private bool _isCompareRightVisible = false;

        // Settings Form fields
        private string _settingsSchoolName = string.Empty;
        private string _settingsTeacherName = string.Empty;
        private string _settingsSmtpEmail = string.Empty;
        private string _settingsSmtpPassword = string.Empty;
        private string _settingsMasterPassword = string.Empty;
        private string _settingsUnlockPassword = string.Empty;
        private ComboBoxItemWrapper? _selectedThemeColor;
        private string _settingsNewFrameworkName = string.Empty;
        private string _settingsNewFrameworkInstruction = string.Empty;
        private string _dynamicApiKeyLabel = "NVIDIA Key:";
        private string _dynamicApiKeyPassword = string.Empty;
        private string _selectedModelTier = string.Empty;
        private ObservableCollection<ComboBoxItemWrapper> _modelTierOptions = new ObservableCollection<ComboBoxItemWrapper>();

        // Navigation state mapping
        private int _selectedNavigationIndex = 0;

        // UI Visuals (Branding & Theme)
        private Brush _navBarBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF392A4C"));
        private string _mainAppTitle = "AI STUDENT REPORT GENERATOR";
        private ImageSource? _schoolLogoImage;
        private bool _isLogoVisible = false;
        private bool _isWelcomeOverlayVisible = true;
        private bool _isProfileSetupVisible = false;
        private bool _isWelcomeBackVisible = false;

        // Dashboard/Metrics binding strings
        private string _hoursSavedDisplay = "0.0";
        private string _tokensUsedDisplay = "0";
        private string _nvidiaCountDisplay = "0";
        private string _geminiCountDisplay = "0";
        private string _openaiCountDisplay = "0";
        private string _claudeCountDisplay = "0";

        // Commands
        public ICommand GenerateSingleCommand { get; }
        public ICommand SaveStudentCommand { get; }
        public ICommand DeleteStudentCommand { get; }
        public ICommand SaveProfileSettingsCommand { get; }
        public ICommand UnlockSettingsCommand { get; }
        public ICommand EnterAppCommand { get; }
        public ICommand EditWelcomeProfileCommand { get; }
        public ICommand UploadLogoCommand { get; }
        public ICommand CopyReportCommand { get; }
        public ICommand SaveWordCommand { get; }
        public ICommand SavePdfCommand { get; }
        public ICommand EmailReportCommand { get; }
        public ICommand ImportBatchCsvCommand { get; set; }
        public ICommand GenerateBatchCommand { get; }
        public ICommand CancelBatchCommand { get; }
        public ICommand ExportBatchWordCommand { get; }
        public ICommand RunComparisonCommand { get; }
        public ICommand ImportRosterCommand { get; }
        public ICommand ViewStudentHistoryCommand { get; }
        public ICommand ShowAllHistoryCommand { get; }
        public ICommand SaveCurriculumCommand { get; }
        public ICommand DeleteCurriculumCommand { get; }
        public ICommand AddFrameworkCommand { get; }
        public ICommand PreviewToneCommand { get; }
        public ICommand CompareHistoryCommand { get; }

        public MainViewModel()
        {
            GenerateSingleCommand = new RelayCommand(_ => Task.Run(GenerateSingleReportAsync));
            SaveStudentCommand = new RelayCommand(_ => SaveStudent());
            DeleteStudentCommand = new RelayCommand(_ => DeleteStudent());
            SaveProfileSettingsCommand = new RelayCommand(_ => SaveProfileSettings());
            UnlockSettingsCommand = new RelayCommand(_ => UnlockSettings());
            EnterAppCommand = new RelayCommand(_ => EnterApplication());
            EditWelcomeProfileCommand = new RelayCommand(_ => EditWelcomeProfile());
            UploadLogoCommand = new RelayCommand(_ => UploadLogo());
            CopyReportCommand = new RelayCommand(_ => CopyReportToClipboard());
            SaveWordCommand = new RelayCommand(_ => SaveAsWord());
            SavePdfCommand = new RelayCommand(_ => SaveAsPdf());
            ImportBatchCsvCommand = new RelayCommand(_ => ImportBatchCsv());
            EmailReportCommand = new RelayCommand(_ => Task.Run(EmailReportAsync));
            GenerateBatchCommand = new RelayCommand(_ => Task.Run(GenerateBatchAsync));
            CancelBatchCommand = new RelayCommand(_ => CancelBatchGeneration());
            ExportBatchWordCommand = new RelayCommand(_ => ExportBatchWord());
            RunComparisonCommand = new RelayCommand(_ => Task.Run(RunSideBySideComparisonAsync));
            ImportRosterCommand = new RelayCommand(_ => ImportRosterCsv());
            ViewStudentHistoryCommand = new RelayCommand(_ => FilterHistoryByStudent());
            ShowAllHistoryCommand = new RelayCommand(_ => ClearHistoryFilter());
            SaveCurriculumCommand = new RelayCommand(_ => SaveCurriculumTopic());
            DeleteCurriculumCommand = new RelayCommand(_ => DeleteCurriculumTopic());
            AddFrameworkCommand = new RelayCommand(_ => AddCustomFrameworkTemplate());
            PreviewToneCommand = new RelayCommand(_ => Task.Run(PreviewToneAsync));
            CompareHistoryCommand = new RelayCommand(_ => CopyHistoryPreviewToCompareBox());

            _sessionHistory = HistoryDatabaseService.LoadHistory() ?? new ObservableCollection<SessionRecord>();
            _studentDatabase = StudentDatabaseService.LoadStudents() ?? new List<StudentProfile>();

            InitializeSettings();
        }

        #region Initialization Routines
        private void InitializeSettings()
        {
            _currentSettings = SecureSettingsService.LoadSettings() ?? new AppSettings();
            IsWelcomeOverlayVisible = true;

            if (string.IsNullOrWhiteSpace(_currentSettings.TeacherSignoff) || _currentSettings.TeacherSignoff == "Mr. / Ms. Teacher")
            {
                IsProfileSetupVisible = true;
                IsWelcomeBackVisible = false;
            }
            else
            {
                IsProfileSetupVisible = false;
                IsWelcomeBackVisible = true;
            }

            SettingsSchoolName = _currentSettings.SchoolName;
            SettingsTeacherName = _currentSettings.TeacherSignoff;
            SettingsSmtpEmail = _currentSettings.SmtpEmail;
            SettingsSmtpPassword = _currentSettings.SmtpPassword;
            SettingsMasterPassword = _currentSettings.MasterPassword;
            IsDarkMode = _currentSettings.IsDarkMode;

            ApplyBrandingConfiguration();
            EvaluateAiProviderOptions(_currentSettings.AiProvider);
            UpdateDashboardMetricsDisplay();
            RefreshCollections();
        }

        private void RefreshCollections()
        {
            StudentNames = new ObservableCollection<string>(_studentDatabase.Select(x => x.FullName).ToList());
        }

        private void EvaluateAiProviderOptions(string provider)
        {
            string cleanProvider = SanitizeControlOutput(provider);
            ModelTierOptions.Clear();
            if (cleanProvider.Contains("NVIDIA"))
            {
                DynamicApiKeyLabel = "NVIDIA Key:";
                DynamicApiKeyPassword = _currentSettings.NvidiaApiKey;
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Llama 3.1 405B (Smarter)", Tag = "meta/llama-3.1-405b-instruct" });
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Llama 3.1 70B (Balanced)", Tag = "meta/llama-3.1-70b-instruct" });
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Nemotron 70B (NVIDIA)", Tag = "nvidia/nemotron-4-340b-instruct" });
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Mistral Large (Fast)", Tag = "mistralai/mistral-large-2-instruct" });
                SelectedModelTier = _currentSettings.NvidiaModelTier;
            }
            else if (cleanProvider.Contains("Gemini"))
            {
                DynamicApiKeyLabel = "Gemini Key:";
                DynamicApiKeyPassword = _currentSettings.GeminiApiKey;
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Gemini 2.5 Flash", Tag = "gemini-2.5-flash" });
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Gemini 2.5 Pro", Tag = "gemini-2.5-pro" });
                SelectedModelTier = _currentSettings.GeminiModelTier;
            }
            else if (cleanProvider.Contains("OpenAI"))
            {
                DynamicApiKeyLabel = "OpenAI Key:";
                DynamicApiKeyPassword = _currentSettings.OpenAiApiKey;
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "GPT-4o Mini", Tag = "gpt-4o-mini" });
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "GPT-4o", Tag = "gpt-4o" });
                SelectedModelTier = _currentSettings.OpenAiModelTier;
            }
            else if (cleanProvider.Contains("Claude"))
            {
                DynamicApiKeyLabel = "Claude Key:";
                DynamicApiKeyPassword = _currentSettings.ClaudeApiKey;
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Claude 3 Haiku", Tag = "claude-3-haiku-20240307" });
                ModelTierOptions.Add(new ComboBoxItemWrapper { Content = "Claude 3.5 Sonnet", Tag = "claude-3-5-sonnet-20240620" });
                SelectedModelTier = _currentSettings.ClaudeModelTier;
            }
        }

        private void UpdateDashboardMetricsDisplay()
        {
            double totalMinutesSaved = _currentSettings.TotalReportsGenerated * 5.0;
            HoursSavedDisplay = Math.Round(totalMinutesSaved / 60.0, 1).ToString();
            TokensUsedDisplay = _currentSettings.TotalTokensEstimated.ToString("N0");
            NvidiaCountDisplay = _currentSettings.NvidiaReportsCount.ToString("N0");
            GeminiCountDisplay = _currentSettings.GeminiReportsCount.ToString("N0");
            OpenaiCountDisplay = _currentSettings.OpenAiReportsCount.ToString("N0");
            ClaudeCountDisplay = _currentSettings.ClaudeReportsCount.ToString("N0");
        }

        private void ApplyBrandingConfiguration()
        {
            if (!string.IsNullOrWhiteSpace(_currentSettings.ThemeColorHex))
            {
                try
                {
                    NavBarBackground = (SolidColorBrush)new BrushConverter().ConvertFrom(_currentSettings.ThemeColorHex);
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(_currentSettings.SchoolName) && _currentSettings.SchoolName != "Enter School Name")
            {
                MainAppTitle = _currentSettings.SchoolName.ToUpper() + " REPORT GENERATOR";
            }

            if (!string.IsNullOrWhiteSpace(_currentSettings.SchoolLogoPath) && File.Exists(_currentSettings.SchoolLogoPath))
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(_currentSettings.SchoolLogoPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    SchoolLogoImage = bitmap;
                    IsLogoVisible = true;
                }
                catch { IsLogoVisible = false; }
            }
            else
            {
                IsLogoVisible = false;
            }
        }

        private string SanitizeControlOutput(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return string.Empty;
            if (source.Contains("System.Windows.Controls.ComboBoxItem:"))
            {
                return source.Replace("System.Windows.Controls.ComboBoxItem:", "").Trim();
            }
            return source.Trim();
        }
        #endregion

        #region MVVM Logic Actions Implementation
        private async Task GenerateSingleReportAsync()
        {
            string cleanTopic = SanitizeControlOutput(SelectedCurriculumTopic);
            string compiledNotes = $"Curriculum Topic Studied: {cleanTopic}\n\n";
            compiledNotes += "Attendance & Timekeeping: ";
            if (IsTimekeepingPerfect) compiledNotes += "100% attendance.\n";
            else if (IsTimekeepingGood) compiledNotes += "Good overall, but has occasional absences.\n";
            else if (IsTimekeepingPoor) compiledNotes += "Struggles with attendance; several unauthorised absences.\n";

            compiledNotes += "Class Contributions: ";
            if (IsContributionEnthusiastic) compiledNotes += "Contributes enthusiastically in class.\n";
            else if (IsContributionOccasional) compiledNotes += "Occasionally contributes.\n";
            else if (IsContributionRare) compiledNotes += "Contributes rarely, but always with high quality responses.\n";
            else if (IsContributionNever) compiledNotes += "Never contributes in class.\n";

            if (!string.IsNullOrWhiteSpace(CustomNotes))
                compiledNotes += $"\nAdditional Teacher Notes: {CustomNotes}";

            IsCompareRightVisible = false;
            StatusText = "Generating report...";

            await ProcessSingleReportExecutionAsync(SanitizeControlOutput(SelectedStudentName), compiledNotes, _currentSettings.AiProvider, report => GeneratedReportOutput = report);
        }

        private async Task<bool> ProcessSingleReportExecutionAsync(string name, string notes, string provider, Action<string> onCompleteOutput)
        {
            IsGenerating = true;
            string activeKey = string.Empty;
            string activeModel = SanitizeControlOutput(SelectedModelTier);
            string cleanProvider = SanitizeControlOutput(provider);
            IAiService activeAiEngine;

            if (cleanProvider.Contains("NVIDIA")) { activeKey = _currentSettings.NvidiaApiKey; activeAiEngine = new NvidiaReportService(_sharedHttpClient, activeKey); }
            else if (cleanProvider.Contains("OpenAI")) { activeKey = _currentSettings.OpenAiApiKey; activeAiEngine = new OpenAiReportService(_sharedHttpClient, activeKey); }
            else if (cleanProvider.Contains("Claude")) { activeKey = _currentSettings.ClaudeApiKey; activeAiEngine = new ClaudeReportService(_sharedHttpClient, activeKey); }
            else { activeKey = _currentSettings.GeminiApiKey; activeAiEngine = new GeminiReportService(_sharedHttpClient, activeKey); }

            if (string.IsNullOrWhiteSpace(activeKey))
            {
                onCompleteOutput("ERROR: Missing API Key inside configuration profiles.");
                IsGenerating = false;
                return false;
            }

            string dbTargetGrade = string.Empty;
            string dbSupportNeeds = string.Empty;
            var match = _studentDatabase.FirstOrDefault(s => s.FullName.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                dbTargetGrade = match.TargetGrade;
                dbSupportNeeds = match.SupportNeeds;
            }

            var request = new ReportRequest
            {
                StudentName = name,
                Subject = SanitizeControlOutput(SelectedCurriculumTopic),
                WordCount = TargetWordCount,
                RawNotes = notes,
                SelectedFramework = SelectedFramework?.Instruction ?? string.Empty,
                SchoolName = _currentSettings.SchoolName,
                TeacherSignoff = _currentSettings.TeacherSignoff,
                SelectedModel = activeModel,
                TargetGrade = dbTargetGrade,
                SupportNeeds = dbSupportNeeds
            };

            var response = await activeAiEngine.GenerateReportAsync(request);
            IsGenerating = false;

            if (response.IsSuccess)
            {
                onCompleteOutput(response.GeneratedReport);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    SessionHistory.Insert(0, new SessionRecord
                    {
                        StudentName = request.StudentName,
                        GeneratedReport = response.GeneratedReport,
                        Timestamp = DateTime.Now
                    });
                });

                HistoryDatabaseService.SaveHistory(SessionHistory);

                int words = response.GeneratedReport.Split(' ').Length;
                _currentSettings.TotalTokensEstimated += (long)(words * 1.3);
                _currentSettings.TotalReportsGenerated++;

                if (cleanProvider.Contains("NVIDIA")) _currentSettings.NvidiaReportsCount++;
                else if (cleanProvider.Contains("OpenAI")) _currentSettings.OpenAiReportsCount++;
                else if (cleanProvider.Contains("Claude")) _currentSettings.ClaudeReportsCount++;
                else _currentSettings.GeminiReportsCount++;

                SecureSettingsService.SaveSettings(_currentSettings);
                UpdateDashboardMetricsDisplay();
                StatusText = "Ready.";
                return true;
            }

            onCompleteOutput($"API Error: {response.ErrorMessage}");
            StatusText = "Generation failed.";
            return false;
        }

        private async Task GenerateBatchAsync()
        {
            if (string.IsNullOrWhiteSpace(BatchDataInput)) return;

            IsBatchModeActive = true;
            IsCompareRightVisible = false;
            GeneratedReportOutput = "Starting batch generation process...\n";

            var lines = BatchDataInput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int successCount = 0;

            _batchCancellationTokenSource = new CancellationTokenSource();
            var token = _batchCancellationTokenSource.Token;

            try
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        GeneratedReportOutput += "\n\n⚠️ BATCH CANCELLED BY USER.";
                        break;
                    }

                    var parts = lines[i].Split('|');
                    if (parts.Length < 2) continue;

                    StatusText = $"Generating report for {parts[0].Trim()} ({i + 1} of {lines.Length})...";

                    bool success = await ProcessSingleReportExecutionAsync(parts[0].Trim(), parts[1].Trim(), _currentSettings.AiProvider, report => GeneratedReportOutput = report);
                    if (success) successCount++;

                    if (i < lines.Length - 1 && !token.IsCancellationRequested)
                    {
                        await Task.Delay(1500, token);
                    }
                }
                StatusText = token.IsCancellationRequested ? $"Batch Stopped. {successCount} reports generated." : $"Batch complete! {successCount} reports written.";
            }
            catch (TaskCanceledException)
            {
                StatusText = $"Batch Stopped. {successCount} reports generated.";
            }
            finally
            {
                IsBatchModeActive = false;
                _batchCancellationTokenSource?.Dispose();
                _batchCancellationTokenSource = null;
            }
        }

        private void CancelBatchGeneration()
        {
            if (_batchCancellationTokenSource != null && !_batchCancellationTokenSource.IsCancellationRequested)
            {
                _batchCancellationTokenSource.Cancel();
                StatusText = "Stopping batch loop generation...";
            }
        }

        private async Task RunSideBySideComparisonAsync()
        {
            if (string.IsNullOrWhiteSpace(CompareStudentName) || string.IsNullOrWhiteSpace(CompareNotes)) return;

            IsCompareRightVisible = true;
            GeneratedReportOutput = $"Awaiting {CompareProvider1}...\n";
            CompareOutputRight = $"Awaiting {CompareProvider2}...\n";
            StatusText = "Running simultaneous dual engine generation...";

            var task1 = ProcessSingleReportExecutionAsync(CompareStudentName, CompareNotes, CompareProvider1, out1 => GeneratedReportOutput = out1);
            var task2 = ProcessSingleReportExecutionAsync(CompareStudentName, CompareNotes, CompareProvider2, out2 => CompareOutputRight = out2);

            await Task.WhenAll(task1, task2);
            StatusText = "Comparison analysis completed.";
        }

        private async Task EmailReportAsync()
        {
            // Operational validation fix: Explicit validation handles unconfigured mail configurations gracefully
            if (string.IsNullOrWhiteSpace(_currentSettings.SmtpEmail) || string.IsNullOrWhiteSpace(_currentSettings.SmtpPassword))
            {
                StatusText = "Email failed: Set up SMTP details in Profile Settings.";
                MessageBox.Show("SMTP Outbox details are missing. Please go to Profile Settings to input your school email credentials.", "Setup Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(GeneratedReportOutput) || string.IsNullOrWhiteSpace(ParentEmail))
            {
                StatusText = "Email failed: Missing text or parent address.";
                return;
            }

            StatusText = "Sending email to parent...";
            try
            {
                string subject = $"Official Student Report for {SelectedStudentName}";
                await EmailService.SendEmailAsync(ParentEmail, subject, GeneratedReportOutput,
                                                  _currentSettings.SmtpServer, _currentSettings.SmtpPort,
                                                  _currentSettings.SmtpEmail, _currentSettings.SmtpPassword);
                StatusText = "Email sent successfully!";
            }
            catch (Exception ex)
            {
                StatusText = $"Email failed: {ex.Message}";
            }
        }

        private async Task PreviewToneAsync()
        {
            if (SelectedFramework == null) return;
            StatusText = "Generating brief tone framework sample snippet...";

            var request = new ReportRequest
            {
                StudentName = "Student",
                Subject = "General",
                WordCount = 30,
                RawNotes = "Student did a good job this term.",
                SelectedFramework = SelectedFramework.Instruction + " IMPORTANT: Write exactly ONE sentence demonstrating this tone.",
                SchoolName = _currentSettings.SchoolName,
                TeacherSignoff = _currentSettings.TeacherSignoff,
                SelectedModel = SelectedModelTier
            };

            IAiService activeAiEngine;
            string provider = SanitizeControlOutput(_currentSettings.AiProvider);
            string key = provider.Contains("NVIDIA") ? _currentSettings.NvidiaApiKey :
                         provider.Contains("OpenAI") ? _currentSettings.OpenAiApiKey :
                         provider.Contains("Claude") ? _currentSettings.ClaudeApiKey : _currentSettings.GeminiApiKey;

            if (provider.Contains("NVIDIA")) activeAiEngine = new NvidiaReportService(_sharedHttpClient, key);
            else if (provider.Contains("OpenAI")) activeAiEngine = new OpenAiReportService(_sharedHttpClient, key);
            else if (provider.Contains("Claude")) activeAiEngine = new ClaudeReportService(_sharedHttpClient, key);
            else activeAiEngine = new GeminiReportService(_sharedHttpClient, key);

            var resp = await activeAiEngine.GenerateReportAsync(request);
            if (resp.IsSuccess)
            {
                MessageBox.Show($"Tone Preview Sample ({SelectedFramework.Name}):\n\n\"{resp.GeneratedReport}\"", "Tone Alignment Verification", MessageBoxButton.OK, MessageBoxImage.Information);
                StatusText = "Tone style verified.";
            }
            else
            {
                StatusText = "Failed to pull tone validation snapshot.";
            }
        }

        private void SaveStudent()
        {
            string cleanName = SanitizeControlOutput(SelectedStudentName);
            if (string.IsNullOrWhiteSpace(cleanName)) return;
            var match = _studentDatabase.FirstOrDefault(s => s.FullName.Equals(cleanName, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                _studentDatabase.Add(new StudentProfile
                {
                    FullName = cleanName,
                    ClassName = StudentClass,
                    ParentEmail = ParentEmail,
                    TargetGrade = TargetGrade,
                    SupportNeeds = SupportNeeds
                });
            }
            else
            {
                match.ClassName = StudentClass;
                match.ParentEmail = ParentEmail;
                match.TargetGrade = TargetGrade;
                match.SupportNeeds = SupportNeeds;
            }
            StudentDatabaseService.SaveStudents(_studentDatabase);
            RefreshCollections();
            StatusText = "Student profile saved.";
        }

        private void DeleteStudent()
        {
            string cleanName = SanitizeControlOutput(SelectedStudentName);
            var match = _studentDatabase.FirstOrDefault(s => s.FullName.Equals(cleanName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                _studentDatabase.Remove(match);
                StudentDatabaseService.SaveStudents(_studentDatabase);
                StudentClass = ParentEmail = TargetGrade = SupportNeeds = string.Empty;
                SelectedStudentName = string.Empty;
                RefreshCollections();
                StatusText = "Record deleted from student profiles cache database.";
            }
        }

        private void SaveProfileSettings()
        {
            _currentSettings.SchoolName = SettingsSchoolName;
            _currentSettings.TeacherSignoff = SettingsTeacherName;
            _currentSettings.SmtpEmail = SettingsSmtpEmail;
            _currentSettings.SmtpPassword = SettingsSmtpPassword;
            if (!string.IsNullOrEmpty(SettingsMasterPassword))
                _currentSettings.MasterPassword = SettingsMasterPassword;

            SecureSettingsService.SaveSettings(_currentSettings);
            ApplyBrandingConfiguration();
            StatusText = "Configurations saved successfully.";
        }

        private void UnlockSettings()
        {
            if (SettingsUnlockPassword == _currentSettings.MasterPassword || string.IsNullOrEmpty(_currentSettings.MasterPassword))
            {
                IsSettingsUnlocked = true;
                StatusText = "Security parameters vault unlocked.";
            }
            else
            {
                MessageBox.Show("Incorrect validation control shield password key.", "Security Exception", MessageBoxButton.OK, MessageBoxImage.Hand);
                SettingsUnlockPassword = string.Empty;
            }
        }

        private void EnterApplication()
        {
            if (IsProfileSetupVisible)
            {
                _currentSettings.TeacherSignoff = SettingsTeacherName;
                _currentSettings.SchoolName = SettingsSchoolName;
                SecureSettingsService.SaveSettings(_currentSettings);
                ApplyBrandingConfiguration();
            }
            IsWelcomeOverlayVisible = false;
        }

        private void EditWelcomeProfile()
        {
            IsWelcomeBackVisible = false;
            IsProfileSetupVisible = true;
        }

        private void UploadLogo()
        {
            var openDialog = new Microsoft.Win32.OpenFileDialog { Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg" };
            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string ext = Path.GetExtension(openDialog.FileName);
                    string target = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"school_logo{ext}");
                    File.Copy(openDialog.FileName, target, true);
                    _currentSettings.SchoolLogoPath = target;
                    SecureSettingsService.SaveSettings(_currentSettings);
                    ApplyBrandingConfiguration();
                }
                catch (Exception e) { MessageBox.Show($"File tracking error: {e.Message}"); }
            }
        }

        private void CopyReportToClipboard()
        {
            if (!string.IsNullOrWhiteSpace(GeneratedReportOutput))
            {
                Clipboard.SetText(GeneratedReportOutput);
                StatusText = "Report stored to clipboard context.";
            }
        }

        private void SaveAsWord()
        {
            if (string.IsNullOrWhiteSpace(GeneratedReportOutput)) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Word Document (*.docx)|*.docx", FileName = GetSmartFileName(".docx") };
            if (dialog.ShowDialog() == true)
            {
                WordExportService.ExportSingle(dialog.FileName, SanitizeControlOutput(SelectedStudentName), GeneratedReportOutput);
                StatusText = "Word document saved.";
            }
        }

        private void SaveAsPdf()
        {
            if (string.IsNullOrWhiteSpace(GeneratedReportOutput)) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "PDF Document (*.pdf)|*.pdf", FileName = GetSmartFileName(".pdf") };
            if (dialog.ShowDialog() == true)
            {
                PdfExportService.ExportSingle(dialog.FileName, SanitizeControlOutput(SelectedStudentName), GeneratedReportOutput);
                StatusText = "PDF document compiled.";
            }
        }

        private void ExportBatchWord()
        {
            if (SessionHistory.Count == 0) return;
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Word Document (*.docx)|*.docx", FileName = $"Batch_Class_Reports_{DateTime.Now:yyyyMMdd}.docx" };
            if (dialog.ShowDialog() == true)
            {
                WordExportService.ExportBatch(dialog.FileName, SessionHistory.ToList());
                StatusText = "Bulk class document portfolio exported successfully.";
            }
        }

        private void ImportBatchCsv()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "CSV Files (*.csv)|*.csv" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var lines = File.ReadAllLines(dialog.FileName);
                    BatchDataInput = string.Empty;
                    var parser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(Y?![^\"]*\"))");
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var parts = parser.Split(line).Select(s => s.Trim('"', ' ')).ToArray();
                        if (parts.Length >= 2)
                        {
                            if (parts[0].ToLower().Contains("name") && parts[1].ToLower().Contains("note")) continue;
                            BatchDataInput += $"{parts[0]} | {parts[1]}\n";
                        }
                    }
                    StatusText = "CSV text parameters parsed successfully.";
                }
                catch (Exception ex) { MessageBox.Show($"Parsing fault: {ex.Message}"); }
            }
        }

        private void ImportRosterCsv()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "CSV Files (*.csv)|*.csv" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var lines = File.ReadAllLines(dialog.FileName);
                    var parser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(Y?![^\"]*\"))");
                    int addedCount = 0;
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var parts = parser.Split(line).Select(s => s.Trim('"', ' ')).ToArray();
                        if (parts.Length >= 1)
                        {
                            string sName = parts[0];
                            if (sName.ToLower().Contains("name")) continue;
                            string sClass = parts.Length >= 2 ? parts[1] : string.Empty;
                            if (!_studentDatabase.Any(s => s.FullName.Equals(sName, StringComparison.OrdinalIgnoreCase)))
                            {
                                _studentDatabase.Add(new StudentProfile { FullName = sName, ClassName = sClass });
                                addedCount++;
                            }
                        }
                    }
                    StudentDatabaseService.SaveStudents(_studentDatabase);
                    RefreshCollections();
                    StatusText = $"Ingested {addedCount} records to student cache roster.";
                }
                catch (Exception ex) { MessageBox.Show($"Roster error: {ex.Message}"); }
            }
        }

        private void FilterHistoryByStudent()
        {
            string target = SanitizeControlOutput(SelectedStudentName);
            if (string.IsNullOrWhiteSpace(target)) return;
            var matchingHistory = HistoryDatabaseService.LoadHistory()
                .Where(r => r.StudentName.Contains(target, StringComparison.OrdinalIgnoreCase)).ToList();
            SessionHistory = new ObservableCollection<SessionRecord>(matchingHistory);
            StatusText = $"Filtering history logs for: {target}";
        }

        private void ClearHistoryFilter()
        {
            SessionHistory = HistoryDatabaseService.LoadHistory() ?? new ObservableCollection<SessionRecord>();
            StatusText = "Reset history timeline overview filter.";
        }

        private void SaveCurriculumTopic()
        {
            string topic = SanitizeControlOutput(SelectedCurriculumTopic);
            if (string.IsNullOrWhiteSpace(topic)) return;
            if (!_currentSettings.CurriculumTopics.Any(t => t.Equals(topic, StringComparison.OrdinalIgnoreCase)))
            {
                _currentSettings.CurriculumTopics.Add(topic);
                _currentSettings.CurriculumTopics.Sort();
                SecureSettingsService.SaveSettings(_currentSettings);
                OnPropertyChanged(nameof(CurriculumTopics));
                SelectedCurriculumTopic = topic;
            }
        }

        private void DeleteCurriculumTopic()
        {
            string topic = SanitizeControlOutput(SelectedCurriculumTopic);
            if (string.IsNullOrWhiteSpace(topic)) return;
            if (_currentSettings.CurriculumTopics.Contains(topic, StringComparer.OrdinalIgnoreCase))
            {
                var match = _currentSettings.CurriculumTopics.First(t => t.Equals(topic, StringComparison.OrdinalIgnoreCase));
                _currentSettings.CurriculumTopics.Remove(match);
                SecureSettingsService.SaveSettings(_currentSettings);
                OnPropertyChanged(nameof(CurriculumTopics));
                SelectedCurriculumTopic = string.Empty;
            }
        }

        private void AddCustomFrameworkTemplate()
        {
            if (!string.IsNullOrWhiteSpace(SettingsNewFrameworkName) && !string.IsNullOrWhiteSpace(SettingsNewFrameworkInstruction))
            {
                _currentSettings.CustomFrameworks.Add(new ReportFramework { Name = SettingsNewFrameworkName, Instruction = SettingsNewFrameworkInstruction });
                SecureSettingsService.SaveSettings(_currentSettings);
                OnPropertyChanged(nameof(CustomFrameworks));
                SettingsNewFrameworkName = SettingsNewFrameworkInstruction = string.Empty;
            }
        }

        private void CopyHistoryPreviewToCompareBox()
        {
            IsCompareRightVisible = true;
            if (SelectedHistoryItem != null)
            {
                CompareOutputRight = SelectedHistoryItem.GeneratedReport;
            }
            StatusText = "Ready to analyze historical comparison values.";
        }

        private string GetSmartFileName(string ext)
        {
            string sName = string.IsNullOrWhiteSpace(SelectedStudentName) ? "Student" : SanitizeControlOutput(SelectedStudentName).Replace(" ", "");
            string topic = string.IsNullOrWhiteSpace(SelectedCurriculumTopic) ? "Report" : SanitizeControlOutput(SelectedCurriculumTopic).Replace(" ", "");
            string date = DateTime.Now.ToString("yyyyMMdd");
            string combined = $"{sName}_{topic}_{date}{ext}";
            return string.Join("_", combined.Split(Path.GetInvalidFileNameChars()));
        }
        #endregion

        #region Standard Data Property Bindings Wrappers
        public ObservableCollection<SessionRecord> SessionHistory { get => _sessionHistory; set => SetProperty(ref _sessionHistory, value); }
        public ObservableCollection<string> StudentNames { get => _studentNames; set => SetProperty(ref _studentNames, value); }
        public List<ReportFramework> CustomFrameworks => _currentSettings.CustomFrameworks;
        public List<string> CurriculumTopics => _currentSettings.CurriculumTopics;

        public SessionRecord? SelectedHistoryItem
        {
            get => _selectedHistoryItem;
            set => SetProperty(ref _selectedHistoryItem, value);
        }

        public int SelectedNavigationIndex
        {
            get => _selectedNavigationIndex;
            set
            {
                if (SetProperty(ref _selectedNavigationIndex, value) && (_selectedNavigationIndex == 3 || _selectedNavigationIndex == 4))
                {
                    if (!string.IsNullOrEmpty(_currentSettings.MasterPassword) && !IsSettingsUnlocked)
                        IsSettingsUnlocked = false;
                }
            }
        }

        public string SelectedAiProvider
        {
            get => _currentSettings.AiProvider;
            set
            {
                string cleanValue = SanitizeControlOutput(value);
                if (_currentSettings.AiProvider != cleanValue)
                {
                    _currentSettings.AiProvider = cleanValue;
                    OnPropertyChanged();
                    EvaluateAiProviderOptions(cleanValue);
                }
            }
        }

        public string SelectedModelTier
        {
            get => _selectedModelTier;
            set
            {
                string cleanValue = SanitizeControlOutput(value);
                if (SetProperty(ref _selectedModelTier, cleanValue))
                {
                    string provider = SanitizeControlOutput(_currentSettings.AiProvider);
                    if (provider.Contains("NVIDIA")) _currentSettings.NvidiaModelTier = cleanValue;
                    else if (provider.Contains("Gemini")) _currentSettings.GeminiModelTier = cleanValue;
                    else if (provider.Contains("OpenAI")) _currentSettings.OpenAiModelTier = cleanValue;
                    else if (provider.Contains("Claude")) _currentSettings.ClaudeModelTier = cleanValue;
                    SecureSettingsService.SaveSettings(_currentSettings);
                }
            }
        }

        public string DynamicApiKeyPassword
        {
            get => _dynamicApiKeyPassword;
            set
            {
                if (SetProperty(ref _dynamicApiKeyPassword, value))
                {
                    string provider = SanitizeControlOutput(_currentSettings.AiProvider);
                    if (provider.Contains("NVIDIA")) _currentSettings.NvidiaApiKey = value;
                    else if (provider.Contains("Gemini")) _currentSettings.GeminiApiKey = value;
                    else if (provider.Contains("OpenAI")) _currentSettings.OpenAiApiKey = value;
                    else if (provider.Contains("Claude")) _currentSettings.ClaudeApiKey = value;
                    SecureSettingsService.SaveSettings(_currentSettings);
                }
            }
        }

        public bool IsDarkMode
        {
            get => _currentSettings.IsDarkMode;
            set
            {
                if (_currentSettings.IsDarkMode != value)
                {
                    _currentSettings.IsDarkMode = value;
                    OnPropertyChanged();
                    var mainWin = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    if (mainWin != null)
                    {
                        var type = mainWin.GetType();
                        var method = type.GetMethod("ApplyDarkMode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        method?.Invoke(mainWin, new object[] { value });
                    }
                    SecureSettingsService.SaveSettings(_currentSettings);
                }
            }
        }

        public string SelectedStudentName
        {
            get => _selectedStudentName;
            set
            {
                string cleanValue = SanitizeControlOutput(value);
                if (SetProperty(ref _selectedStudentName, cleanValue))
                {
                    var match = _studentDatabase.FirstOrDefault(x => x.FullName == cleanValue);
                    if (match != null)
                    {
                        StudentClass = match.ClassName;
                        ParentEmail = match.ParentEmail;
                        TargetGrade = match.TargetGrade;
                        SupportNeeds = match.SupportNeeds;
                    }
                }
            }
        }

        public string StudentClass { get => _studentClass; set => SetProperty(ref _studentClass, value); }
        public string TargetGrade { get => _targetGrade; set => SetProperty(ref _targetGrade, value); }
        public string SupportNeeds { get => _supportNeeds; set => SetProperty(ref _supportNeeds, value); }
        public string ParentEmail { get => _parentEmail; set => SetProperty(ref _parentEmail, value); }
        public int TargetWordCount { get => _targetWordCount; set => SetProperty(ref _targetWordCount, value); }
        public ReportFramework? SelectedFramework { get => _selectedFramework; set => SetProperty(ref _selectedFramework, value); }
        public string SelectedCurriculumTopic { get => _selectedCurriculumTopic; set => SetProperty(ref _selectedCurriculumTopic, value); }
        public string CustomNotes { get => _customNotes; set => SetProperty(ref _customNotes, value); }
        public string GeneratedReportOutput { get => _generatedReportOutput; set => SetProperty(ref _generatedReportOutput, value); }
        public bool IsTimekeepingPerfect { get => _isTimekeepingPerfect; set => SetProperty(ref _isTimekeepingPerfect, value); }
        public bool IsTimekeepingGood { get => _isTimekeepingGood; set => SetProperty(ref _isTimekeepingGood, value); }
        public bool IsTimekeepingPoor { get => _isTimekeepingPoor; set => SetProperty(ref _isTimekeepingPoor, value); }
        public bool IsContributionEnthusiastic { get => _isContributionEnthusiastic; set => SetProperty(ref _isContributionEnthusiastic, value); }
        public bool IsContributionOccasional { get => _isContributionOccasional; set => SetProperty(ref _isContributionOccasional, value); }
        public bool IsContributionRare { get => _isContributionRare; set => SetProperty(ref _isContributionRare, value); }
        public bool IsContributionNever { get => _isContributionNever; set => SetProperty(ref _isContributionNever, value); }
        public string BatchDataInput { get => _batchDataInput; set => SetProperty(ref _batchDataInput, value); }
        public bool IsBatchModeActive { get => _isBatchModeActive; set => SetProperty(ref _isBatchModeActive, value); }
        public string CompareStudentName { get => _compareStudentName; set => SetProperty(ref _compareStudentName, value); }
        public string CompareNotes { get => _compareNotes; set => SetProperty(ref _compareNotes, value); }
        public string CompareProvider1 { get => _compareProvider1; set => SetProperty(ref _compareProvider1, value); }
        public string CompareProvider2 { get => _compareProvider2; set => SetProperty(ref _compareProvider2, value); }
        public string CompareOutputRight { get => _compareOutputRight; set => SetProperty(ref _compareOutputRight, value); }
        public bool IsCompareRightVisible { get => _isCompareRightVisible; set => SetProperty(ref _isCompareRightVisible, value); }
        public string SettingsSchoolName { get => _settingsSchoolName; set => SetProperty(ref _settingsSchoolName, value); }
        public string SettingsTeacherName { get => _settingsTeacherName; set => SetProperty(ref _settingsTeacherName, value); }
        public string SettingsSmtpEmail { get => _settingsSmtpEmail; set => SetProperty(ref _settingsSmtpEmail, value); }
        public string SettingsSmtpPassword { get => _settingsSmtpPassword; set => SetProperty(ref _settingsSmtpPassword, value); }
        public string SettingsMasterPassword { get => _settingsMasterPassword; set => SetProperty(ref _settingsMasterPassword, value); }
        public string SettingsUnlockPassword { get => _settingsUnlockPassword; set => SetProperty(ref _settingsUnlockPassword, value); }
        public ComboBoxItemWrapper? SelectedThemeColor { get => _selectedThemeColor; set => SetProperty(ref _selectedThemeColor, value); }
        public string SettingsNewFrameworkName { get => _settingsNewFrameworkName; set => SetProperty(ref _settingsNewFrameworkName, value); }
        public string SettingsNewFrameworkInstruction { get => _settingsNewFrameworkInstruction; set => SetProperty(ref _settingsNewFrameworkInstruction, value); }
        public string DynamicApiKeyLabel { get => _dynamicApiKeyLabel; set => SetProperty(ref _dynamicApiKeyLabel, value); }
        public ObservableCollection<ComboBoxItemWrapper> ModelTierOptions { get => _modelTierOptions; set => SetProperty(ref _modelTierOptions, value); }
        public Brush NavBarBackground { get => _navBarBackground; set => SetProperty(ref _navBarBackground, value); }
        public string MainAppTitle { get => _mainAppTitle; set => SetProperty(ref _mainAppTitle, value); }
        public ImageSource? SchoolLogoImage { get => _schoolLogoImage; set => SetProperty(ref _schoolLogoImage, value); }
        public bool IsLogoVisible { get => _isLogoVisible; set => SetProperty(ref _isLogoVisible, value); }
        public bool IsWelcomeOverlayVisible { get => _isWelcomeOverlayVisible; set => SetProperty(ref _isWelcomeOverlayVisible, value); }
        public bool IsProfileSetupVisible { get => _isProfileSetupVisible; set => SetProperty(ref _isProfileSetupVisible, value); }
        public bool IsWelcomeBackVisible { get => _isWelcomeBackVisible; set => SetProperty(ref _isWelcomeBackVisible, value); }
        public bool IsSettingsUnlocked { get => _isSettingsUnlocked; set => SetProperty(ref _isSettingsUnlocked, value); }
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
        public bool IsGenerating { get => _isGenerating; set => SetProperty(ref _isGenerating, value); }
        public string HoursSavedDisplay { get => _hoursSavedDisplay; set => SetProperty(ref _hoursSavedDisplay, value); }
        public string TokensUsedDisplay { get => _tokensUsedDisplay; set => SetProperty(ref _tokensUsedDisplay, value); }
        public string NvidiaCountDisplay { get => _nvidiaCountDisplay; set => SetProperty(ref _nvidiaCountDisplay, value); }
        public string GeminiCountDisplay { get => _geminiCountDisplay; set => SetProperty(ref _geminiCountDisplay, value); }
        public string OpenaiCountDisplay { get => _openaiCountDisplay; set => SetProperty(ref _openaiCountDisplay, value); }
        public string ClaudeCountDisplay { get => _claudeCountDisplay; set => SetProperty(ref _claudeCountDisplay, value); }
        #endregion
    }
}