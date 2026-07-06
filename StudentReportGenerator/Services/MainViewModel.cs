using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
        private readonly ReportOrchestratorService _orchestrator;
        private readonly SchoolDataOrchestratorService _schoolData;
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
        private Guid _lastGeneratedRecordId;

        // Form Fields (Single Report)
        private string _selectedStudentName = string.Empty;
        private string _studentClass = string.Empty;
        private string _targetGrade = string.Empty;
        private string _supportNeeds = string.Empty;
        private string _parentEmail = string.Empty;
        private int _targetWordCount = 300;
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
        private string _selectedPronouns = "They/Them";
        public string SelectedPronouns { get => _selectedPronouns; set => SetProperty(ref _selectedPronouns, value); }

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
        public ICommand UpdateHistoryEditCommand { get; }
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
        public ICommand ClearBatchCommand { get; }
        public ICommand CopyBatchCommand { get; }
        public ICommand DictateCommand { get; }
        public ICommand InsertPhraseCommand { get; }
        public ICommand AddPhraseCommand { get; }
        public ICommand DeletePhraseCommand { get; }
        public ICommand RestoreAiDraftCommand { get; }
        public ICommand SimplifyForParentsCommand { get; }
        public ICommand TranslateReportCommand { get; }
        public ICommand ReadAloudCommand { get; }
        public ICommand StopReadingCommand { get; }
        public ICommand AuditToneCommand { get; }
        public ICommand RapidNextCommand { get; }
        public ICommand RapidSkipCommand { get; }
        public ICommand RetryQueuedCommand { get; }

        public MainViewModel(AppStateService appState, SettingsViewModel settingsVM, ReportOrchestratorService orchestrator, SchoolDataOrchestratorService schoolData)
        {
            _appState = appState;
            SettingsVM = settingsVM;
            _orchestrator = orchestrator;
            _schoolData = schoolData;

            UpdateHistoryEditCommand = new AsyncRelayCommand(_ => UpdateHistoryEditAsync(), _ => !string.IsNullOrWhiteSpace(GeneratedReportOutput));
            ClearBatchCommand = new RelayCommand(_ => ClearBatchInput());
            CopyBatchCommand = new RelayCommand(_ => { if (!string.IsNullOrWhiteSpace(BatchDataInput)) { System.Windows.Clipboard.SetText(BatchDataInput); StatusText = "✅ Entire batch copied!"; } }); 
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
            RunComparisonCommand = new AsyncRelayCommand(_ => RunSideBySideComparisonAsync(), _ => !IsGenerating && !IsComparing);
            ImportRosterCommand = new RelayCommand(_ => ImportRosterCsv());
            ViewStudentHistoryCommand = new RelayCommand(_ => FilterHistoryByStudent());
            ShowAllHistoryCommand = new RelayCommand(_ => ClearHistoryFilter());
            SaveCurriculumCommand = new RelayCommand(_ => SaveCurriculumTopic());
            DeleteCurriculumCommand = new RelayCommand(_ => DeleteCurriculumTopic());
            PreviewToneCommand = new AsyncRelayCommand(_ => PreviewToneAsync(), _ => !IsGenerating);
            CompareHistoryCommand = new RelayCommand(_ => CopyHistoryPreviewToCompareBox());
            DeleteHistoryCommand = new RelayCommand(DeleteHistoryRecord);
            DictateCommand = new RelayCommand(_ => ToggleDictation());
            InsertPhraseCommand = new RelayCommand(p => InsertPhrase(p as string));
            AddPhraseCommand = new RelayCommand(_ => AddPhraseToBank(), _ => !string.IsNullOrWhiteSpace(NewPhraseText));
            DeletePhraseCommand = new RelayCommand(p => DeletePhraseFromBank(p as string));
            RestoreAiDraftCommand = new RelayCommand(_ => RestoreAiDraft(), _ => !string.IsNullOrWhiteSpace(_lastAiDraft) && GeneratedReportOutput != _lastAiDraft);
            SimplifyForParentsCommand = new AsyncRelayCommand(_ => RunUtilityAsync(
                "Rewrite the following school report in plain, simple English (around a reading age of 11). Keep every fact, name and grade unchanged, and keep the same warm, professional tone. Output only the rewritten report.",
                GeneratedReportOutput, "Simplifying for parents...", "Simplified version (plain English):"), _ => !IsBusy && !string.IsNullOrWhiteSpace(GeneratedReportOutput));
            TranslateReportCommand = new AsyncRelayCommand(_ => RunUtilityAsync(
                $"Translate the following school report into {SelectedTranslationLanguage}. Keep student and teacher names unchanged. Output only the translation.",
                GeneratedReportOutput, $"Translating to {SelectedTranslationLanguage}...", "MACHINE TRANSLATED — please have it checked before sending:"), _ => !IsBusy && !string.IsNullOrWhiteSpace(GeneratedReportOutput));
            AuditToneCommand = new AsyncRelayCommand(_ => RunUtilityAsync(
                "You are auditing a batch of AI-drafted school reports for fairness. Compare tone, warmth, length and word choice across the reports below and flag any systematic differences between students (for example by gender implied through pronouns). Be concise, specific, and quote short examples. If the reports are balanced, say so plainly.",
                GeneratedReportOutput, "Auditing tone balance...", "Tone balance audit (optional check):"), _ => !IsBusy && !string.IsNullOrWhiteSpace(GeneratedReportOutput));
            ReadAloudCommand = new RelayCommand(_ => _speech.ReadAloud(GeneratedReportOutput), _ => !string.IsNullOrWhiteSpace(GeneratedReportOutput));
            StopReadingCommand = new RelayCommand(_ => _speech.StopReading());
            RapidNextCommand = new RelayCommand(_ => RapidCommitAndAdvance(), _ => !string.IsNullOrWhiteSpace(RapidCurrentName));
            RapidSkipCommand = new RelayCommand(_ => RapidAdvance(), _ => !string.IsNullOrWhiteSpace(RapidCurrentName));
            RetryQueuedCommand = new AsyncRelayCommand(_ => RetryQueuedReportsAsync(), _ => _offlineQueue.Count > 0 && !IsBusy);

            _commentBankPhrases = CommentBankService.LoadPhrases();
            RefreshCommentBank();
            LoadOfflineQueue();
            _speech.TextRecognized += OnDictationText;
            System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;

            _sessionHistory = HistoryDatabaseService.LoadHistory() ?? new ObservableCollection<SessionRecord>();
            _studentDatabase = StudentDatabaseService.LoadStudents() ?? new List<StudentProfile>();
            AttachHistorySearchFilter();

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
            RapidReset();
        }

        private void UpdateDashboardMetricsDisplay()
        {
            double totalMinutesSaved = _appState.CurrentSettings.TotalReportsGenerated * 10.0;
            double hoursSaved = totalMinutesSaved / 60.0;
            HoursSavedDisplay = Math.Round(totalMinutesSaved / 60.0, 1).ToString();
            TokensUsedDisplay = _appState.CurrentSettings.TotalTokensEstimated.ToString("N0");
            NvidiaCountDisplay = _appState.CurrentSettings.NvidiaReportsCount.ToString("N0");
            GeminiCountDisplay = _appState.CurrentSettings.GeminiReportsCount.ToString("N0");
            OpenaiCountDisplay = _appState.CurrentSettings.OpenAiReportsCount.ToString("N0");
            ClaudeCountDisplay = _appState.CurrentSettings.ClaudeReportsCount.ToString("N0");
            OnPropertyChanged(nameof(EstimatedCostDisplay));
            OnPropertyChanged(nameof(AggregateInsightsDisplay));
        }

        // Cost awareness: token counts translated into an approximate spend figure for
        // whoever pays the AI bill (see CostEstimatorService for the assumptions)
        public string EstimatedCostDisplay => CostEstimatorService.EstimateCostSummary(
            _appState.CurrentSettings.TotalTokensEstimated,
            _appState.CurrentSettings.NvidiaReportsCount,
            _appState.CurrentSettings.GeminiReportsCount,
            _appState.CurrentSettings.OpenAiReportsCount,
            _appState.CurrentSettings.ClaudeReportsCount);

        // Anonymised, aggregate-only figures — no individual teacher or student breakdown
        public string AggregateInsightsDisplay
        {
            get
            {
                var history = SessionHistory;
                if (history == null || history.Count == 0) return "No reports in the history log yet.";
                double avgWords = history.Average(r => r.GeneratedReport.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
                int thisMonth = history.Count(r => r.Timestamp.Year == DateTime.Now.Year && r.Timestamp.Month == DateTime.Now.Month);
                return $"Average report length: {Math.Round(avgWords)} words • Reports this month: {thisMonth} • Total on record: {history.Count}";
            }
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

            RunSafeguardingScan(CustomNotes);

            IsCompareRightVisible = false;
            StatusText = "Generating report...";
            await ProcessSingleReportExecutionAsync(cleanStudentName, compiledNotes, _appState.CurrentSettings.AiProvider, report => GeneratedReportOutput = report);
        }

        private async Task<bool> ProcessSingleReportExecutionAsync(string name, string notes, string provider, Action<string> onCompleteOutput, bool managesBusyFlag = true)
        {
            if (managesBusyFlag) IsGenerating = true;

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
                Pronouns = SelectedPronouns,
                Subject = SanitizeControlOutput(SelectedCurriculumTopic),
                WordCount = TargetWordCount,
                RawNotes = notes,
                SelectedFramework = SelectedFramework?.Instruction ?? string.Empty,
                SchoolName = _appState.CurrentSettings.SchoolName,
                TeacherSignoff = _appState.CurrentSettings.TeacherSignoff,
                TargetGrade = dbTargetGrade,
                SupportNeeds = dbSupportNeeds

            };

            // Ground the prompt in SIS facts when a connection exists and the school has
            // opted the category in; unmatched students silently keep the manual-entry values
            if (match != null && _schoolData.IsConnectionConfigured)
            {
                var stats = await _schoolData.GetStatsForStudentAsync(match);
                if (stats != null)
                {
                    var settings = _appState.CurrentSettings;
                    if (settings.IncludeAttendanceInPrompts) request.AttendancePercent = stats.AttendancePercent;
                    if (settings.IncludeBehaviourInPrompts) request.BehaviourPoints = stats.BehaviourPoints;
                    if (settings.IncludeGradesInPrompts && stats.RecentGrades.Count > 0)
                        request.RecentGradesSummary = string.Join("; ", stats.RecentGrades.Select(g => $"{g.Key}: {g.Value}"));
                    if (settings.IncludeSupportPlanInPrompts && !string.IsNullOrWhiteSpace(stats.SupportPlanSummary))
                        request.SupportNeeds = stats.SupportPlanSummary;
                    if (!string.IsNullOrWhiteSpace(stats.TargetGrade)) request.TargetGrade = stats.TargetGrade;
                }
            }

            try
            {
                var response = await _orchestrator.GenerateAsync(request, provider);
                if (managesBusyFlag) IsGenerating = false;

                if (response.IsSuccess)
                {
                    onCompleteOutput(response.GeneratedReport);

                    if (provider == _appState.CurrentSettings.AiProvider)
                    {
                        _lastAiDraft = response.GeneratedReport;
                        var newRecord = new SessionRecord
                        {
                            StudentName = request.StudentName,
                            GeneratedReport = response.GeneratedReport,
                            OriginalDraft = response.GeneratedReport,
                            Timestamp = DateTime.Now
                        };
                        _lastGeneratedRecordId = newRecord.Id;
                        SessionHistory.Insert(0, newRecord);
                        HistoryDatabaseService.SaveHistory(SessionHistory);
                    }

                    UpdateDashboardMetricsDisplay();
                    StatusText = "Ready.";
                    return true;
                }

                onCompleteOutput("We ran into a slight issue connecting to the AI provider. This is usually temporary. Please wait a moment and try again.");
                StatusText = "Service unavailable.";
                return false;
            }
            catch (TaskCanceledException)
            {
                if (managesBusyFlag) IsGenerating = false;
                if (name != "Student Preview")
                {
                    // Offline drafting: keep the request and retry automatically when connectivity returns
                    EnqueueForRetry(name, notes);
                    onCompleteOutput("The connection timed out. This report has been queued and will be retried automatically when your connection returns.");
                    StatusText = "Offline — report queued for retry.";
                }
                else
                {
                    onCompleteOutput("The connection timed out. Please check your school's internet connection or firewall and try again.");
                    StatusText = "Connection timed out.";
                }
                return false;
            }
            catch (Exception)
            {
                if (managesBusyFlag) IsGenerating = false;
                onCompleteOutput("An unexpected software error occurred while generating this report. If this persists, please restart or contact IT support.");
                StatusText = "Generation error.";
                return false;
            }
        }

        private async Task PreviewToneAsync()
        {
            if (SelectedFramework == null)
            {
                MessageBox.Show("Please select a Tone Template first.", "Template Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string notes = $"Generate a 2-sentence preview showing this tone. Subject: {SanitizeControlOutput(SelectedCurriculumTopic)}";

            StatusText = "Previewing Tone...";
            await ProcessSingleReportExecutionAsync("Student Preview", notes, _appState.CurrentSettings.AiProvider, report =>
            {
                MessageBox.Show(report, $"Tone Preview: {SelectedFramework.Name}", MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private async Task UpdateHistoryEditAsync()
        {
            var record = SessionHistory.FirstOrDefault(x => x.Id == _lastGeneratedRecordId);
            if (record != null)
            {
                record.GeneratedReport = GeneratedReportOutput;
                HistoryDatabaseService.SaveHistory(SessionHistory);

                StatusText = "✅ Edits saved to history log!";
                await Task.Delay(2000);
                StatusText = "Ready.";
            }
        }

        private void DeleteHistoryRecord(object? parameter)
        {
            if (SelectedHistoryItem == null) return;

            if (System.Windows.MessageBox.Show(
                $"Are you sure you want to delete this report for {SelectedHistoryItem.StudentName}?",
                "Confirm Deletion",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes)
            {
                SessionHistory.Remove(SelectedHistoryItem);
                HistoryDatabaseService.SaveHistory(SessionHistory);
                SelectedHistoryItem = null;
                StatusText = "History record deleted.";
            }
        }

        private async Task GenerateBatchAsync()
        {
            if (string.IsNullOrWhiteSpace(BatchDataInput)) return;
            RunSafeguardingScan(BatchDataInput);
            IsBatchModeActive = true;
            IsCompareRightVisible = false;
            GeneratedReportOutput = "🚀 Initializing Multi-Threaded Batch Processor...\n";

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

                    
                    await ProcessSingleReportExecutionAsync(studentNameClean, studentNotesClean, _appState.CurrentSettings.AiProvider, report =>
                    {
                        GeneratedReportOutput += $"\n\n=========================================\n📝 STUDENT: {studentNameClean.ToUpper()}\n=========================================\n{report}\n";
                    });


                    if (i < lines.Length - 1 && !token.IsCancellationRequested)
                    {
                        await Task.Delay(1500, token);
                    }
                }

                GeneratedReportOutput += "\n\n✅ BATCH PROCESSING COMPLETE.";
                StatusText = "Batch update completed.";
            }
            catch (TaskCanceledException)
            {
                GeneratedReportOutput += "\n\n🛑 BATCH ABORTED BY USER.";
                StatusText = "Batch Stopped.";
            }
            finally
            {
                IsBatchModeActive = false;
                _batchCancellationTokenSource?.Dispose();
                _batchCancellationTokenSource = null;
            }
        }

        private void ClearBatchInput()
        {
            if (string.IsNullOrWhiteSpace(BatchDataInput)) return;

            if (System.Windows.MessageBox.Show(
                "Are you sure you want to clear the entire batch input? All typed notes will be lost.",
                "Confirm Clear Batch",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes)
            {
                BatchDataInput = string.Empty;
                StatusText = "Batch input cleared.";
            }
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

            IsComparing = true;
            try
            {
                var task1 = ProcessSingleReportExecutionAsync(CompareStudentName, CompareNotes, CompareProvider1, out1 => GeneratedReportOutput = out1, managesBusyFlag: false);
                var task2 = ProcessSingleReportExecutionAsync(CompareStudentName, CompareNotes, CompareProvider2, out2 => CompareOutputRight = out2, managesBusyFlag: false);

                await Task.WhenAll(task1, task2);
            }
            finally
            {
                IsComparing = false;
            }
            StatusText = "Ready.";
        }

        private async Task EmailReportAsync()
        {
            if (!ConfirmUneditedDraft()) return;
            string plainSmtpPassword = CryptoService.DecryptSecret(_appState.CurrentSettings.SmtpPassword);
            if (string.IsNullOrWhiteSpace(_appState.CurrentSettings.SmtpEmail) || string.IsNullOrWhiteSpace(plainSmtpPassword))
            {
                // UX POLISH: Better instructions
                MessageBox.Show("Please configure your school email address and app password in the Profile Settings tab before sending emails.", "Email Setup Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try { _ = new System.Net.Mail.MailAddress(ParentEmail); }
            catch
            {
                MessageBox.Show("The parent email address provided is not formatted correctly. Please double-check it.", "Check Email Address", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string studentName = SanitizeControlOutput(SelectedStudentName);
            string subjectLine = string.IsNullOrWhiteSpace(studentName) ? "Student Update Report" : $"Performance Update — {studentName}";

            StatusText = "Sending email securely...";
            try
            {
                var securePwd = new SecureString();
                foreach (char c in plainSmtpPassword) securePwd.AppendChar(c);
                securePwd.MakeReadOnly();

                await EmailService.SendEmailAsync(ParentEmail, subjectLine, WithDisclosure(GeneratedReportOutput), _appState.CurrentSettings.SmtpServer, _appState.CurrentSettings.SmtpPort, _appState.CurrentSettings.SmtpEmail, securePwd);
                StatusText = "Email sent successfully.";
            }
            catch
            {
                // UX POLISH: Hide the SMTP socket exception trace
                StatusText = "Email failed to send. Check firewall/password.";
                MessageBox.Show("We couldn't connect to your email server. Please verify that your IT department hasn't blocked SMTP traffic, and ensure your App Password is correct.", "Delivery Failed", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    var sb = new StringBuilder();
                    var parser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");
                    int skippedHeader = 0;

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var parts = parser.Split(line).Select(s => s.Trim('"', ' ')).ToArray();
                        if (parts.Length >= 1)
                        {
                            string studentName = parts[0].Trim();
                            if (studentName.ToLower().Contains("name"))
                            {
                                skippedHeader++;
                                continue;
                            }
                            string studentNotes = parts.Length >= 2 ? parts[1].Trim() : string.Empty;

                            sb.AppendLine($"{studentName} | {studentNotes}");
                        }
                    }
                    BatchDataInput = sb.ToString();
                    StatusText = $"Imported CSV (Skipped {skippedHeader} header rows).";
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
        private void DeleteStudent()
        {
            if (string.IsNullOrWhiteSpace(SelectedStudentName)) return;

            if (System.Windows.MessageBox.Show($"Are you sure you want to permanently delete {SelectedStudentName}'s profile?", "Confirm Deletion", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes)
            {
                var match = _studentDatabase.FirstOrDefault(x => x.FullName == SelectedStudentName);
                if (match != null)
                {
                    _studentDatabase.Remove(match);
                    StudentDatabaseService.SaveStudents(_studentDatabase); // FIXED: Called SaveStudents!
                    StatusText = "Student profile deleted.";
                    SelectedStudentName = string.Empty;
                    RefreshCollections();
                }
            }
        }
        private void EnterApplication() { IsWelcomeOverlayVisible = false; }
        private void EditWelcomeProfile() { IsWelcomeBackVisible = false; IsProfileSetupVisible = true; SelectedNavigationIndex = 3; }
        private async void CopyReportToClipboard()
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(GeneratedReportOutput))
                {
                    System.Windows.Clipboard.SetText(GeneratedReportOutput);
                    StatusText = "✅ Copied to clipboard!";
                    await Task.Delay(2000);
                    StatusText = "Ready.";
                }
            }
            catch
            {
                StatusText = "Could not access clipboard.";
            }
        }
        private SchoolBranding CurrentBranding() => new SchoolBranding
        {
            SchoolName = _appState.CurrentSettings.SchoolName,
            LogoPath = _appState.CurrentSettings.SchoolLogoPath,
            AccentColorHex = _appState.CurrentSettings.ThemeColorHex
        };

        private void SaveAsWord() { if (!ConfirmUneditedDraft()) return; var d = new Microsoft.Win32.SaveFileDialog { Filter = "Word (*.docx)|*.docx" }; if (d.ShowDialog() == true) WordExportService.ExportSingle(d.FileName, SanitizeControlOutput(SelectedStudentName), WithDisclosure(GeneratedReportOutput), CurrentBranding()); }
        private void SaveAsPdf() { if (!ConfirmUneditedDraft()) return; var d = new Microsoft.Win32.SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf" }; if (d.ShowDialog() == true) PdfExportService.ExportSingle(d.FileName, SanitizeControlOutput(SelectedStudentName), WithDisclosure(GeneratedReportOutput), CurrentBranding()); }
        private void ExportBatchWord()
        {
            if (SessionHistory.Count == 0) return;
            var d = new Microsoft.Win32.SaveFileDialog { Filter = "Word (*.docx)|*.docx" };
            if (d.ShowDialog() != true) return;
            var records = SessionHistory
                .Select(r => new SessionRecord { StudentName = r.StudentName, GeneratedReport = WithDisclosure(r.GeneratedReport), Timestamp = r.Timestamp })
                .ToList();
            WordExportService.ExportBatch(d.FileName, records, CurrentBranding());
        }
        private void FilterHistoryByStudent() { string clean = SanitizeControlOutput(SelectedStudentName); if (string.IsNullOrEmpty(clean)) return; SessionHistory = new ObservableCollection<SessionRecord>(HistoryDatabaseService.LoadHistory().Where(x => x.StudentName.Contains(clean, StringComparison.OrdinalIgnoreCase)).ToList()); }
        private void ClearHistoryFilter() { SessionHistory = HistoryDatabaseService.LoadHistory() ?? new ObservableCollection<SessionRecord>(); }
        private void SaveCurriculumTopic() { string clean = SanitizeControlOutput(SelectedCurriculumTopic); if (!string.IsNullOrEmpty(clean) && !_appState.CurrentSettings.CurriculumTopics.Contains(clean)) { _appState.CurrentSettings.CurriculumTopics.Add(clean); _appState.SaveSettings(); OnPropertyChanged(nameof(CurriculumTopics)); } }
        private void DeleteCurriculumTopic() { string clean = SanitizeControlOutput(SelectedCurriculumTopic); if (!string.IsNullOrEmpty(clean) && _appState.CurrentSettings.CurriculumTopics.Contains(clean)) { _appState.CurrentSettings.CurriculumTopics.Remove(clean); _appState.SaveSettings(); OnPropertyChanged(nameof(CurriculumTopics)); } }
        private void CopyHistoryPreviewToCompareBox() { IsCompareRightVisible = true; if (SelectedHistoryItem != null) CompareOutputRight = SelectedHistoryItem.GeneratedReport; }

        public ObservableCollection<SessionRecord> SessionHistory { get => _sessionHistory; set { if (SetProperty(ref _sessionHistory, value)) AttachHistorySearchFilter(); } }
        public ObservableCollection<string> StudentNames { get => _studentNames; set => SetProperty(ref _studentNames, value); }
        public List<ReportFramework> CustomFrameworks => _appState.CurrentSettings.CustomFrameworks;
        public List<string> CurriculumTopics => _appState.CurrentSettings.CurriculumTopics;

        public SessionRecord? SelectedHistoryItem { get => _selectedHistoryItem; set => SetProperty(ref _selectedHistoryItem, value); }
        public int SelectedNavigationIndex
        {
            get => _selectedNavigationIndex;
            set
            {
      
                if (_selectedNavigationIndex == 0 && value != 0 && !string.IsNullOrWhiteSpace(CustomNotes))
                {
                    var result = System.Windows.MessageBox.Show(
                        "You have unsaved Teacher Observations. Are you sure you want to leave this tab? Your notes will be lost.",
                        "Unsaved Changes",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);

                    if (result == System.Windows.MessageBoxResult.No)
                    {
                        return; // Cancel the navigation!
                    }
                }

                if (SetProperty(ref _selectedNavigationIndex, value))
                {
                    // Clean up the notes if they safely navigated away
                    if (_selectedNavigationIndex != 0)
                    {
                        CustomNotes = string.Empty;
                    }
                }
            }
        }
        public string SelectedStudentName
        {
            get => _selectedStudentName;
            set
            {
                string clean = SanitizeControlOutput(value);
                if (SetProperty(ref _selectedStudentName, clean))
                {
                    var m = _studentDatabase.FirstOrDefault(x => x.FullName == clean);
                    if (m != null)
                    {
                        StudentClass = m.ClassName;
                        ParentEmail = m.ParentEmail;
                        TargetGrade = m.TargetGrade;
                        SupportNeeds = m.SupportNeeds;
                    }
                    UpdateLastTermPreview(clean);
                    CustomNotes = string.Empty;
                    IsTimekeepingPerfect = true;
                    IsTimekeepingGood = false;
                    IsTimekeepingPoor = false;
                    IsContributionEnthusiastic = true;
                    IsContributionOccasional = false;
                    IsContributionRare = false;
                    IsContributionNever = false;
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
        public string GeneratedReportOutput { get => _generatedReportOutput; set { if (SetProperty(ref _generatedReportOutput, value)) { OnPropertyChanged(nameof(ReadingLevelDisplay)); (EmailReportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); System.Windows.Input.CommandManager.InvalidateRequerySuggested(); } } }
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
        public bool IsGenerating { get => _isGenerating; set { if (SetProperty(ref _isGenerating, value)) { OnPropertyChanged(nameof(IsBusy)); (GenerateSingleCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); (EmailReportCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); (RunComparisonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); (PreviewToneCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); } } }
        private bool _isComparing = false;
        public bool IsComparing { get => _isComparing; set { if (SetProperty(ref _isComparing, value)) { OnPropertyChanged(nameof(IsBusy)); (RunComparisonCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged(); } } }
        public bool IsBusy => IsGenerating || IsComparing;
        public string HoursSavedDisplay { get => _hoursSavedDisplay; set => SetProperty(ref _hoursSavedDisplay, value); }
        public string TokensUsedDisplay { get => _tokensUsedDisplay; set => SetProperty(ref _tokensUsedDisplay, value); }
        public string NvidiaCountDisplay { get => _nvidiaCountDisplay; set => SetProperty(ref _nvidiaCountDisplay, value); }
        public string GeminiCountDisplay { get => _geminiCountDisplay; set => SetProperty(ref _geminiCountDisplay, value); }
        public string OpenaiCountDisplay { get => _openaiCountDisplay; set => SetProperty(ref _openaiCountDisplay, value); }
        public string ClaudeCountDisplay { get => _claudeCountDisplay; set => SetProperty(ref _claudeCountDisplay, value); }
        public string AppVersionDisplay => $"Version {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown"}";
        public string AppBuildDateDisplay
        {
            get
            {
                try
                {
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
                    return $"Built {File.GetLastWriteTime(exePath):d MMMM yyyy}";
                }
                catch { return string.Empty; }
            }
        }
        public string AboutInfo => $"FacultyFlow AI • {AppVersionDisplay}";

        private string _historySearchText = string.Empty;
        public string HistorySearchText
        {
            get => _historySearchText;
            set
            {
                if (SetProperty(ref _historySearchText, value))
                {
                    System.Windows.Data.CollectionViewSource.GetDefaultView(SessionHistory).Refresh();
                }
            }
        }

        // ===================== NEW FEATURE STATE =====================
        private readonly SpeechService _speech = new SpeechService();
        private List<string> _commentBankPhrases = new List<string>();
        private ObservableCollection<string> _visibleCommentBank = new ObservableCollection<string>();
        private string _commentFilterText = string.Empty;
        private string _newPhraseText = string.Empty;
        private string _lastAiDraft = string.Empty;
        private string _lastTermReportPreview = string.Empty;
        private bool _isDictating;
        private int _rapidIndex = 0;
        private string _rapidNotes = string.Empty;
        private string _selectedTranslationLanguage = "Polish";
        private readonly List<QueuedReport> _offlineQueue = new List<QueuedReport>();
        private static readonly string QueueFilePath = FileSandboxService.GetSafeFilePath("queued_reports.json");

        public class QueuedReport
        {
            public string StudentName { get; set; } = string.Empty;
            public string Notes { get; set; } = string.Empty;
        }

        public ObservableCollection<string> VisibleCommentBank { get => _visibleCommentBank; set => SetProperty(ref _visibleCommentBank, value); }
        public string CommentFilterText { get => _commentFilterText; set { if (SetProperty(ref _commentFilterText, value)) RefreshCommentBank(); } }
        public string NewPhraseText { get => _newPhraseText; set => SetProperty(ref _newPhraseText, value); }
        public bool IsDictating { get => _isDictating; set => SetProperty(ref _isDictating, value); }
        public string LastTermReportPreview { get => _lastTermReportPreview; set { if (SetProperty(ref _lastTermReportPreview, value)) OnPropertyChanged(nameof(HasLastTermReport)); } }
        public bool HasLastTermReport => !string.IsNullOrWhiteSpace(LastTermReportPreview);
        public string ReadingLevelDisplay => ReadabilityService.DescribeReadingLevel(GeneratedReportOutput);
        public List<string> TranslationLanguages { get; } = new List<string> { "Polish", "Romanian", "Urdu", "Arabic", "Portuguese", "Spanish", "French", "Somali", "Bengali", "Ukrainian", "Turkish", "Mandarin Chinese" };
        public string SelectedTranslationLanguage { get => _selectedTranslationLanguage; set => SetProperty(ref _selectedTranslationLanguage, SanitizeControlOutput(value)); }
        public string RapidCurrentName => _rapidIndex >= 0 && _rapidIndex < StudentNames.Count ? StudentNames[_rapidIndex] : string.Empty;
        public string RapidNotes { get => _rapidNotes; set => SetProperty(ref _rapidNotes, value); }
        public string RapidProgressDisplay => StudentNames.Count == 0
            ? "No students on the roster yet — import or save students first."
            : (_rapidIndex < StudentNames.Count ? $"Student {_rapidIndex + 1} of {StudentNames.Count}" : "✅ End of roster reached.");
        public string QueuedCountDisplay => _offlineQueue.Count == 0 ? string.Empty : $"{_offlineQueue.Count} report(s) queued for when connection returns";
        public bool HasQueuedReports => _offlineQueue.Count > 0;

        // --- Comment bank ---
        private void RefreshCommentBank()
        {
            VisibleCommentBank = new ObservableCollection<string>(CommentBankService.Suggest(_commentBankPhrases, CommentFilterText));
        }

        private void InsertPhrase(string? phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase)) return;
            string current = CustomNotes.TrimEnd();
            CustomNotes = string.IsNullOrEmpty(current) ? phrase : $"{current} {phrase}";
        }

        private void AddPhraseToBank()
        {
            _commentBankPhrases.Add(NewPhraseText.Trim());
            CommentBankService.SavePhrases(_commentBankPhrases);
            _commentBankPhrases = CommentBankService.LoadPhrases();
            NewPhraseText = string.Empty;
            RefreshCommentBank();
        }

        private void DeletePhraseFromBank(string? phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase)) return;
            _commentBankPhrases.RemoveAll(p => string.Equals(p, phrase, StringComparison.OrdinalIgnoreCase));
            CommentBankService.SavePhrases(_commentBankPhrases);
            RefreshCommentBank();
        }

        // --- Voice notes (on-device dictation) ---
        private void ToggleDictation()
        {
            if (_speech.IsDictating)
            {
                _speech.StopDictation();
                IsDictating = false;
                StatusText = "Dictation stopped.";
            }
            else if (_speech.StartDictation())
            {
                IsDictating = true;
                StatusText = "🎙️ Listening — speak your observations...";
            }
            else
            {
                StatusText = "Dictation unavailable (no microphone or speech engine found).";
            }
        }

        private void OnDictationText(string text)
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                string current = CustomNotes.TrimEnd();
                CustomNotes = string.IsNullOrEmpty(current) ? text : $"{current} {text}";
            });
        }

        // --- Version history / restore ---
        private void RestoreAiDraft()
        {
            if (!string.IsNullOrWhiteSpace(_lastAiDraft)) GeneratedReportOutput = _lastAiDraft;
        }

        private bool ConfirmUneditedDraft()
        {
            if (!string.IsNullOrWhiteSpace(_lastAiDraft) && GeneratedReportOutput == _lastAiDraft)
            {
                return System.Windows.MessageBox.Show(
                    "You haven't personalised this AI draft yet. Parents value a teacher's own voice — send/export it exactly as generated?",
                    "Add a Personal Touch?",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;
            }
            return true;
        }

        // --- AI disclosure ---
        private string WithDisclosure(string report)
        {
            if (!_appState.CurrentSettings.AppendAiDisclosure || string.IsNullOrWhiteSpace(report)) return report;
            string teacher = _appState.CurrentSettings.TeacherSignoff;
            string reviewer = string.IsNullOrWhiteSpace(teacher) || teacher == "Mr. / Ms. Teacher" ? "the class teacher" : teacher;
            return report + $"\n\nThis report was drafted with AI assistance and reviewed by {reviewer}.";
        }

        // --- Safeguarding nudge ---
        private void RunSafeguardingScan(string notes)
        {
            if (!_appState.CurrentSettings.EnableSafeguardingPrompt) return;
            var hits = SafeguardingScanService.Scan(notes);
            if (hits.Count > 0)
            {
                System.Windows.MessageBox.Show(
                    $"Your notes mention: {string.Join(", ", hits)}.\n\n" +
                    "If this relates to a child's safety or welfare, please also raise it through your school's usual safeguarding process (e.g. CPOMS or your DSL) — a report-writing tool is not the right channel for a disclosure.\n\n" +
                    "Report generation will continue as normal.",
                    "Possible Safeguarding Note",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }

        // --- Utility AI calls (simplify / translate / tone audit) ---
        private async Task RunUtilityAsync(string instruction, string content, string statusLabel, string resultHeader)
        {
            if (string.IsNullOrWhiteSpace(content)) return;
            IsComparing = true;
            StatusText = statusLabel;
            try
            {
                var request = new ReportRequest { UtilityInstruction = instruction, RawNotes = content, StudentName = "Utility", WordCount = 800 };
                var response = await _orchestrator.GenerateAsync(request);
                IsCompareRightVisible = true;
                CompareOutputRight = response.IsSuccess
                    ? $"{resultHeader}\n\n{response.GeneratedReport}"
                    : "The AI provider could not complete this request. Please try again in a moment.";
                StatusText = response.IsSuccess ? "Ready." : "Service unavailable.";
            }
            catch
            {
                StatusText = "Request failed — check your connection.";
            }
            finally
            {
                IsComparing = false;
            }
        }

        // --- Rapid-fire whole-class entry ---
        private void RapidCommitAndAdvance()
        {
            if (!string.IsNullOrWhiteSpace(RapidNotes))
            {
                string line = $"{RapidCurrentName} | {RapidNotes.Trim().Replace("\r", " ").Replace("\n", " ")}";
                BatchDataInput = string.IsNullOrWhiteSpace(BatchDataInput) ? line : $"{BatchDataInput.TrimEnd()}\r\n{line}";
            }
            RapidAdvance();
        }

        private void RapidAdvance()
        {
            RapidNotes = string.Empty;
            if (_rapidIndex < StudentNames.Count) _rapidIndex++;
            OnPropertyChanged(nameof(RapidCurrentName));
            OnPropertyChanged(nameof(RapidProgressDisplay));
        }

        private void RapidReset()
        {
            _rapidIndex = 0;
            RapidNotes = string.Empty;
            OnPropertyChanged(nameof(RapidCurrentName));
            OnPropertyChanged(nameof(RapidProgressDisplay));
        }

        // --- Offline queue ---
        private void LoadOfflineQueue()
        {
            try
            {
                if (File.Exists(QueueFilePath))
                {
                    var queued = System.Text.Json.JsonSerializer.Deserialize<List<QueuedReport>>(File.ReadAllText(QueueFilePath));
                    if (queued != null) _offlineQueue.AddRange(queued);
                }
            }
            catch { }
            OnPropertyChanged(nameof(QueuedCountDisplay));
            OnPropertyChanged(nameof(HasQueuedReports));
        }

        private void SaveOfflineQueue()
        {
            try { File.WriteAllText(QueueFilePath, System.Text.Json.JsonSerializer.Serialize(_offlineQueue)); } catch { }
            OnPropertyChanged(nameof(QueuedCountDisplay));
            OnPropertyChanged(nameof(HasQueuedReports));
        }

        private void EnqueueForRetry(string studentName, string notes)
        {
            if (_offlineQueue.Any(q => q.StudentName == studentName && q.Notes == notes)) return;
            _offlineQueue.Add(new QueuedReport { StudentName = studentName, Notes = notes });
            SaveOfflineQueue();
        }

        private async Task RetryQueuedReportsAsync()
        {
            var pending = _offlineQueue.ToList();
            foreach (var item in pending)
            {
                StatusText = $"Retrying queued report for {item.StudentName}...";
                bool ok = await ProcessSingleReportExecutionAsync(item.StudentName, item.Notes, _appState.CurrentSettings.AiProvider,
                    report => GeneratedReportOutput = report);
                if (!ok) break; // still offline — stop and keep the rest queued
                _offlineQueue.Remove(item);
                SaveOfflineQueue();
            }
            StatusText = _offlineQueue.Count == 0 ? "Queued reports completed." : "Some reports are still queued.";
        }

        private void OnNetworkAvailabilityChanged(object? sender, System.Net.NetworkInformation.NetworkAvailabilityEventArgs e)
        {
            if (e.IsAvailable && _offlineQueue.Count > 0)
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
                {
                    if (!IsBusy) await RetryQueuedReportsAsync();
                });
            }
        }

        // --- Term-over-term continuity ---
        private void UpdateLastTermPreview(string studentName)
        {
            var previous = (HistoryDatabaseService.LoadHistory() ?? new ObservableCollection<SessionRecord>())
                .Where(r => r.StudentName.Equals(studentName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.Timestamp)
                .FirstOrDefault();
            LastTermReportPreview = previous == null
                ? string.Empty
                : $"Previous report ({previous.Timestamp:d MMM yyyy}):\n\n{previous.GeneratedReport}";
        }

        private void AttachHistorySearchFilter()
        {
            System.Windows.Data.CollectionViewSource.GetDefaultView(SessionHistory).Filter = MatchesHistorySearch;
        }

        private bool MatchesHistorySearch(object item)
        {
            if (string.IsNullOrWhiteSpace(HistorySearchText)) return true;
            return item is SessionRecord record &&
                   ((record.StudentName?.Contains(HistorySearchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (record.GeneratedReport?.Contains(HistorySearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        }
    }
}