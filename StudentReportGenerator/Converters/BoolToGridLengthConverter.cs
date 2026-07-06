using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StudentReportGenerator.Converters
{
    // Collapses a grid column when Simple Mode is on (true -> zero width, false -> the
    // star width given as the converter parameter, e.g. "1.0").
    public class BoolToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double star = 1.0;
            if (parameter is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                star = parsed;

            return value is true ? new GridLength(0) : new GridLength(star, GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
