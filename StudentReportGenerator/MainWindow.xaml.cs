using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StudentReportGenerator.Services;

namespace StudentReportGenerator
{
    public partial class MainWindow : Window

    {
        public MainWindow()
        {
            InitializeComponent();
        }
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
            viewModel.SettingsVM.PropertyChanged += OnSettingsPropertyChanged;
        }

        // We now route the sensitive password box changes directly into the new SettingsVM
        private void PbSmtpPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.SettingsVM.SettingsSmtpPassword = ((PasswordBox)sender).Password;
        }

        private void PbMasterPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.SettingsVM.SettingsMasterPassword = ((PasswordBox)sender).Password;
        }

        private void PbUnlockPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.SettingsVM.SettingsUnlockPassword = ((PasswordBox)sender).Password;
        }

        private void PbApiKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.SettingsVM.DynamicApiKeyPassword = ((PasswordBox)sender).Password;
        }

        private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.SettingsUnlockPassword) &&
                DataContext is MainViewModel vm &&
                string.IsNullOrEmpty(vm.SettingsVM.SettingsUnlockPassword) &&
                pbUnlockPassword != null)
            {
                pbUnlockPassword.Clear();
            }
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

        private void UpdateResource(string key, object value)
        {
            Application.Current.Resources[key] = value;
            if (this.Resources.Contains(key)) this.Resources[key] = value;
            else this.Resources.Add(key, value);
        }
    }
}