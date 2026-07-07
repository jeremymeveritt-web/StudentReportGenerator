using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace StudentReportGenerator.Services
{
    /// <summary>
    /// Shared base for every ViewModel in the app (<see cref="MainViewModel"/>,
    /// <see cref="SettingsViewModel"/>), providing the standard MVVM boilerplate: property-changed
    /// notification and a couple of small helpers used across both view models.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises <see cref="PropertyChanged"/> for the given property. The compiler supplies
        /// <paramref name="propertyName"/> automatically via <see cref="CallerMemberNameAttribute"/>
        /// when called from within a property setter, so callers rarely need to pass it explicitly.
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>Trims free-text input from XAML bindings and normalises null/whitespace-only
        /// values to <see cref="string.Empty"/>, so downstream code never has to null-check user input.</summary>
        protected string SanitizeControlOutput(string source)
        {
            return string.IsNullOrWhiteSpace(source) ? string.Empty : source.Trim();
        }

        /// <summary>
        /// Standard "set backing field, skip if unchanged, notify if changed" helper used by every
        /// bindable property in the app. Returns <c>true</c> only when the value actually changed,
        /// so callers can chain follow-up side effects (e.g. re-evaluating a dependent computed
        /// property) without doing so on every no-op set.
        /// </summary>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}