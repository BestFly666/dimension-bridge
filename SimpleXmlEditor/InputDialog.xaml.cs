using System.Windows;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor
{
    public partial class InputDialog : Window
    {
        public string Value1 { get; private set; }
        public string Value2 { get; private set; }

        public InputDialog(string title, string label1, string label2)
        {
            InitializeComponent();
            LocalizationManager.LanguageChanged += ApplyLocalization;
            Closed += (_, _) => LocalizationManager.LanguageChanged -= ApplyLocalization;
            ApplyLocalization();
            Title = title;
            TitleTextBlock.Text = title;
            Label1Text.Text = label1;
            Label2Text.Text = label2;
        }

        private void ApplyLocalization()
        {
            CancelBtn.Content = LocalizationManager.GetString("Cancel");
            OKBtn.Content = LocalizationManager.GetString("OK");
        }

        private void OKBtn_Click(object sender, RoutedEventArgs e)
        {
            Value1 = Input1.Text;
            Value2 = Input2.Text;
            DialogResult = true;
            Close();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
