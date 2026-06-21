using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
// Resolves Bug #7: Explicit namespace declaration prevents fragile transitive build configuration breaks
using System.Collections.Generic;

namespace StudentReportGenerator.Services
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected string SanitizeControlOutput(string source)
        {
            if (string.IsNullOrWhiteSpace(source)) return string.Empty;
            if (source.Contains("System.Windows.Controls.ComboBoxItem:"))
                return source.Replace("System.Windows.Controls.ComboBoxItem:", "").Trim();
            return source.Trim();
        }

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