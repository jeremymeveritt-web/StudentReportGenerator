using System.Linq;
using System.Windows;
using System.Windows.Media;
using StudentReportGenerator.Services;

namespace StudentReportGenerator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml. Cleaned and decoupled for production-ready MVVM.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Wire up the decentralized MainViewModel as the primary DataContext pipeline
            this.DataContext = new MainViewModel();
        }

        /// <summary>
        /// Invoked dynamically by the MainViewModel when a user flips the theme preference toggle state.
        /// </summary>
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