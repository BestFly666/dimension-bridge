using System.Windows.Media;

namespace SimpleXmlEditor
{
    public partial class MainWindow
    {
        private void ApplyTheme()
        {
            if (_isDarkMode)
            {
                this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E2E"));

                EntriesGrid.AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27273A"));
                EntriesGrid.RowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E2E"));
                EntriesGrid.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CDD6F4"));

                FilterKeyBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313244"));
                FilterKeyBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CDD6F4"));
                FilterBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313244"));
                FilterBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CDD6F4"));
                FilterTranslationBox.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#313244"));
                FilterTranslationBox.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CDD6F4"));
            }
            else
            {
                this.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F2F5"));

                EntriesGrid.AlternatingRowBackground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFB"));
                EntriesGrid.RowBackground = new SolidColorBrush(Colors.White);
                EntriesGrid.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#37474F"));

                FilterKeyBox.Background = new SolidColorBrush(Colors.White);
                FilterKeyBox.Foreground = new SolidColorBrush(Colors.Black);
                FilterBox.Background = new SolidColorBrush(Colors.White);
                FilterBox.Foreground = new SolidColorBrush(Colors.Black);
                FilterTranslationBox.Background = new SolidColorBrush(Colors.White);
                FilterTranslationBox.Foreground = new SolidColorBrush(Colors.Black);
            }
        }
    }
}
