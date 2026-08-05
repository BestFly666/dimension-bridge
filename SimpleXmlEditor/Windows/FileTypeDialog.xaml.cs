using System;
using System.Windows;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor
{
    public enum FileTypeResult
    {
        Source,     // 原文
        Translation,// 译文
        Cancel
    }

    public partial class FileTypeDialog : Window
    {
        public FileTypeResult Result { get; private set; } = FileTypeResult.Cancel;

        public FileTypeDialog(Window owner)
        {
            InitializeComponent();
            Owner = owner;
            LocalizationManager.LanguageChanged += ApplyLocalization;
            Closed += (_, _) => LocalizationManager.LanguageChanged -= ApplyLocalization;
            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            Func<string, string> L = LocalizationManager.GetString;
            this.Title = L("FileTypeTitle");
            PromptText.Text = L("FileTypePrompt");
            SourceBtn.Content = L("SourceFile");
            TranslationBtn.Content = L("TranslationFile");
            CancelBtn.Content = L("Cancel");
        }

        private void SourceBtn_Click(object sender, RoutedEventArgs e)
        {
            Result = FileTypeResult.Source;
            DialogResult = true;
        }

        private void TranslationBtn_Click(object sender, RoutedEventArgs e)
        {
            Result = FileTypeResult.Translation;
            DialogResult = true;
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Result = FileTypeResult.Cancel;
            DialogResult = false;
        }
    }
}
