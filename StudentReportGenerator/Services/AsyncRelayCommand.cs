using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// An async-capable ICommand implementation that:
    ///   1. Runs the async payload on the calling (UI) thread via await, preserving SynchronizationContext.
    ///   2. Exposes a CanExecute predicate so buttons disable themselves during execution.
    ///   3. Catches unhandled exceptions and surfaces them as MessageBox alerts rather than
    ///      silently dying on a background ThreadPool thread.
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object?, Task> _execute;
        private readonly Predicate<object?>? _canExecute;
        private bool _isExecuting;

        public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute == null || _canExecute(parameter));
        }

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute(parameter);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{ex.Message}",
                    "Application Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged()
        {
            Application.Current?.Dispatcher?.Invoke(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty));
        }
    }
}