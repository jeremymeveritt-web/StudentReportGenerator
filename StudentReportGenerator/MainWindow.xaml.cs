using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using StudentReportGenerator.Models;
using StudentReportGenerator.Services;

namespace StudentReportGenerator
{
    public partial class MainWindow : Window
    {
        // Commercial Optimization: Reuse a single static HttpClient socket across all services to prevent connection exhaustion
        private static readonly HttpClient _sharedHttpClient = new HttpClient();

        private ObservableCollection<SessionRecord> _sessionHistory = new ObservableCollection<SessionRecord>();
        private AppSettings _currentSettings = new AppSettings();
        private bool _isSettingsUnlocked = false;
        private List<StudentProfile> _studentDatabase = new List<StudentProfile>();
        private System.Threading.CancellationTokenSource? _batchCancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            _sessionHistory = HistoryDatabaseService.LoadHistory();
            lstHistory.ItemsSource = _sessionHistory;
            LoadSettings();
        }

        private void LoadSettings()
        {
            _currentSettings = SecureSettingsService.LoadSettings() ?? new AppSettings();
            overlayWelcome.Visibility = Visibility.Visible;

            if (string.IsNullOrWhiteSpace(_currentSettings.TeacherSignoff) || _currentSettings.TeacherSignoff == "Mr. / Ms. Teacher")
            {
                pnlProfileSetup.Visibility = Visibility.Visible;
                pnlWelcomeBack.Visibility = Visibility.Collapsed;
            }
            else
            {
                pnlProfileSetup.Visibility = Visibility.Collapsed;
                pnlWelcomeBack.Visibility = Visibility.Visible;
                lblWelcomeBack.Text = $"Welcome back, {_currentSettings.TeacherSignoff}!";
            }

            txtSetSchoolName.Text = _currentSettings.SchoolName;
            txtSetTeacherName.Text = _currentSettings.TeacherSignoff;
            pwdMasterSet.Password = _currentSettings.MasterPassword;
            txtSmtpEmail.Text = _currentSettings.SmtpEmail;
            pwdSmtpPassword.Password = _currentSettings.SmtpPassword;
            chkDarkMode.IsChecked = _currentSettings.IsDarkMode;

            ApplyDarkMode(_currentSettings.IsDarkMode);
            ApplyBranding();

            foreach (ComboBoxItem item in cmbAiProvider.Items)
            {
                if (item.Content?.ToString() == _currentSettings.AiProvider)
                {
                    cmbAiProvider.SelectedItem = item;
                    break;
                }
            }

            RefreshFrameworkDropdown();
            UpdateDashboardUI();
            _studentDatabase = StudentDatabaseService.LoadStudents() ?? new List<StudentProfile>();
            RefreshStudentDropdown();
            RefreshCurriculumDropdown();
        }

        private void UpdateDashboardUI()
        {
            double totalMinutesSaved = _currentSettings.TotalReportsGenerated * 5.0;
            lblHoursSaved.Text = Math.Round(totalMinutesSaved / 60.0, 1).ToString();
            lblTokensUsed.Text = _currentSettings.TotalTokensEstimated.ToString("N0");
            lblGeminiCount.Text = _currentSettings.GeminiReportsCount.ToString("N0");
            lblOpenAiCount.Text = _currentSettings.OpenAiReportsCount.ToString("N0");
            lblClaudeCount.Text = _currentSettings.ClaudeReportsCount.ToString("N0");
            lblNvidiaCount.Text = _currentSettings.NvidiaReportsCount.ToString("N0");
        }

        private void ApplyBranding()
        {
            if (!string.IsNullOrWhiteSpace(_currentSettings.ThemeColorHex))
            {
                try
                {
                    TopNavBar.Background = (SolidColorBrush)new BrushConverter().ConvertFrom(_currentSettings.ThemeColorHex);
                    foreach (ComboBoxItem item in cmbThemeColor.Items)
                    {
                        if (item.Tag?.ToString() == _currentSettings.ThemeColorHex)
                        {
                            cmbThemeColor.SelectedItem = item;
                            break;
                        }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(_currentSettings.SchoolName) && _currentSettings.SchoolName != "Enter School Name")
            {
                lblMainTitle.Text = _currentSettings.SchoolName.ToUpper() + " REPORT GENERATOR";
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
                    imgSchoolLogo.Source = bitmap;
                    imgSchoolLogo.Visibility = Visibility.Visible;
                }
                catch { imgSchoolLogo.Visibility = Visibility.Collapsed; }
            }
            else
            {
                imgSchoolLogo.Visibility = Visibility.Collapsed;
            }
        }

        private void btnUploadLogo_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openDialog = new Microsoft.Win32.OpenFileDialog();
            openDialog.Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg";

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string ext = Path.GetExtension(openDialog.FileName);
                    string safeLogoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"school_logo{ext}");
                    File.Copy(openDialog.FileName, safeLogoPath, true);

                    _currentSettings.SchoolLogoPath = safeLogoPath;
                    SecureSettingsService.SaveSettings(_currentSettings);
                    ApplyBranding();
                    MessageBox.Show("Logo updated successfully!", "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading image: {ex.Message}", "Error");
                }
            }
        }

        private void cmbThemeColor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbThemeColor.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                _currentSettings.ThemeColorHex = selectedItem.Tag.ToString() ?? "";
                ApplyBranding();
            }
        }

        private void cmbAiProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentSettings == null || pwdDynamicApiKey == null || lblDynamicApiKey == null || cmbModelTier == null) return;
            string selectedProvider = (cmbAiProvider.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            pwdDynamicApiKey.PasswordChanged -= pwdDynamicApiKey_PasswordChanged;
            cmbModelTier.SelectionChanged -= cmbModelTier_SelectionChanged;
            cmbModelTier.Items.Clear();

            if (selectedProvider.Contains("NVIDIA"))
            {
                lblDynamicApiKey.Text = "NVIDIA Key:";
                pwdDynamicApiKey.Password = _currentSettings.NvidiaApiKey;
                cmbModelTier.Items.Add(new ComboBoxItem { Content = "Llama 3.1 405B (Smarter)", Tag = "meta/llama-3.1-405b-instruct" });
                cmbModelTier.Items.Add(new ComboBoxItem { Content = "Llama 3.1 70B (Balanced)", Tag = "meta/llama-3.1-70b-instruct" });
                cmbModelTier.Items.Add(new ComboBoxItem { Content = "Nemotron 70B (NVIDIA)", Tag = "nvidia/nemotron-4-340b-instruct" });
                cmbModelTier.Items.Add(new ComboBoxItem { Content = "Mistral Large (Fast)", Tag = "mistralai/mistral-large-2-instruct" });
                SetDropdownByTag(cmbModelTier, _currentSettings.NvidiaModelTier);
            }
            else if (selectedProvider.Contains("Gemini"))
            {
                lblDynamicApiKey.Text = "Gemini Key:";
                pwdDynamicApiKey.Password = _currentSettings.GeminiApiKey;
                cmbModelTier.Items.Add(new ComboBoxItem { Content = "Gemini 2.5 Flash", Tag = "gemini-2.5-flash" });
                cmbModelTier.Items.Add(new ComboBoxItem { Content = "Gemini 2.5 Pro", Tag = "gemini-2.5-pro" });
                SetDropdownByTag(cmbModelTier, _currentSettings.GeminiModelTier);
            }
            else if (selectedProvider.Contains("OpenAI"))
            {
                lblDynamicApiKey.Text = "OpenAI Key:";
                pwdDynamicApiKey.Password = _currentSettings.OpenAiApiKey;
                cmbModelTier.Items.Add(new ComboBoxItem { Content = "GPT-4o Mini", Tag = "gpt-4o-mini" });
                cmbModelTier.Items.Add(new ComboBoxItem { Content = "GPT-4o", Tag = "gpt-4o" });
                SetDropdownByTag(cmbModelTier, _currentSettings.OpenAiModelTier);
            }
            else if (selectedProvider.Contains("Claude"))
            {
                lblDynamicApiKey.Text = "Claude Key:";
                pwdDynamicApiKey.Password = _currentSettings.ClaudeApiKey;
                cmbModelTier.Items.Add(new ComboBoxItem { Content = "Claude 3 Haiku", Tag = "claude-3-haiku-20240307" });
                cmbModelTier.Items.Add(new ComboBoxItem { Content = "Claude 3.5 Sonnet", Tag = "claude-3-5-sonnet-20240620" });
                SetDropdownByTag(cmbModelTier, _currentSettings.ClaudeModelTier);
            }

            pwdDynamicApiKey.PasswordChanged += pwdDynamicApiKey_PasswordChanged;
            cmbModelTier.SelectionChanged += cmbModelTier_SelectionChanged;
        }

        private void pwdDynamicApiKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_currentSettings == null) return;
            string selectedProvider = (cmbAiProvider.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            if (selectedProvider.Contains("NVIDIA")) _currentSettings.NvidiaApiKey = pwdDynamicApiKey.Password;
            else if (selectedProvider.Contains("Gemini")) _currentSettings.GeminiApiKey = pwdDynamicApiKey.Password;
            else if (selectedProvider.Contains("OpenAI")) _currentSettings.OpenAiApiKey = pwdDynamicApiKey.Password;
            else if (selectedProvider.Contains("Claude")) _currentSettings.ClaudeApiKey = pwdDynamicApiKey.Password;
        }

        private void cmbModelTier_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentSettings == null) return;
            string selectedProvider = (cmbAiProvider.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            string selectedTier = (cmbModelTier.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

            if (selectedProvider.Contains("NVIDIA")) _currentSettings.NvidiaModelTier = selectedTier;
            else if (selectedProvider.Contains("Gemini")) _currentSettings.GeminiModelTier = selectedTier;
            else if (selectedProvider.Contains("OpenAI")) _currentSettings.OpenAiModelTier = selectedTier;
            else if (selectedProvider.Contains("Claude")) _currentSettings.ClaudeModelTier = selectedTier;
        }

        private async Task<bool> ProcessSingleReport(string studentName, string notes, string requestedProvider, TextBox targetBox)
        {
            if (btnGenerate != null) btnGenerate.IsEnabled = false;
            if (prgLoading != null) prgLoading.Visibility = Visibility.Visible;

            string activeKey = ""; string activeModel = ""; IAiService activeAiEngine;

            if (requestedProvider.Contains("NVIDIA")) { activeKey = _currentSettings.NvidiaApiKey; activeModel = _currentSettings.NvidiaModelTier; activeAiEngine = new NvidiaReportService(_sharedHttpClient, activeKey); }
            else if (requestedProvider.Contains("OpenAI")) { activeKey = _currentSettings.OpenAiApiKey; activeModel = _currentSettings.OpenAiModelTier; activeAiEngine = new OpenAiReportService(_sharedHttpClient, activeKey); }
            else if (requestedProvider.Contains("Claude")) { activeKey = _currentSettings.ClaudeApiKey; activeModel = _currentSettings.ClaudeModelTier; activeAiEngine = new ClaudeReportService(_sharedHttpClient, activeKey); }
            else { activeKey = _currentSettings.GeminiApiKey; activeModel = _currentSettings.GeminiModelTier; activeAiEngine = new GeminiReportService(_sharedHttpClient, activeKey); }

            if (string.IsNullOrWhiteSpace(activeKey)) { targetBox.Text = "ERROR: Missing API Key."; if (prgLoading != null) prgLoading.Visibility = Visibility.Collapsed; if (btnGenerate != null) btnGenerate.IsEnabled = true; return false; }

            int selectedWordCount = 150; if (cmbWordCount.SelectedItem is ComboBoxItem item) int.TryParse(item.Tag?.ToString(), out selectedWordCount);
            string selectedInstruction = (cmbFramework.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

            string dbTargetGrade = "";
            string dbSupportNeeds = "";
            var studentRecord = _studentDatabase.FirstOrDefault(s => s.FullName.Equals(studentName, StringComparison.OrdinalIgnoreCase));
            if (studentRecord != null)
            {
                dbTargetGrade = studentRecord.TargetGrade;
                dbSupportNeeds = studentRecord.SupportNeeds;
            }

            var request = new ReportRequest
            {
                StudentName = studentName,
                Subject = cmbCurriculum.Text,
                WordCount = selectedWordCount,
                RawNotes = notes,
                SelectedFramework = selectedInstruction,
                SchoolName = _currentSettings.SchoolName,
                TeacherSignoff = _currentSettings.TeacherSignoff,
                SelectedModel = activeModel,
                TargetGrade = dbTargetGrade,
                SupportNeeds = dbSupportNeeds
            };

            var response = await activeAiEngine.GenerateReportAsync(request);
            if (prgLoading != null) prgLoading.Visibility = Visibility.Collapsed;
            if (btnGenerate != null) btnGenerate.IsEnabled = true;

            if (response.IsSuccess)
            {
                targetBox.Text = $"[{requestedProvider}]\n\n{response.GeneratedReport}";
                _sessionHistory.Insert(0, new SessionRecord { StudentName = $"{request.StudentName} ({requestedProvider.Split(' ')[0]})", GeneratedReport = response.GeneratedReport, Timestamp = DateTime.Now });
                HistoryDatabaseService.SaveHistory(_sessionHistory);

                int words = response.GeneratedReport.Split(' ').Length; _currentSettings.TotalTokensEstimated += (long)(words * 1.3);
                _currentSettings.TotalReportsGenerated++;
                if (requestedProvider.Contains("NVIDIA")) _currentSettings.NvidiaReportsCount++;
                else if (requestedProvider.Contains("OpenAI")) _currentSettings.OpenAiReportsCount++;
                else if (requestedProvider.Contains("Claude")) _currentSettings.ClaudeReportsCount++;
                else _currentSettings.GeminiReportsCount++;

                SecureSettingsService.SaveSettings(_currentSettings); UpdateDashboardUI();
                return true;
            }
            else { targetBox.Text = response.ErrorMessage; return false; }
        }

        private void SetDropdownByTag(ComboBox cmb, string tagValue) { foreach (ComboBoxItem item in cmb.Items) { if (item.Tag?.ToString() == tagValue) { cmb.SelectedItem = item; return; } } }

        private void btnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            _currentSettings.SchoolName = txtSetSchoolName.Text;
            _currentSettings.TeacherSignoff = txtSetTeacherName.Text;
            _currentSettings.SmtpEmail = txtSmtpEmail.Text;
            _currentSettings.SmtpPassword = pwdSmtpPassword.Password;
            SecureSettingsService.SaveSettings(_currentSettings);
            MessageBox.Show("Saved!");
        }

        private void cmbNavigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gridSingleReport == null) return;
            gridSingleReport.Visibility = gridBatchMode.Visibility = gridCompare.Visibility = gridProfileSettings.Visibility = gridAiSettings.Visibility = gridDashboard.Visibility = Visibility.Collapsed;
            int i = cmbNavigation.SelectedIndex;
            if (i == 0) gridSingleReport.Visibility = Visibility.Visible;
            else if (i == 1) gridBatchMode.Visibility = Visibility.Visible;
            else if (i == 2) gridCompare.Visibility = Visibility.Visible;
            else if (i == 3) gridProfileSettings.Visibility = Visibility.Visible;
            else if (i == 4) gridAiSettings.Visibility = Visibility.Visible;
            else if (i == 5) gridDashboard.Visibility = Visibility.Visible;

            if (i == 3 || i == 4)
            {
                if (!string.IsNullOrEmpty(_currentSettings.MasterPassword) && !_isSettingsUnlocked)
                {
                    scrollProfileSettings.Visibility = Visibility.Collapsed;
                    brdProfileLock.Visibility = Visibility.Visible;
                    pwdUnlockProfile.Password = "";

                    scrollAiSettings.Visibility = Visibility.Collapsed;
                    brdAiLock.Visibility = Visibility.Visible;
                    pwdUnlockAi.Password = "";
                }
                else
                {
                    scrollProfileSettings.Visibility = Visibility.Visible;
                    brdProfileLock.Visibility = Visibility.Collapsed;
                    scrollAiSettings.Visibility = Visibility.Visible;
                    brdAiLock.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void btnEnterApp_Click(object sender, RoutedEventArgs e)
        {
            if (pnlProfileSetup.Visibility == Visibility.Visible)
            {
                _currentSettings.TeacherSignoff = txtWelcomeTeacherName.Text;
                _currentSettings.SchoolName = txtWelcomeSchoolName.Text;
                SecureSettingsService.SaveSettings(_currentSettings);
                ApplyBranding();
            }
            overlayWelcome.Visibility = Visibility.Collapsed;
        }

        private void btnEditProfile_Click(object sender, RoutedEventArgs e)
        {
            pnlWelcomeBack.Visibility = Visibility.Collapsed;
            pnlProfileSetup.Visibility = Visibility.Visible;
            txtWelcomeTeacherName.Text = _currentSettings.TeacherSignoff;
            txtWelcomeSchoolName.Text = _currentSettings.SchoolName;
            btnEnterApp.Content = "Update Profile & Enter Application";
        }

        private void btnUnlockSettings_Click(object sender, RoutedEventArgs e)
        {
            string attemptedPassword = gridProfileSettings.Visibility == Visibility.Visible ? pwdUnlockProfile.Password : pwdUnlockAi.Password;
            if (attemptedPassword == _currentSettings.MasterPassword)
            {
                _isSettingsUnlocked = true;
                gridProfileSettings.Visibility = gridAiSettings.Visibility = Visibility.Visible;
                brdProfileLock.Visibility = brdAiLock.Visibility = Visibility.Collapsed;
            }
            else
            {
                MessageBox.Show("Incorrect Password.", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                pwdUnlockProfile.Password = "";
                pwdUnlockAi.Password = "";
            }
        }

        private void ResetCompareModeUI()
        {
            colCompareRight.Width = new GridLength(0);
            txtCompareOutput2.Visibility = Visibility.Collapsed;
        }

        private async void btnGenerate_Click(object sender, RoutedEventArgs e)
        {
            string compiledNotes = $"Curriculum Topic Studied: {cmbCurriculum.Text}\n\n";
            compiledNotes += "Attendance & Timekeeping: ";
            if (rbTime1.IsChecked == true) compiledNotes += "100% attendance.\n";
            else if (rbTime2.IsChecked == true) compiledNotes += "Good overall, but has occasional absences.\n";
            else if (rbTime3.IsChecked == true) compiledNotes += "Struggles with attendance; several unauthorised absences.\n";
            compiledNotes += "Class Contributions: ";
            if (rbContribute1.IsChecked == true) compiledNotes += "Contributes enthusiastically in class.\n";
            else if (rbContribute2.IsChecked == true) compiledNotes += "Occasionally contributes.\n";
            else if (rbContribute3.IsChecked == true) compiledNotes += "Contributes rarely, but always with high quality responses.\n";
            else if (rbContribute4.IsChecked == true) compiledNotes += "Never contributes in class.\n";
            if (!string.IsNullOrWhiteSpace(txtNotes.Text)) compiledNotes += $"\nAdditional Teacher Notes: {txtNotes.Text}";

            ResetCompareModeUI();
            await ProcessSingleReport(cmbStudentDatabase.Text, compiledNotes, _currentSettings.AiProvider, txtOutput);
        }

        private void btnSaveStudent_Click(object sender, RoutedEventArgs e)
        {
            var existingStudent = _studentDatabase.FirstOrDefault(s => s.FullName.Equals(cmbStudentDatabase.Text, StringComparison.OrdinalIgnoreCase));
            if (existingStudent == null)
            {
                _studentDatabase.Add(new StudentProfile
                {
                    FullName = cmbStudentDatabase.Text,
                    ClassName = txtStudentClass.Text,
                    ParentEmail = txtParentEmail.Text,
                    TargetGrade = txtTargetGrade.Text,
                    SupportNeeds = txtSupportNeeds.Text
                });
            }
            else
            {
                existingStudent.ClassName = txtStudentClass.Text;
                existingStudent.ParentEmail = txtParentEmail.Text;
                existingStudent.TargetGrade = txtTargetGrade.Text;
                existingStudent.SupportNeeds = txtSupportNeeds.Text;
            }
            StudentDatabaseService.SaveStudents(_studentDatabase);
            RefreshStudentDropdown();
        }

        private void btnDeleteStudent_Click(object sender, RoutedEventArgs e)
        {
            var student = _studentDatabase.FirstOrDefault(s => s.FullName.Equals(cmbStudentDatabase.Text, StringComparison.OrdinalIgnoreCase));
            if (student != null)
            {
                _studentDatabase.Remove(student);
                StudentDatabaseService.SaveStudents(_studentDatabase);
                txtStudentClass.Text = "";
                txtParentEmail.Text = "";
                txtTargetGrade.Text = "";
                txtSupportNeeds.Text = "";
                RefreshStudentDropdown();
                cmbStudentDatabase.Text = "";
            }
        }

        private void cmbStudentDatabase_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var s = _studentDatabase.FirstOrDefault(x => x.FullName == cmbStudentDatabase.SelectedItem?.ToString());
            if (s != null)
            {
                txtStudentClass.Text = s.ClassName;
                txtParentEmail.Text = s.ParentEmail;
                txtTargetGrade.Text = s.TargetGrade;
                txtSupportNeeds.Text = s.SupportNeeds;
            }
        }

        private void RefreshStudentDropdown() { cmbStudentDatabase.ItemsSource = _studentDatabase.Select(x => x.FullName).ToList(); }
        private void RefreshCurriculumDropdown() { cmbCurriculum.ItemsSource = _currentSettings.CurriculumTopics; }
        private void RefreshFrameworkDropdown() { cmbFramework.Items.Clear(); foreach (var f in _currentSettings.CustomFrameworks) cmbFramework.Items.Add(new ComboBoxItem { Content = f.Name, Tag = f.Instruction }); }

        private void UpdateResource(string key, object value)
        {
            // Update Globally
            Application.Current.Resources[key] = value;
            // Update Locally on this Window context to force immediate UI recalculation overrides
            if (this.Resources.Contains(key)) this.Resources[key] = value;
            else this.Resources.Add(key, value);
        }

        private void ApplyDarkMode(bool isDark)
        {
            if (isDark)
            {
                UpdateResource("ThemeAppBg", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF1E1E1E")));
                UpdateResource("ThemeCardBg", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2D2D30")));
                UpdateResource("ThemeText", Brushes.White);
                UpdateResource("ThemeMutedText", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA")));
                UpdateResource("ThemeBorder", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF444444")));
                UpdateResource("ThemeInputBg", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF252526")));
                UpdateResource("ThemePreviewBg", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2D2D30")));
                if (lblActiveModule != null) lblActiveModule.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCE93D8"));
            }
            else
            {
                UpdateResource("ThemeAppBg", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFAFAFA")));
                UpdateResource("ThemeCardBg", Brushes.White);
                UpdateResource("ThemeText", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF333333")));
                UpdateResource("ThemeMutedText", Brushes.Gray);
                UpdateResource("ThemeBorder", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFDDDDDD")));
                UpdateResource("ThemeInputBg", Brushes.White);
                UpdateResource("ThemePreviewBg", new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF9F9F9")));
                if (lblActiveModule != null) lblActiveModule.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9C27B0"));
            }
        }

        private void chkDarkMode_Changed(object sender, RoutedEventArgs e)
        {
            bool isDark = chkDarkMode.IsChecked == true;
            ApplyDarkMode(isDark);
            if (_currentSettings != null)
            {
                _currentSettings.IsDarkMode = isDark;
                SecureSettingsService.SaveSettings(_currentSettings);
            }
        }

        private void MenuItem_EditHistory_Click(object sender, RoutedEventArgs e)
        {
            if (lstHistory.SelectedItem is SessionRecord selectedRecord)
            {
                txtOutput.Text = selectedRecord.GeneratedReport;
                cmbNavigation.SelectedIndex = 0;
                MessageBox.Show("Report loaded back into the active editor!", "Edit Mode");
            }
            else
            {
                MessageBox.Show("Please select a report first by left-clicking it, then right-click.", "Select Report");
            }
        }

        private void MenuItem_DeleteHistory_Click(object sender, RoutedEventArgs e)
        {
            if (lstHistory.SelectedItem is SessionRecord selectedRecord)
            {
                var result = MessageBox.Show("Are you sure you want to permanently delete this report from your history?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    _sessionHistory.Remove(selectedRecord);
                    HistoryDatabaseService.SaveHistory(_sessionHistory);
                    txtHistoryPreview.Text = "";
                }
            }
            else
            {
                MessageBox.Show("Please select a report first by left-clicking it, then right-click.", "Select Report");
            }
        }

        private void lstHistory_SelectionChanged(object sender, SelectionChangedEventArgs e) { txtHistoryPreview.Text = (lstHistory.SelectedItem as SessionRecord)?.GeneratedReport ?? ""; }
        private void btnCompareHistory_Click(object sender, RoutedEventArgs e) { colCompareRight.Width = new GridLength(1, GridUnitType.Star); txtCompareOutput2.Visibility = Visibility.Visible; txtCompareOutput2.Text = txtHistoryPreview.Text; }

        private string GetSmartFileName(string extension)
        {
            string sName = string.IsNullOrWhiteSpace(cmbStudentDatabase.Text) ? "Student" : cmbStudentDatabase.Text.Replace(" ", "");
            string topic = string.IsNullOrWhiteSpace(cmbCurriculum.Text) ? "Report" : cmbCurriculum.Text.Replace(" ", "");
            string date = DateTime.Now.ToString("yyyyMMdd");
            string combined = $"{sName}_{topic}_{date}{extension}";
            return string.Join("_", combined.Split(Path.GetInvalidFileNameChars()));
        }

        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtOutput.Text))
            {
                Clipboard.SetText(txtOutput.Text);
                MessageBox.Show("Report copied to clipboard!", "Copied");
            }
            else
            {
                MessageBox.Show("There is no report generated yet to copy.", "Empty");
            }
        }

        private void btnSaveWord_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtOutput.Text)) return;
            Microsoft.Win32.SaveFileDialog saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.Filter = "Word Document (*.docx)|*.docx";
            saveDialog.FileName = GetSmartFileName(".docx");
            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    WordExportService.ExportSingle(saveDialog.FileName, cmbStudentDatabase.Text, txtOutput.Text);
                    MessageBox.Show("Native Word Document saved successfully!", "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not save Word file. Make sure it isn't already open!\n\nError: {ex.Message}", "Error");
                }
            }
        }

        private void btnSavePdf_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtOutput.Text)) return;
            Microsoft.Win32.SaveFileDialog saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.Filter = "PDF Document (*.pdf)|*.pdf";
            saveDialog.FileName = GetSmartFileName(".pdf");
            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    PdfExportService.ExportSingle(saveDialog.FileName, cmbStudentDatabase.Text, txtOutput.Text);
                    MessageBox.Show("PDF Document saved successfully!", "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not generate PDF. Make sure the file isn't open!\n\nError: {ex.Message}", "Error");
                }
            }
        }

        private async void btnEmailReport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtOutput.Text)) return;
            if (string.IsNullOrWhiteSpace(txtParentEmail.Text))
            {
                MessageBox.Show("Please enter a Parent Email address for this student first.", "Missing Email");
                return;
            }
            if (string.IsNullOrWhiteSpace(_currentSettings.SmtpEmail) || string.IsNullOrWhiteSpace(_currentSettings.SmtpPassword))
            {
                MessageBox.Show("Please set up your SMTP Email credentials in the Profile Settings module.", "Setup Required");
                return;
            }

            var result = MessageBox.Show($"Send this report directly to {txtParentEmail.Text}?", "Confirm Send", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                btnEmailReport.IsEnabled = false;
                btnEmailReport.Content = "Sending...";
                try
                {
                    string subject = $"Official Report for {cmbStudentDatabase.Text}";
                    await EmailService.SendEmailAsync(txtParentEmail.Text, subject, txtOutput.Text,
                                                      _currentSettings.SmtpServer, _currentSettings.SmtpPort,
                                                      _currentSettings.SmtpEmail, _currentSettings.SmtpPassword);
                    MessageBox.Show("Email sent successfully!", "Success");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to send email. Ensure you are using an 'App Password' for Google/Microsoft and not your standard login.\n\nError: {ex.Message}", "Email Failed");
                }
                btnEmailReport.IsEnabled = true;
                btnEmailReport.Content = "✉️ Email to Parent";
            }
        }

        private void btnImportCSV_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openDialog = new Microsoft.Win32.OpenFileDialog();
            openDialog.Filter = "CSV Files (*.csv)|*.csv";
            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string[] lines = System.IO.File.ReadAllLines(openDialog.FileName);
                    txtBatchData.Clear();
                    var csvParser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(Y?![^\"]*\"))");
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        string[] parts = csvParser.Split(line).Select(s => s.Trim('"', ' ')).ToArray();
                        if (parts.Length >= 2)
                        {
                            if (parts[0].ToLower().Contains("name") && parts[1].ToLower().Contains("note")) continue;
                            txtBatchData.Text += $"{parts[0]} | {parts[1]}\n";
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading CSV file.\n\nError: {ex.Message}", "Import Failed");
                }
            }
        }

        private async void btnGenerateBatch_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtBatchData.Text)) return;

            btnGenerateBatch.Visibility = Visibility.Collapsed;
            btnCancelBatch.Visibility = Visibility.Visible;
            btnCancelBatch.IsEnabled = true;
            btnCancelBatch.Content = "🛑 Stop Generation";

            ResetCompareModeUI();
            if (prgLoading != null) prgLoading.Visibility = Visibility.Visible;
            txtOutput.Text = "Starting batch process...\n";

            var lines = txtBatchData.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int successCount = 0;

            _batchCancellationTokenSource = new System.Threading.CancellationTokenSource();
            var token = _batchCancellationTokenSource.Token;

            try
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        txtOutput.Text += "\n\n⚠️ BATCH CANCELLED BY USER.";
                        break;
                    }

                    var parts = lines[i].Split('|');
                    if (parts.Length < 2) continue;

                    if (lblStatus != null) lblStatus.Text = $"Generating report for {parts[0].Trim()} ({i + 1} of {lines.Length})...";

                    bool success = await ProcessSingleReport(parts[0].Trim(), parts[1].Trim(), _currentSettings.AiProvider, txtOutput);
                    if (success) successCount++;

                    if (i < lines.Length - 1 && !token.IsCancellationRequested)
                    {
                        await Task.Delay(1500, token);
                    }
                }

                if (lblStatus != null) lblStatus.Text = token.IsCancellationRequested ? $"Batch Stopped. {successCount} reports generated." : $"Batch Complete! {successCount} reports generated.";
            }
            catch (TaskCanceledException)
            {
                if (lblStatus != null) lblStatus.Text = $"Batch Stopped. {successCount} reports generated.";
            }
            finally
            {
                if (prgLoading != null) prgLoading.Visibility = Visibility.Collapsed;
                btnGenerateBatch.Visibility = Visibility.Visible;
                btnCancelBatch.Visibility = Visibility.Collapsed;

                _batchCancellationTokenSource?.Dispose();
                _batchCancellationTokenSource = null;
            }
        }

        private void btnCancelBatch_Click(object sender, RoutedEventArgs e)
        {
            if (_batchCancellationTokenSource != null && !_batchCancellationTokenSource.IsCancellationRequested)
            {
                _batchCancellationTokenSource.Cancel();
                btnCancelBatch.Content = "Stopping...";
                btnCancelBatch.IsEnabled = false;
            }
        }

        private void btnExportBatchWord_Click(object sender, RoutedEventArgs e)
        {
            if (_sessionHistory.Count == 0) return;
            Microsoft.Win32.SaveFileDialog saveDialog = new Microsoft.Win32.SaveFileDialog();
            saveDialog.Filter = "Word Document (*.docx)|*.docx";
            saveDialog.FileName = $"Batch_Class_Reports_{DateTime.Now:yyyyMMdd}.docx";
            if (saveDialog.ShowDialog() == true)
            {
                WordExportService.ExportBatch(saveDialog.FileName, _sessionHistory.ToList());
                MessageBox.Show("Batch Word Document saved successfully!", "Success");
            }
        }

        private async void btnCompare_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtCompareName.Text) || string.IsNullOrWhiteSpace(txtCompareNotes.Text)) return;
            string provider1 = cmbCompare1.Text;
            string provider2 = cmbCompare2.Text;
            colCompareRight.Width = new GridLength(1, GridUnitType.Star);
            txtCompareOutput2.Visibility = Visibility.Visible;
            txtOutput.Text = $"Waiting on {provider1}...\n";
            txtCompareOutput2.Text = $"Waiting on {provider2}...\n";
            btnCompare.IsEnabled = false;
            if (prgLoading != null) prgLoading.Visibility = Visibility.Visible;
            if (lblStatus != null) lblStatus.Text = "Running simultaneous generation...";
            var task1 = ProcessSingleReport(txtCompareName.Text, txtCompareNotes.Text, provider1, txtOutput);
            var task2 = ProcessSingleReport(txtCompareName.Text, txtCompareNotes.Text, provider2, txtCompareOutput2);
            await Task.WhenAll(task1, task2);
            if (prgLoading != null) prgLoading.Visibility = Visibility.Collapsed;
            if (lblStatus != null) lblStatus.Text = "Comparison complete!";
            btnCompare.IsEnabled = true;
        }

        private void btnImportRoster_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog openDialog = new Microsoft.Win32.OpenFileDialog();
            openDialog.Filter = "CSV Files (*.csv)|*.csv";
            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string[] lines = File.ReadAllLines(openDialog.FileName);
                    var csvParser = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(Y?![^\"]*\"))");
                    int addedCount = 0;
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        string[] parts = csvParser.Split(line).Select(s => s.Trim('"', ' ')).ToArray();
                        if (parts.Length >= 1)
                        {
                            string sName = parts[0];
                            if (sName.ToLower().Contains("name")) continue;
                            string sClass = parts.Length >= 2 ? parts[1] : "";
                            if (!_studentDatabase.Any(s => s.FullName.Equals(sName, StringComparison.OrdinalIgnoreCase)))
                            {
                                _studentDatabase.Add(new StudentProfile { FullName = sName, ClassName = sClass });
                                addedCount++;
                            }
                        }
                    }
                    StudentDatabaseService.SaveStudents(_studentDatabase);
                    RefreshStudentDropdown();
                    MessageBox.Show($"Successfully imported {addedCount} new students into the database!", "Import Complete");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error reading CSV roster file.\n\nError: {ex.Message}", "Import Failed");
                }
            }
        }

        private void btnViewStudentHistory_Click(object sender, RoutedEventArgs e)
        {
            string targetName = cmbStudentDatabase.Text.Trim();
            if (string.IsNullOrWhiteSpace(targetName))
            {
                MessageBox.Show("Please type or select a student name first.", "Missing Data");
                return;
            }
            var filteredHistory = _sessionHistory.Where(r => r.StudentName.Contains(targetName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (filteredHistory.Count == 0)
            {
                MessageBox.Show($"No past reports found for '{targetName}'.", "History Empty");
                return;
            }
            lstHistory.ItemsSource = filteredHistory;
            btnShowAllHistory.Visibility = Visibility.Visible;
            if (lblStatus != null) lblStatus.Text = $"Showing filtered history for: {targetName}";
        }

        private void btnShowAllHistory_Click(object sender, RoutedEventArgs e)
        {
            lstHistory.ItemsSource = _sessionHistory;
            btnShowAllHistory.Visibility = Visibility.Collapsed;
            if (lblStatus != null) lblStatus.Text = "Ready.";
        }

        private void btnSaveCurriculum_Click(object sender, RoutedEventArgs e)
        {
            string newTopic = cmbCurriculum.Text.Trim();
            if (string.IsNullOrWhiteSpace(newTopic)) return;
            if (!_currentSettings.CurriculumTopics.Any(t => t.Equals(newTopic, StringComparison.OrdinalIgnoreCase)))
            {
                _currentSettings.CurriculumTopics.Add(newTopic);
                _currentSettings.CurriculumTopics.Sort();
                SecureSettingsService.SaveSettings(_currentSettings);
                RefreshCurriculumDropdown();
                cmbCurriculum.Text = newTopic;
                MessageBox.Show($"'{newTopic}' added to the Curriculum Topics!", "Saved");
            }
            else MessageBox.Show("This topic is already in the list.", "Info");
        }

        private void btnDeleteCurriculum_Click(object sender, RoutedEventArgs e)
        {
            string targetTopic = cmbCurriculum.Text.Trim();
            if (string.IsNullOrWhiteSpace(targetTopic)) return;
            if (_currentSettings.CurriculumTopics.Contains(targetTopic, StringComparer.OrdinalIgnoreCase))
            {
                var result = MessageBox.Show($"Are you sure you want to completely delete '{targetTopic}' from the list?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    var exactMatch = _currentSettings.CurriculumTopics.First(t => t.Equals(targetTopic, StringComparison.OrdinalIgnoreCase));
                    _currentSettings.CurriculumTopics.Remove(exactMatch);
                    SecureSettingsService.SaveSettings(_currentSettings);
                    RefreshCurriculumDropdown();
                    cmbCurriculum.Text = "";
                }
            }
        }

        private void btnAddFramework_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtFrameworkName.Text) && !string.IsNullOrWhiteSpace(txtFrameworkInstruction.Text))
            {
                _currentSettings.CustomFrameworks.Add(new ReportFramework { Name = txtFrameworkName.Text, Instruction = txtFrameworkInstruction.Text });
                RefreshFrameworkDropdown();
                txtFrameworkName.Text = "";
                txtFrameworkInstruction.Text = "";
                SecureSettingsService.SaveSettings(_currentSettings);
            }
        }

        private async void btnPreviewTone_Click(object sender, RoutedEventArgs e)
        {
            if (cmbFramework.SelectedItem is ComboBoxItem selectedFrameworkItem && selectedFrameworkItem.Tag != null)
            {
                string frameworkInstruction = selectedFrameworkItem.Tag.ToString() ?? "";
                string provider = _currentSettings.AiProvider;
                btnPreviewTone.IsEnabled = false;
                btnPreviewTone.Content = "Loading...";
                string activeKey = string.Empty;
                IAiService activeAiEngine;

                if (provider.Contains("NVIDIA"))
                {
                    activeKey = _currentSettings.NvidiaApiKey;
                    activeAiEngine = new NvidiaReportService(_sharedHttpClient, activeKey);
                }
                else if (provider.Contains("OpenAI"))
                {
                    activeKey = _currentSettings.OpenAiApiKey;
                    activeAiEngine = new OpenAiReportService(_sharedHttpClient, activeKey);
                }
                else if (provider.Contains("Claude"))
                {
                    activeKey = _currentSettings.ClaudeApiKey;
                    activeAiEngine = new ClaudeReportService(_sharedHttpClient, activeKey);
                }
                else
                {
                    activeKey = _currentSettings.GeminiApiKey;
                    activeAiEngine = new GeminiReportService(_sharedHttpClient, activeKey);
                }

                if (string.IsNullOrWhiteSpace(activeKey))
                {
                    MessageBox.Show($"Please enter an API Key for {provider} in the Settings module.", "Missing Key");
                    btnPreviewTone.IsEnabled = true;
                    btnPreviewTone.Content = "Preview Tone";
                    return;
                }

                var request = new ReportRequest
                {
                    StudentName = "Student",
                    Subject = "General",
                    WordCount = 30,
                    RawNotes = "Student did a good job this term.",
                    SelectedFramework = frameworkInstruction + " IMPORTANT: Write exactly ONE sentence demonstrating this tone.",
                    SchoolName = _currentSettings.SchoolName,
                    TeacherSignoff = _currentSettings.TeacherSignoff,
                    SelectedModel = provider.Contains("NVIDIA") ? _currentSettings.NvidiaModelTier :
                                   (provider.Contains("OpenAI") ? _currentSettings.OpenAiModelTier :
                                   (provider.Contains("Claude") ? _currentSettings.ClaudeModelTier : _currentSettings.GeminiModelTier))
                };

                var response = await activeAiEngine.GenerateReportAsync(request);
                btnPreviewTone.IsEnabled = true;
                btnPreviewTone.Content = "Preview Tone";

                if (response.IsSuccess)
                {
                    MessageBox.Show($"Tone Preview ({selectedFrameworkItem.Content}):\n\n\"{response.GeneratedReport}\"", "Tone Preview", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Preview Failed: {response.ErrorMessage}", "Error");
                }
            }
        }
    }
}