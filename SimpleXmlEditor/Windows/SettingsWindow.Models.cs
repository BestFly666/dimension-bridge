using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    public partial class SettingsWindow
    {
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentProvider = GetSelectedProvider();
                
                if (currentProvider != AIProvider.GoogleGemini)
                {
                    var apiKey = ApiKeyTextBox.Password.Trim();
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        await RefreshModelsInternalAsync(apiKey);
                        SelectSavedModel();
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(Model))
                        {
                            ModelComboBox.Items.Add(new ComboBoxItem
                            {
                                Content = Model + " " + LocalizationManager.GetString("LoadModelRefreshHint"),
                                Tag = Model
                            });
                            ModelComboBox.SelectedIndex = 0;
                        }
                    }
                    return;
                }
                
                var geminiApiKey = ApiKeyTextBox.Password.Trim();
                if (!string.IsNullOrEmpty(geminiApiKey))
                {
                    await RefreshModelsInternalAsync(geminiApiKey);
                    SelectSavedModel();
                }
                else
                {
                    if (!string.IsNullOrEmpty(Model))
                    {
                        ModelComboBox.Items.Add(new ComboBoxItem
                        {
                            Content = Model + " " + LocalizationManager.GetString("LoadModelRefreshHint"),
                            Tag = Model
                        });
                        ModelComboBox.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings Load error: {ex.Message}");
            }
        }

        private void SelectSavedModel()
        {
            foreach (ComboBoxItem item in ModelComboBox.Items)
            {
                if (item.Tag?.ToString() == Model || 
                    item.Content?.ToString().StartsWith(Model) == true)
                {
                    ModelComboBox.SelectedItem = item;
                    return;
                }
            }
            if (ModelComboBox.SelectedItem == null && ModelComboBox.Items.Count > 0)
            {
                ModelComboBox.SelectedIndex = 0;
            }
        }

        private async void AiProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                var newProvider = GetSelectedProvider();
                
                var apiKey = ApiKeyTextBox.Password.Trim();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    await RefreshModelsInternalAsync(apiKey);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Provider selection changed error: {ex.Message}");
            }
        }

        #region General Settings

        private async void RefreshModelsBtn_Click(object sender, RoutedEventArgs e)
        {
            var apiKey = ApiKeyTextBox.Password.Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show(LocalizationManager.GetString("EnterAPIKeyFirst"), LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await RefreshModelsInternalAsync(apiKey);
        }

        private AIProvider GetSelectedProvider()
        {
            if (AiProviderComboBox.SelectedItem is ComboBoxItem item)
            {
                var tag = item.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag) && Enum.TryParse<AIProvider>(tag, out AIProvider provider))
                    return provider;
            }
            return AIProvider.GoogleGemini;
        }

        private async Task RefreshModelsInternalAsync(string apiKey)
        {
            RefreshModelsBtn.IsEnabled = false;
            RefreshModelsBtn.Content = LocalizationManager.GetString("Loading");

            try
            {
                var selectedProvider = GetSelectedProvider();
                var models = await _mainWindow.FetchAvailableModelsAsync(apiKey, selectedProvider);

                if (models.Count > 0)
                {
                    ModelComboBox.Items.Clear();
                    
                    var rateLimits = _mainWindow.GetModelLimits(selectedProvider);
                    
                    foreach (var model in models)
                    {
                        var displayText = model;
                        
                        if (rateLimits.ContainsKey(model))
                        {
                            var limits = rateLimits[model];
                            var rpmText = limits.requestsPerMinute > 0 ? limits.requestsPerMinute.ToString() : "∞";
                            var rpdText = limits.requestsPerDay > 0 ? limits.requestsPerDay.ToString() : "∞";
                            var tpmText = limits.tokensPerMinute > 0 ? $"{limits.tokensPerMinute / 1000}K" : "∞";
                            
                            displayText = $"{model} ({rpmText}/min, {rpdText}/day, {tpmText} tokens)";
                        }
                        
                        var item = new System.Windows.Controls.ComboBoxItem
                        {
                            Content = displayText,
                            Tag = model
                        };
                        
                        ModelComboBox.Items.Add(item);
                    }
                    
                    if (ModelComboBox.Items.Count > 0)
                    {
                        ModelComboBox.SelectedIndex = 0;
                    }
                    
                    MessageBox.Show(LocalizationManager.GetString("ModelsFoundSuccess", models.Count), LocalizationManager.GetString("MsgSuccess"), 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(LocalizationManager.GetString("NoModelsFound"), LocalizationManager.GetString("MsgError"), 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(LocalizationManager.GetString("ErrorFetchingModels", ex.Message), LocalizationManager.GetString("MsgError"), 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RefreshModelsBtn.IsEnabled = true;
                RefreshModelsBtn.Content = $"🔄 {LocalizationManager.GetString("Refresh")}";
            }
        }

        #endregion

        #region Evaluation Models

        private string GetSelectedEvalProvider()
        {
            if (EvalProviderComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                return item.Tag?.ToString() ?? "";
            return "";
        }

        /// <summary>拉取当前厂商的模型列表填充评估模型下拉（与翻译模型一致）。</summary>
        private async void EvalRefreshModelsBtn_Click(object sender, RoutedEventArgs e)
        {
            var providerStr = GetSelectedEvalProvider();
            if (string.IsNullOrEmpty(providerStr) || !Enum.TryParse<AIProvider>(providerStr, out var provider))
            {
                MessageBox.Show(LocalizationManager.GetString("EvalSelectModelFirst"), LocalizationManager.GetString("MsgWarning"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var apiKey = EvalApiKeyTextBox.Password.Trim();
            if (string.IsNullOrEmpty(apiKey))
                apiKey = ApiKeyTextBox.Password.Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show(LocalizationManager.GetString("EnterAPIKeyFirst"), LocalizationManager.GetString("MsgError"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            EvalRefreshModelsBtn.IsEnabled = false;
            EvalRefreshModelsBtn.Content = LocalizationManager.GetString("Loading");
            try
            {
                var models = await _mainWindow.FetchAvailableModelsAsync(apiKey, provider);
                if (models.Count > 0)
                {
                    EvalModelComboBox.Items.Clear();
                    foreach (var model in models)
                    {
                        EvalModelComboBox.Items.Add(new System.Windows.Controls.ComboBoxItem
                        {
                            Content = model,
                            Tag = model
                        });
                    }
                    EvalModelComboBox.SelectedIndex = 0;
                    MessageBox.Show(LocalizationManager.GetString("ModelsFoundSuccess", models.Count),
                        LocalizationManager.GetString("MsgSuccess"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(LocalizationManager.GetString("NoModelsFound"), LocalizationManager.GetString("MsgError"),
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(LocalizationManager.GetString("ErrorFetchingModels", ex.Message), LocalizationManager.GetString("MsgError"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EvalRefreshModelsBtn.IsEnabled = true;
                EvalRefreshModelsBtn.Content = $"🔄 {LocalizationManager.GetString("EvalRefreshModels")}";
            }
        }

        /// <summary>把当前 (厂商 + 模型 + Key) 加入已配置评估模型列表。</summary>
        private void EvalAddModelBtn_Click(object sender, RoutedEventArgs e)
        {
            var provider = GetSelectedEvalProvider();
            if (string.IsNullOrEmpty(provider))
            {
                MessageBox.Show(LocalizationManager.GetString("EvalSelectModelFirst"), LocalizationManager.GetString("MsgWarning"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var model = EvalModelComboBox.Text.Trim();
            if (string.IsNullOrEmpty(model))
            {
                MessageBox.Show(LocalizationManager.GetString("EvalSelectModelFirst"), LocalizationManager.GetString("MsgWarning"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _evalModels.Add(new EvalModelItem
            {
                Provider = provider,
                Model = model,
                ApiKey = EvalApiKeyTextBox.Password.Trim()
            });
            EvalModelsListBox.ItemsSource = null;
            EvalModelsListBox.ItemsSource = _evalModels;
            UpdateEvalListEmptyState();
            EvalModelComboBox.SelectedIndex = -1;
        }

        private void EvalRemoveModelBtn_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as System.Windows.Controls.Button)?.DataContext is EvalModelItem item)
            {
                _evalModels.Remove(item);
                EvalModelsListBox.ItemsSource = null;
                EvalModelsListBox.ItemsSource = _evalModels;
                UpdateEvalListEmptyState();
            }
        }

        private void UpdateEvalListEmptyState()
        {
            EvalModelListEmptyText.Visibility = _evalModels.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        #endregion
    }

    /// <summary>设置窗口中"已配置评估模型"列表项。</summary>
    public class EvalModelItem
    {
        public string Provider { get; set; } = "";
        public string Model { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string Display => $"{FormatProviderName(Provider)} | {Model}";

        private static string FormatProviderName(string provider) => provider switch
        {
            "GoogleGemini" => "Gemini",
            "DeepSeek" => "DeepSeek",
            "Doubao" => "豆包",
            "Qianwen" => "千问",
            "Zhipu" => "智谱",
            "Moonshot" => "Kimi",
            "Wenxin" => "文心一言",
            "Xunfei" => "讯飞星火",
            _ => provider
        };
    }
}
