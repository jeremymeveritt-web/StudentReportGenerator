using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StudentReportGenerator.Converters
{
    /// <summary>
    /// Converts bool -> Visibility.
    /// Pass ConverterParameter="Inverse" to flip the logic (true -> Collapsed, false -> Visible).
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool flag = value is bool b && b;

            bool invert = parameter is string s &&
                          s.Equals("Inverse", StringComparison.OrdinalIgnoreCase);

            if (invert) flag = !flag;

            return flag ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}