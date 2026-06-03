using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using StudentReportGenerator.Models;

namespace StudentReportGenerator.Services
{
    public class MainViewModel : ViewModelBase
    {
        private static readonly HttpClient _sharedHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private readonly AppStateService _appState;

        // Expose the Settings ViewModel so the UI can bind to it!
        public SettingsViewModel SettingsVM { get; }

        private CancellationTokenSource? _batchCancellationTokenSource;
        private ObservableCollection<SessionRecord> _sessionHistory = new ObservableCollection<SessionRecord>();
        private List<StudentProfile> _studentDatabase = new List<StudentProfile>();
        private ObservableCollection<string> _studentNames = new ObservableCollection<string>();
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

        private int _selectedNavigationIndex = 0;
        private bool _isWelcomeOverlayVisible = true;
        private bool _isProfileSetupVisible = false;
        private bool _isWelcomeBackVisible = false;

        private string _hoursSavedDisplay = "0.0";
        private string _tokensUsedDisplay = "0";
        private string _nvidiaCountDisplay = "0";
        private string _geminiCountDisplay = "0";
        private string _openaiCountDisplay = "0";
        private string _claudeCountDisplay = "0";

        // Commands Definitions
        public ICommand GenerateSingleCommand { get; }
        public ICommand SaveStudentCommand { get; }
        public ICommand DeleteStudentCommand { get; }
        public ICommand EnterAppCommand { get; }
        public ICommand EditWelcomeProfileCommand { get; }
        public ICommand CopyReportCommand { get; }
        public ICommand SaveWordCommand { get; }
        public ICommand SavePdfCommand { get; }
        public ICommand EmailReportCommand { get; }
        public ICommand ImportBatchCsvCommand { get; }
        public ICommand GenerateBatchCommand { get; }
        public ICommand CancelBatchCommand { get; }
        public ICommand ExportBatchWordCommand { get; }
        public ICommand RunComparisonCommand { get; }
        public ICommand ImportRosterCommand { get; }
        public ICommand ViewStudentHistoryCommand { get; }
        public ICommand ShowAllHistoryCommand { get; }
        public ICommand SaveCurriculumCommand { get; }
        public ICommand DeleteCurriculumCommand { get; }
        public ICommand PreviewToneCommand { get; }
        public ICommand CompareHistoryCommand { get; }
        public ICommand DeleteHistoryCommand { get; }

        public MainViewModel(AppStateService appState, SettingsViewModel settingsVM)
        {
            _appState = appState;
            SettingsVM = settingsVM;

            GenerateSingleCommand = new AsyncRelayCommand(_ => GenerateSingleReportAsync(), _ => !IsGenerating);
            SaveStudentCommand = new RelayCommand(_ => SaveStudent());
            DeleteStudentCommand = new RelayCommand(_ => DeleteStudent());
            EnterAppCommand = new RelayCommand(_ => EnterApplication());
            EditWelcomeProfileCommand = new RelayCommand(_ => EditWelcomeProfile());
            CopyReportCommand = new RelayCommand(_ => CopyReportToClipboard(), _ => !string.IsNullOrWhiteSpace(GeneratedReportOutput));
            SaveWordCommand = new RelayCommand(_ => SaveAsWord(), _ => !string.IsNullOrWhiteSpace(GeneratedReportOutput));
            SavePdfCommand = new RelayCommand(_ => SaveAsPdf(), _ => !string.IsNullOrWhiteSpace(GeneratedReportOutput));
            ImportBatchCsvCommand = new RelayCommand(_ => ImportBatchCsv());
            EmailReportCommand = new AsyncRelayCommand(_ => EmailReportAsync(), _ => !IsGenerating && !string.IsNullOrWhiteSpace(GeneratedReportOutput));
            GenerateBatchCommand = new AsyncRelayCommand(_ => GenerateBatchAsync(), _ => !_isBatchModeActive);
            CancelBatchCommand = new RelayCommand(_ => CancelBatchGeneration());
            ExportBatchWordCommand = new RelayCommand(_ => ExportBatchWord());
            RunComparisonCommand = new AsyncRelayCommand(_ => RunSideBySideComparisonAsync(), _ => !IsGenerating);
            ImportRosterCommand = new RelayCommand(_ => ImportRosterCsv());
            ViewStudentHistoryCommand = new RelayCommand(_ => FilterHistoryByStudent());
            ShowAllHistoryCommand = new RelayCommand(_ => ClearHistoryFilter());
            SaveCurriculumCommand = new RelayCommand(_ => SaveCurriculumTopic());
            DeleteCurriculumCommand = new RelayCommand(_ => DeleteCurriculumTopic());
            PreviewToneCommand = new AsyncRelayCommand(_ => PreviewToneAsync(), _ => !IsGenerating);
            CompareHistoryCommand = new RelayCommand(_ => CopyHistoryPreviewToCompareBox());
            DeleteHistoryCommand = new RelayCommand(DeleteHistoryRecord);

            _sessionHistory = HistoryDatabaseService.LoadHistory() ?? new ObservableCollection<SessionRecord>();
            _studentDatabase = StudentDatabaseService.LoadStudents() ?? new List<StudentProfile>();

            RefreshCollections();
            UpdateDashboardMetricsDisplay();

            IsWelcomeOverlayVisible = true;
            if (string.IsNullOrWhiteSpace(_appState.CurrentSettings.TeacherSignoff) || _appState.CurrentSettings.TeacherSignoff == "Mr. / Ms. Teacher")
            {
                IsProfileSetupVisible = true;
                IsWelcomeBackVisible = false;
            }
            else
            {
                IsProfileSetupVisible = false;
                IsWelcomeBackVisible = true;
            }
        }

        private void RefreshCollections()
        {
            StudentNames = new ObservableCollection<string>(_studentDatabase.Select(x => x.FullName).ToList());
        }

        private void UpdateDashboardMetricsDisplay()
        {
            double totalMinutesSaved = _appState.CurrentSettings.TotalReportsGenerated * 5.0;
            HoursSavedDisplay = Math.Round(totalMinutesSaved / 60.0, 1).ToString();
            TokensUsedDisplay = _appState.CurrentSettings.TotalTokensEstimated.ToString("N0");
            NvidiaCountDisplay = _appState.CurrentSettings.NvidiaReportsCount.ToString("N0");
            GeminiCountDisplay = _appState.CurrentSettings.GeminiReportsCount.ToString("N0");
            OpenaiCountDisplay = _appState.CurrentSettings.OpenAiReportsCount.ToString("N0");
            ClaudeCountDisplay = _appState.CurrentSettings.ClaudeReportsCount.ToString("N0");
        }

        private string SanitizeControlOutput(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return string.Empty;
            if (source.Contains("System.Windows.Controls.ComboBoxItem:"))
                return source.Replace("System.Windows.Controls.ComboBoxItem:", "").Trim();
            return source.Trim();
        }

        private async Task GenerateSingleReportAsync()
        {
            string cleanStudentName = SanitizeControlOutput(SelectedStudentName);
            string cleanTopic = SanitizeControlOutput(SelectedCurriculumTopic);

            if (string.IsNullOrWhiteSpace(cleanStudentName)) { MessageBox.Show("The student's name field cannot be empty.", "Missing Field", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (string.IsNullOrWhiteSpace(cleanTopic)) { MessageBox.Show("A curriculum topic must be designated to proceed.", "Missing Field", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            string compiledNotes = $"Curriculum Topic Studied: {cleanTopic}\n\nAttendance & Timekeeping: ";
            if (IsTimekeepingPerfect) compiledNotes += "100% attendance.\n";
            else if (IsTimekeepingGood) compiledNotes += "Good overall, but has occasional absences.\n";
            else if (IsTimekeepingPoor) compiledNotes += "Struggles with attendance; several unauthorised absences.\n";

            compiledNotes += "Class Contributions: ";
            if (IsContributionEnthusiastic) compiledNotes += "Contributes enthusiastically in class.\n";
            else if (IsContributionOccasional) compiledNotes += "Occasionally contributes.\n";
            else if (IsContributionRare) compiledNotes += "Contributes rarely, but always with high quality responses.\n";
            else if (IsContributionNever) compiledNotes += "Never contributes in class.\n";

            if (!string.IsNullOrWhiteSpace(CustomNotes)) compiledNotes += $"\nAdditional Teacher Notes: {CustomNotes}";

            IsCompareRightVisible = false;
            StatusText = "Generating report...";
            await ProcessSingleReportExecutionAsync(cleanStudentName, compiledNotes, _appState.CurrentSettings.AiProvider, report => GeneratedReportOutput = report);
        }

        private async Task<bool> ProcessSingleReportExecutionAsync(string name, string notes, string provider, Action<string> onCompleteOutput)
        {
            IsGenerating = true;
            string cleanProvider = SanitizeControlOutput(provider);
            string activeKey = string.Empty;
            string activeModel = string.Empty;
            IAiService activeAiEngine;

            if (cleanProvider.Contains("NVIDIA"))
            {
                activeKey = CryptoService.DecryptSecret(_appState.CurrentSettings.NvidiaApiKey);
                activeModel = _appState.CurrentSettings.NvidiaModelTier;
                activeAiEngine = new NvidiaReportService(_sharedHttpClient, activeKey);
            }
            else if (cleanProvider.Contains("OpenAI"))
            {
                activeKey = CryptoService.DecryptSecret(_appState.CurrentSettings.OpenAiApiKey);
                activeModel = _appState.CurrentSettings.OpenAiModelTier;
                activeAiEngine = new OpenAiReportService(_sharedHttpClient, activeKey);
            }
            else if (cleanProvider.Contains("Claude"))
            {
                activeKey = CryptoService.DecryptSecret(_appState.CurrentSettings.ClaudeApiKey);
                activeModel = _appState.CurrentSettings.ClaudeModelTier;
                activeAiEngine = new ClaudeReportService(_sharedHttpClient, activeKey);
            }
            else
            {
                activeKey = CryptoService.DecryptSecret(_appState.CurrentSettings.GeminiApiKey);
                activeModel = _appState.CurrentSettings.GeminiModelTier;
                activeAiEngine = new GeminiReportService(_sharedHttpClient, activeKey);
            }

            if (string.IsNullOrWhiteSpace(activeKey))
            {
                onCompleteOutput("ERROR: Missing API Key inside configuration profiles. Please check Settings.");
                IsGenerating = false;
                return false;
            }

            var match = _studentDatabase.FirstOrDefault(s => s.FullName.Equals(name, StringComparison.OrdinalIgnoreCase));
            var request = new ReportRequest
            {
                StudentName = name,
                Subject = SanitizeControlOutput(SelectedCurriculumTopic),
                WordCount = TargetWordCount,
                RawNotes = notes,
                SelectedFramework = SelectedFramework?.Instruction ?? string.Empty,
                SchoolName = _appState.CurrentSettings.SchoolName,
                TeacherSignoff = _appState.CurrentSettings.TeacherSignoff,
                SelectedModel = activeModel,
                TargetGrade = match?.TargetGrade ?? string.Empty,
                SupportNeeds = match?.SupportNeeds ?? string.Empty
            };

            var response = await activeAiEngine.GenerateReportAsync(request);
            IsGenerating = false;

            if (response.IsSuccess)
            {
                onCompleteOutput(response.GeneratedReport);
                SessionHistory.Insert(0, new SessionRecord { StudentName = request.StudentName, GeneratedReport = response.GeneratedReport, Timestamp = DateTime.Now });
                HistoryDatabaseService.SaveHistory(SessionHistory);

                int words = response.GeneratedReport.Split(' ').Length;
                _appState.CurrentSettings.TotalTokensEstimated += (long)(words * 1.3);
                _appState.CurrentSettings.TotalReportsGenerated++;

                if (cleanProvider.Contains("NVIDIA")) _appState.CurrentSettings.NvidiaReportsCount++;
                else if (cleanProvider.Contains("OpenAI")) _appState.CurrentSettings.OpenAiReportsCount++;
                else if (cleanProvider.Contains("Claude")) _appState.CurrentSettings.ClaudeReportsCount++;
                else _appState.CurrentSettings.GeminiReportsCount++;

                _appState.SaveSettings();
                UpdateDashboardMetricsDisplay();
                StatusText = "Ready.";
                return true;
            }

            onCompleteOutput(response.ErrorMessage);
            StatusText = "Error encountered.";
            return false;
        }

        private async Task PreviewToneAsync()
        {
            if (SelectedFramework == null) { MessageBox.Show("Please select a tone framework first."); return; }
            string notes = $"Generate a 2-sentence preview showing this tone. Subject: {SanitizeControlOutput(SelectedCurriculumTopic)}";
            IsCompareRightVisible = true;
            StatusText = "Previewing Tone...";
            await ProcessSingleReportExecutionAsync("Student Preview", notes, _appState.CurrentSettings.AiProvider, report => CompareOutputRight = report);
        }

        private void DeleteHistoryRecord(object? parameter)
        {
            if (parameter is SessionRecord record && SessionHistory.Contains(record))
            {
                SessionHistory.Remove(record);
                HistoryDatabaseService.SaveHistory(SessionHistory);
                StatusText = "History record deleted successfully.";
            }
        }

        private async Task GenerateBatchAsync()
        {
            if (string.IsNullOrWhiteSpace(BatchDataInput)) return;
            IsBatchModeActive = true;
            IsCompareRightVisible = false;
            GeneratedReportOutput = "Starting batch processing...\n";

            var lines = BatchDataInput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            _batchCancellationTokenSource = new CancellationTokenSource();
            var token = _batchCancellationTokenSource.Token;

            try
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    if (token.IsCancellationRequested) break;
                    if (!lines[i].Contains("|")) continue;

                    var parts = lines[i].Split('|');
                    string studentNameClean = parts[0].Trim();
                    string studentNotesClean = parts[1].Trim();

                    if (string.IsNullOrWhiteSpace(studentNameClean)) continue;

                    StatusText = $"Generating batch card {i + 1} of {lines.Length}...";
                    await ProcessSingleReportExecutionAsync(studentNameClean, studentNotesClean, _appState.CurrentSettings.AiProvider, report => GeneratedReportOutput = report);

                    if (i < lines.Length - 1 && !token.IsCancellationRequested) await Task.Delay(1500, token);
                }
                StatusText = "Batch update completed.";
            }
            catch (TaskCanceledException) { StatusText = "Batch Stopped."; }
            finally { IsBatchModeActive = false; _batchCancellationTokenSource?.Dispose(); _batchCancellationTokenSource = null; }
        }

        private void CancelBatchGeneration()
        {
            if (_batchCancellationTokenSource != null && !_batchCancellationTokenSource.IsCancellationRequested)
            {
                _batchCancellationTokenSource.Cancel();
                StatusText = "Stopping active runs...";
            }
        }

        private async Task RunSideBySideComparisonAsync()
        {
            if (string.IsNullOrWhiteSpace(CompareStudentName) || string.IsNullOrWhiteSpace(CompareNotes)) return;
            IsCompareRightVisible = true;
            GeneratedReportOutput = $"Querying {CompareProvider1}...\n";
            CompareOutputRight = $"Querying {CompareProvider2}...\n";
            StatusText = "Running simultaneous analysis...";

            var task1 = ProcessSingleReportExecutionAsync(CompareStudentName, CompareNotes, CompareProvider1, out1 => GeneratedReportOutput = out1);
            var task2 = ProcessSingleReportExecutionAsync(CompareStudentName, CompareNotes, CompareProvider2, out2 => CompareOutputRight = out2);

            await Task.WhenAll(task1, task2);
            StatusText = "Ready.";
        }

        private async Task EmailReportAsync()
        {
            string plainSmtpPassword = CryptoService.DecryptSecret(_appState.CurrentSettings.SmtpPassword);
            if (string.IsNullOrWhiteSpace(_appState.CurrentSettings.SmtpEmail) || string.IsNullOrWhiteSpace(plainSmtpPassword))
            {
                MessageBox.Show("Please configure your SMTP credentials in Profile Settings.", "Missing Credentials", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try { _ = new System.Net.Mail.MailAddress(ParentEmail); }
            catch { MessageBox.Show("Invalid parent email address.", "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }

            string studentName = SanitizeControlOutput(SelectedStudentName);
            string subjectLine = string.IsNullOrWhiteSpace(studentName) ? "Student Update Report" : $"Performance Update — {studentName}";

            StatusText = "Sending email...";
            try
            {
                var securePwd = new SecureString();
                foreach (char c in plainSmtpPassword) securePwd.AppendChar(c);
                securePwd.MakeReadOnly();

                await EmailService.SendEmailAsync(ParentEmail, subjectLine, GeneratedReportOutput, _appState.CurrentSettings.SmtpServer, _appState.CurrentSettings.SmtpPort, _appState.CurrentSettings.SmtpEmail, securePwd);
                StatusText = "Email sent successfully.";
            }
            catch (Exception ex) { StatusText = $"Email failed: {ex.Message}"; }
        }

        private void ImportBatchCsv()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "CSV Files (*.csv)|*.csv" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var lines = File.ReadAllLines(dialog.FileName);
                    var sb = new StringBuilder();
                    var parser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var parts = parser.Split(line).Select(s => s.Trim('"', ' ')).ToArray();
                        if (parts.Length >= 2)
                        {
                            if (parts[0].ToLower().Contains("name") && parts[1].ToLower().Contains("note")) continue;
                            sb.AppendLine($"{parts[0]} | {parts[1]}");
                        }
                    }
                    BatchDataInput = sb.ToString();
                }
                catch (Exception ex) { MessageBox.Show($"File error: {ex.Message}"); }
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
                    var parser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var parts = parser.Split(line).Select(s => s.Trim('"', ' ')).ToArray();
                        if (parts.Length >= 1)
                        {
                            string sName = parts[0];
                            if (sName.ToLower().Contains("name")) continue;
                            if (!_studentDatabase.Any(s => s.FullName.Equals(sName, StringComparison.OrdinalIgnoreCase)))
                                _studentDatabase.Add(new StudentProfile { FullName = sName, ClassName = parts.Length >= 2 ? parts[1] : string.Empty });
                        }
                    }
                    StudentDatabaseService.SaveStudents(_studentDatabase);
                    RefreshCollections();
                }
                catch (Exception ex) { MessageBox.Show($"Roster error: {ex.Message}"); }
            }
        }

        private void SaveStudent() { string clean = SanitizeControlOutput(SelectedStudentName); if (string.IsNullOrEmpty(clean)) return; var match = _studentDatabase.FirstOrDefault(x => x.FullName.Equals(clean, StringComparison.OrdinalIgnoreCase)); if (match == null) { _studentDatabase.Add(new StudentProfile { FullName = clean, ClassName = StudentClass, ParentEmail = ParentEmail, TargetGrade = TargetGrade, SupportNeeds = SupportNeeds }); } else { match.ClassName = StudentClass; match.ParentEmail = ParentEmail; match.TargetGrade = TargetGrade; match.SupportNeeds = SupportNeeds; } StudentDatabaseService.SaveStudents(_studentDatabase); RefreshCollections(); StatusText = "Profile saved."; }
        private void DeleteStudent() { string clean = SanitizeControlOutput(SelectedStudentName); var match = _studentDatabase.FirstOrDefault(x => x.FullName.Equals(clean, StringComparison.OrdinalIgnoreCase)); if (match != null) { _studentDatabase.Remove(match); StudentDatabaseService.SaveStudents(_studentDatabase); StudentClass = ParentEmail = TargetGrade = SupportNeeds = string.Empty; SelectedStudentName = string.Empty; RefreshCollections(); StatusText = "Record dropped."; } }
        private void EnterApplication() { IsWelcomeOverlayVisible = false; }
        private void EditWelcomeProfile() { IsWelcomeBackVisible = false; IsProfileSetupVisible = true; SelectedNavigationIndex = 3; }
        private void CopyReportToClipboard() { if (!string.IsNullOrEmpty(GeneratedReportOutput)) { try { Clipboard.SetText(GeneratedReportOutput); StatusText = "Copied."; } catch { StatusText = "Clipboard blocked."; } } }
        private void SaveAsWord() { var d = new Microsoft.Win32.SaveFileDialog { Filter = "Word (*.docx)|*.docx" }; if (d.ShowDialog() == true) WordExportService.ExportSingle(d.FileName, SanitizeControlOutput(SelectedStudentName), GeneratedReportOutput); }
        private void SaveAsPdf() { var d = new Microsoft.Win32.SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf" }; if (d.ShowDialog() == true) PdfExportService.ExportSingle(d.FileName, SanitizeControlOutput(SelectedStudentName), GeneratedReportOutput); }
        private void ExportBatchWord() { if (SessionHistory.Count > 0) { var d = new Microsoft.Win32.SaveFileDialog { Filter = "Word (*.docx)|*.docx" }; if (d.ShowDialog() == true) WordExportService.ExportBatch(d.FileName, SessionHistory.ToList()); } }
        private void FilterHistoryByStudent() { string clean = SanitizeControlOutput(SelectedStudentName); if (string.IsNullOrEmpty(clean)) return; SessionHistory = new ObservableCollection<SessionRecord>(HistoryDatabaseService.LoadHistory().Where(x => x.StudentName.Contains(clean, StringComparison.OrdinalIgnoreCase)).ToList()); }
        private void ClearHistoryFilter() { SessionHistory = HistoryDatabaseService.LoadHistory() ?? new ObservableCollection<SessionRecord>(); }
        private void SaveCurriculumTopic() { string clean = SanitizeControlOutput(SelectedCurriculumTopic); if (!string.IsNullOrEmpty(clean) && !_appState.CurrentSettings.CurriculumTopics.Contains(clean)) { _appState.CurrentSettings.CurriculumTopics.Add(clean); _appState.SaveSettings(); OnPropertyChanged(nameof(CurriculumTopics)); } }
        private void DeleteCurriculumTopic() { string clean = SanitizeControlOutput(SelectedCurriculumTopic); if (!string.IsNullOrEmpty(clean) && _appState.CurrentSettings.CurriculumTopics.Contains(clean)) { _appState.CurrentSettings.CurriculumTopics.Remove(clean); _appState.SaveSettings(); OnPropertyChanged(nameof(CurriculumTopics)); } }
        private void CopyHistoryPreviewToCompareBox() { IsCompareRightVisible = true; if (SelectedHistoryItem != null) CompareOutputRight = SelectedHistoryItem.GeneratedReport; }

        public ObservableCollection<SessionRecord> SessionHistory { get => _sessionHistory; set => SetProperty(ref _sessionHistory, value); }
        public ObservableCollection<string> StudentNames { get => _studentNames; set => SetProperty(ref _studentNames, value); }
        public List<ReportFramework> CustomFrameworks => _appState.CurrentSettings.CustomFrameworks;
        public List<string> CurriculumTopics => _appState.CurrentSettings.CurriculumTopics;

        public SessionRecord? SelectedHistoryItem { get => _selectedHistoryItem; set => SetProperty(ref _selectedHistoryItem, value); }
        public int SelectedNavigationIndex { get => _selectedNavigationIndex; set { if (SetProperty(ref _selectedNavigationIndex, value)) { if (_selectedNavigationIndex < 3 && !string.IsNullOrEmpty(_appState.CurrentSettings.MasterPassword)) { SettingsVM.IsSettingsUnlocked = false; } } } }
        public string SelectedStudentName { get => _selectedStudentName; set { string clean = SanitizeControlOutput(value); if (SetProperty(ref _selectedStudentName, clean)) { var m = _studentDatabase.FirstOrDefault(x => x.FullName == clean); if (m != null) { StudentClass = m.ClassName; ParentEmail = m.ParentEmail; TargetGrade = m.TargetGrade; SupportNeeds = m.SupportNeeds; } } } }
        public string StudentClass { get => _studentClass; set => SetProperty(ref _studentClass, value); }
        public string TargetGrade { get => _targetGrade; set => SetProperty(ref _targetGrade, value); }
        public string SupportNeeds { get => _supportNeeds; set => SetProperty(ref _supportNeeds, value); }
        public string ParentEmail { get => _parentEmail; set => SetProperty(ref _parentEmail, value); }
        public int TargetWordCount { get => _targetWordCount; set => SetProperty(ref _targetWordCount, value); }
        public ReportFramework? SelectedFramework { get => _selectedFramework; set => SetProperty(ref _selectedFramework, value); }
        public string SelectedCurriculumTopic { get => _selectedCurriculumTopic; set => SetProperty(ref _selectedCurriculumTopic, value); }
        public string CustomNotes { get => _customNotes; set => SetProperty(ref _customNotes, value); }
        public string GeneratedReportOutput { get => _generatedReportOutput; set { if (SetProperty(ref _generatedReportOutput, value)) { (EmailReportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } } }
        public bool IsTimekeepingPerfect { get => _isTimekeepingPerfect; set => SetProperty(ref _isTimekeepingPerfect, value); }
        public bool IsTimekeepingGood { get => _isTimekeepingGood; set => SetProperty(ref _isTimekeepingGood, value); }
        public bool IsTimekeepingPoor { get => _isTimekeepingPoor; set => SetProperty(ref _isTimekeepingPoor, value); }
        public bool IsContributionEnthusiastic { get => _isContributionEnthusiastic; set => SetProperty(ref _isContributionEnthusiastic, value); }
        public bool IsContributionOccasional { get => _isContributionOccasional; set => SetProperty(ref _isContributionOccasional, value); }
        public bool IsContributionRare { get => _isContributionRare; set => SetProperty(ref _isContributionRare, value); }
        public bool IsContributionNever { get => _isContributionNever; set => SetProperty(ref _isContributionNever, value); }
        public string BatchDataInput { get => _batchDataInput; set => SetProperty(ref _batchDataInput, value); }
        public bool IsBatchModeActive { get => _isBatchModeActive; set { if (SetProperty(ref _isBatchModeActive, value)) { (GenerateBatchCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); } } }
        public string CompareStudentName { get => _compareStudentName; set => SetProperty(ref _compareStudentName, value); }
        public string CompareNotes { get => _compareNotes; set => SetProperty(ref _compareNotes, value); }
        public string CompareProvider1 { get => _compareProvider1; set => SetProperty(ref _compareProvider1, value); }
        public string CompareProvider2 { get => _compareProvider2; set => SetProperty(ref _compareProvider2, value); }
        public string CompareOutputRight { get => _compareOutputRight; set => SetProperty(ref _compareOutputRight, value); }
        public bool IsCompareRightVisible { get => _isCompareRightVisible; set => SetProperty(ref _isCompareRightVisible, value); }
        public bool IsWelcomeOverlayVisible { get => _isWelcomeOverlayVisible; set => SetProperty(ref _isWelcomeOverlayVisible, value); }
        public bool IsProfileSetupVisible { get => _isProfileSetupVisible; set => SetProperty(ref _isProfileSetupVisible, value); }
        public bool IsWelcomeBackVisible { get => _isWelcomeBackVisible; set => SetProperty(ref _isWelcomeBackVisible, value); }
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
        public bool IsGenerating { get => _isGenerating; set { if (SetProperty(ref _isGenerating, value)) { (GenerateSingleCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); (EmailReportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); (RunComparisonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); (PreviewToneCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); } } }
        public string HoursSavedDisplay { get => _hoursSavedDisplay; set => SetProperty(ref _hoursSavedDisplay, value); }
        public string TokensUsedDisplay { get => _tokensUsedDisplay; set => SetProperty(ref _tokensUsedDisplay, value); }
        public string NvidiaCountDisplay { get => _nvidiaCountDisplay; set => SetProperty(ref _nvidiaCountDisplay, value); }
        public string GeminiCountDisplay { get => _geminiCountDisplay; set => SetProperty(ref _geminiCountDisplay, value); }
        public string OpenaiCountDisplay { get => _openaiCountDisplay; set => SetProperty(ref _openaiCountDisplay, value); }
        public string ClaudeCountDisplay { get => _claudeCountDisplay; set => SetProperty(ref _claudeCountDisplay, value); }
    }
}