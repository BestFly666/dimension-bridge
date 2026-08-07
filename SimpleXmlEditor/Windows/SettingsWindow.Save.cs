using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    public partial class SettingsWindow
    {
        #region General Settings

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ApiKey = ApiKeyTextBox.Text.Trim();
            
            // Read AI provider
            if (AiProviderComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem providerItem)
            {
                var tag = providerItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag) && Enum.TryParse<AIProvider>(tag, out AIProvider provider))
                    AiProvider = provider;
            }
            
            if (ModelComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                Model = selectedItem.Tag?.ToString() ?? "";
            }
            else
            {
                Model = ModelComboBox.SelectedItem?.ToString() ?? "";
            }

            if (TargetLanguageComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem targetLangItem)
            {
                TargetLanguage = targetLangItem.Tag?.ToString() ?? "Turkish";
            }
            else
            {
                TargetLanguage = "Turkish";
            }

            if (ProgramLanguageComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem programLangItem)
            {
                ProgramLanguage = programLangItem.Tag?.ToString() ?? "en";
            }
            else
            {
                ProgramLanguage = "en";
            }

            CustomPrompt = CustomPromptTextBox.Text.Trim();
            DisableThinking = DisableThinkingCheckBox.IsChecked == true;

            // 评估模型配置
            if (EvalProviderComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem evalItem)
                EvalAiProvider = evalItem.Tag?.ToString() ?? "";
            EvalApiKey = EvalApiKeyTextBox.Text.Trim();
            EvalModel = EvalModelComboBox.Text.Trim();
            EvalModels = _evalModels.Select(m => (m.Provider, m.Model, m.ApiKey)).ToList();

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ResetPromptBtn_Click(object sender, RoutedEventArgs e)
        {
            CustomPromptTextBox.Text = GetDefaultPrompt();
        }

        private string GetDefaultPrompt()
        {
            return PromptTemplates.DefaultBatchPrompt;
        }

        #endregion
    }
}
