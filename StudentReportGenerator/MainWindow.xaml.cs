using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using StudentReportGenerator.Services;

namespace StudentReportGenerator
{
    /// <summary>
    /// Code-behind for the main application window. Kept deliberately thin per MVVM convention —
    /// its only responsibility is bridging the handful of things WPF's data binding genuinely
    /// cannot do in pure XAML: <see cref="PasswordBox"/> controls don't support binding their
    /// <c>Password</c> property (a security feature of the control), so each one is wired to its
    /// ViewModel property manually via a <c>PasswordChanged</c> event handler below.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>Parameterless constructor required by the WPF designer/XAML tooling. Not used
        /// at runtime — the app always resolves <see cref="MainWindow"/> through the DI container,
        /// which calls the constructor below instead.</summary>
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

        // Each PasswordBox routes its sensitive value directly into the corresponding
        // SettingsViewModel property, since PasswordBox.Password cannot be data-bound in XAML.

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

        /// <summary>
        /// When <see cref="SettingsViewModel.SettingsUnlockPassword"/> is cleared programmatically
        /// (e.g. after a successful unlock, or a rejected attempt), clears the corresponding
        /// PasswordBox to match — because the binding only flows one way (control → ViewModel) for
        /// security reasons, the ViewModel can't push a value back into the control itself.
        /// </summary>
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