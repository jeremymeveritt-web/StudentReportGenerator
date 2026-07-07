using System;
using System.Windows.Input;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Standard synchronous ICommand implementation for MVVM bindings (the classic "RelayCommand"/
    /// "DelegateCommand" pattern). Wraps a plain <see cref="Action{T}"/> so view models don't need to
    /// hand-roll ICommand for every button/menu binding.
    /// </summary>
    /// <remarks>
    /// For commands whose execute delegate is <c>async</c>, use <see cref="AsyncRelayCommand"/> instead —
    /// awaiting inside a synchronous <see cref="Execute"/> would turn it into fire-and-forget and swallow
    /// exceptions.
    /// </remarks>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        /// <param name="execute">The action to run when the command is invoked.</param>
        /// <param name="canExecute">Optional guard; when omitted the command is always enabled.</param>
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);

        /// <summary>
        /// Hooks into WPF's <see cref="CommandManager.RequerySuggested"/> so bound controls
        /// automatically re-evaluate <see cref="CanExecute"/> after most UI input events,
        /// without each view model needing to manually raise this event.
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}