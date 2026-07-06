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

    }
}