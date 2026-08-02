using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SimpleXmlEditor.ExpertProfiles;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    public partial class SettingsWindow : Window
    {
        public string ApiKey { get; private set; }
        public string Model { get; private set; }
        public string TargetLanguage { get; private set; }
        public string ProgramLanguage { get; private set; }
        public string CustomPrompt { get; private set; }
        public string ActiveExpertProfile { get; private set; }
        public AIProvider AiProvider { get; private set; }
        public string EvalAiProvider { get; private set; } = "";
        public string EvalApiKey { get; private set; } = "";
        public string EvalModel { get; private set; } = "";
        private readonly MainWindow _mainWindow;
        private readonly IExpertProfileManager _profileManager;

        public SettingsWindow(string currentApiKey, string currentModel, string currentTargetLanguage,
            string currentProgramLanguage, string currentCustomPrompt, string currentActiveExpertProfile,
            AIProvider currentAiProvider, MainWindow mainWindow, IExpertProfileManager profileManager,
            string currentEvalProvider = "", string currentEvalApiKey = "", string currentEvalModel = "")
        {
            InitializeComponent();
            
            // Subscribe to language changes
            LocalizationManager.LanguageChanged += ApplyLocalization;
            Closed += (_, _) => LocalizationManager.LanguageChanged -= ApplyLocalization;
            ApplyLocalization();            
            _mainWindow = mainWindow;
            _profileManager = profileManager;
            ActiveExpertProfile = currentActiveExpertProfile;
            AiProvider = currentAiProvider;
            
            // Set current AI provider
            foreach (System.Windows.Controls.ComboBoxItem item in AiProviderComboBox.Items)
            {
                if (item.Tag?.ToString() == currentAiProvider.ToString())
                {
                    AiProviderComboBox.SelectedItem = item;
                    break;
                }
            }
            
            if (AiProviderComboBox.SelectedItem == null && AiProviderComboBox.Items.Count > 0)
            {
                AiProviderComboBox.SelectedIndex = 0;
            }
            
            ApiKeyTextBox.Text = currentApiKey;

            // 评估模型配置
            EvalApiKeyTextBox.Text = currentEvalApiKey;
            EvalModelTextBox.Text = currentEvalModel;
            foreach (System.Windows.Controls.ComboBoxItem item in EvalProviderComboBox.Items)
            {
                if (item.Tag?.ToString() == currentEvalProvider)
                {
                    EvalProviderComboBox.SelectedItem = item;
                    break;
                }
            }
            if (EvalProviderComboBox.SelectedItem == null && EvalProviderComboBox.Items.Count > 0)
                EvalProviderComboBox.SelectedIndex = 0;
            
            // Set current model
            foreach (System.Windows.Controls.ComboBoxItem item in ModelComboBox.Items)
            {
                if (item.Tag?.ToString() == currentModel || 
                    item.Content?.ToString().StartsWith(currentModel) == true)
                {
                    ModelComboBox.SelectedItem = item;
                    break;
                }
            }
            
            if (ModelComboBox.SelectedItem == null && ModelComboBox.Items.Count > 0)
            {
                ModelComboBox.SelectedIndex = 0;
            }

            // Set current target language
            foreach (System.Windows.Controls.ComboBoxItem item in TargetLanguageComboBox.Items)
            {
                if (item.Tag?.ToString() == currentTargetLanguage)
                {
                    TargetLanguageComboBox.SelectedItem = item;
                    break;
                }
            }
            
            if (TargetLanguageComboBox.SelectedItem == null)
            {
                TargetLanguageComboBox.SelectedIndex = 0;
            }

            // Set current program language
            foreach (System.Windows.Controls.ComboBoxItem item in ProgramLanguageComboBox.Items)
            {
                if (item.Tag?.ToString() == currentProgramLanguage)
                {
                    ProgramLanguageComboBox.SelectedItem = item;
                    break;
                }
            }
            
            if (ProgramLanguageComboBox.SelectedItem == null)
            {
                ProgramLanguageComboBox.SelectedIndex = 0;
            }

            // Set custom prompt
            CustomPromptTextBox.Text = string.IsNullOrEmpty(currentCustomPrompt) ? GetDefaultPrompt() : currentCustomPrompt;

            // Load profiles
            RefreshProfilesList();
        }

        private void ApplyLocalization()
        {
            Func<string, string> L = LocalizationManager.GetString;

            // Window title
            this.Title = L("SettingsTitle");

            // Header
            SettingsTitleLabel.Text = L("Settings");
            SettingsSubtitleLabel.Text = L("SettingsSubtitle");

            // Tab headers
            GeneralSettingsTab.Header = $"  🔧  {L("GeneralSettings")}  ";
            ExpertProfilesTab.Header = $"  🧠  {L("ExpertProfiles")}  ";

            // AI Provider section
            AiProviderHeader.Text = $"🔌 {L("AiProviderLabel")}";
            SelectAiProviderText.Text = L("SelectAiProvider");

            // API Key section
            ApiKeyHeader.Text = $"🔑 {L("APIKey")}";
            EnterYourApiKeyText.Text = L("EnterYourApiKey");

            // Model section
            AiModelHeader.Text = $"🤖 {L("AIModel")}";
            AiModelHelpText.Text = L("SelectModel");
            RefreshModelsBtn.Content = $"🔄 {L("Refresh")}";

            // Target language section
            TargetLangHeader.Text = $"🌍 {L("TargetLanguage")}";
            TargetLangHelpText.Text = L("SelectTargetLanguage");
            RebuildTargetLanguageComboBox();

            // Program language section
            ProgramLangHeader.Text = $"🌐 {L("ProgramLanguage")}";
            ProgramLangHelpText.Text = L("SelectProgramLanguage");

            // Custom prompt section
            CustomPromptHeader.Text = $"📝 {L("CustomPrompt")}";
            ResetPromptBtn.Content = $"🔄 {L("Reset")}";

            // Custom prompt help
            CustomPromptHelpContent.Text = L("CustomPromptSyntaxHelp");

            // Quick tips
            QuickTipsTitle.Text = $"💡 {L("QuickTipsTitle")}";
            QuickTipsContent.Text = L("QuickTipsContent");

            // Buttons
            CancelButton.Content = L("Cancel");

            // Evaluation model tab
            EvaluationModelTab.Header = $"  🔍  {L("EvalModelTab")}  ";
            EvalModelConfigTitle.Text = $"🔍 {L("EvalModelConfig")}";
            EvalModelConfigDesc.Text = L("EvalModelDesc");
            EvalProviderHeader.Text = $"🔌 {L("EvalAiProviderLabel")}";
            EvalUseTranslationModelItem.Content = L("EvalUseTranslationModel");
            EvalApiKeyHeader.Text = $"🔑 {L("EvalApiKeyLabel")}";
            HandyControl.Controls.InfoElement.SetPlaceholder(EvalApiKeyTextBox, L("EvalApiKeyPlaceholder"));
            EvalModelHeader.Text = $"🤖 {L("EvalModelNameLabel")}";
            HandyControl.Controls.InfoElement.SetPlaceholder(EvalModelTextBox, L("EvalModelPlaceholder"));
            OkButton.Content = L("SaveApply");

            // Expert profiles tab
            ExpertSystemTitle.Text = $"🧠 {L("ExpertSystemTitle")}";
            ExpertSystemDesc.Text = L("ExpertSystemDesc");
            SavedProfilesLabel.Text = $"📋 {L("SavedProfiles")}";
            AddProfileBtn.Content = $"➕ {L("AddProfile")}";

            // Profile editor
            ProfileEditorTitle.Text = L("ProfileEditTitle");
            ProfileNameHeader.Text = L("ProfileNameLabel");
            ProfileDescHeader.Text = L("ProfileDescLabel");
            ProfileContextHeader.Text = L("ProfileContextLabel");
            ProfileContextHelp.Text = L("ProfileContextHelp");
            ProfileGlossaryHeader.Text = L("ProfileGlossaryLabel");
            ProfileGlossaryHelp.Text = L("ProfileGlossaryHelp");
            CancelProfileBtn.Content = L("Cancel");
            SaveProfileBtn.Content = $"💾 {L("SaveProfileBtn")}";
        }

        /// <summary>
        /// Rebuilds the TargetLanguageComboBox with localized language names.
        /// Preserves the currently selected item by matching Tag.
        /// </summary>
        private void RebuildTargetLanguageComboBox()
        {
            var currentTag = (TargetLanguageComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (string.IsNullOrEmpty(currentTag))
                currentTag = TargetLanguage; // Preserve from constructor

            TargetLanguageComboBox.Items.Clear();

            var languages = new[]
            {
                new { Tag = "Turkish", Flag = "🇹🇷", Native = "Türkçe", Key = "Lang_Turkish" },
                new { Tag = "Spanish", Flag = "🇪🇸", Native = "Español", Key = "Lang_Spanish" },
                new { Tag = "French", Flag = "🇫🇷", Native = "Français", Key = "Lang_French" },
                new { Tag = "German", Flag = "🇩🇪", Native = "Deutsch", Key = "Lang_German" },
                new { Tag = "Italian", Flag = "🇮🇹", Native = "Italiano", Key = "Lang_Italian" },
                new { Tag = "Portuguese", Flag = "🇵🇹", Native = "Português", Key = "Lang_Portuguese" },
                new { Tag = "Russian", Flag = "🇷🇺", Native = "Русский", Key = "Lang_Russian" },
                new { Tag = "Japanese", Flag = "🇯🇵", Native = "日本語", Key = "Lang_Japanese" },
                new { Tag = "Korean", Flag = "🇰🇷", Native = "한국어", Key = "Lang_Korean" },
                new { Tag = "Chinese (Simplified)", Flag = "🇨🇳", Native = "简体中文", Key = "Lang_Chinese_Simplified" },
                new { Tag = "Chinese (Traditional)", Flag = "🇹🇼", Native = "繁體中文", Key = "Lang_Chinese_Traditional" },
                new { Tag = "Arabic", Flag = "🇦🇪", Native = "العربية", Key = "Lang_Arabic" },
                new { Tag = "Hindi", Flag = "🇮🇳", Native = "हिन्दी", Key = "Lang_Hindi" },
                new { Tag = "Dutch", Flag = "🇳🇱", Native = "Nederlands", Key = "Lang_Dutch" },
                new { Tag = "Swedish", Flag = "🇸🇪", Native = "Svenska", Key = "Lang_Swedish" },
                new { Tag = "Norwegian", Flag = "🇳🇴", Native = "Norsk", Key = "Lang_Norwegian" },
                new { Tag = "Danish", Flag = "🇩🇰", Native = "Dansk", Key = "Lang_Danish" },
                new { Tag = "Finnish", Flag = "🇫🇮", Native = "Suomi", Key = "Lang_Finnish" },
                new { Tag = "Polish", Flag = "🇵🇱", Native = "Polski", Key = "Lang_Polish" },
                new { Tag = "Czech", Flag = "🇨🇿", Native = "Čeština", Key = "Lang_Czech" },
                new { Tag = "Hungarian", Flag = "🇭🇺", Native = "Magyar", Key = "Lang_Hungarian" },
                new { Tag = "Romanian", Flag = "🇷🇴", Native = "Română", Key = "Lang_Romanian" },
                new { Tag = "Greek", Flag = "🇬🇷", Native = "Ελληνικά", Key = "Lang_Greek" },
                new { Tag = "Bulgarian", Flag = "🇧🇬", Native = "Български", Key = "Lang_Bulgarian" },
                new { Tag = "Ukrainian", Flag = "🇺🇦", Native = "Українська", Key = "Lang_Ukrainian" },
                new { Tag = "Thai", Flag = "🇹🇭", Native = "ไทย", Key = "Lang_Thai" },
                new { Tag = "Vietnamese", Flag = "🇻🇳", Native = "Tiếng Việt", Key = "Lang_Vietnamese" },
                new { Tag = "Indonesian", Flag = "🇮🇩", Native = "Bahasa Indonesia", Key = "Lang_Indonesian" },
                new { Tag = "Hebrew", Flag = "🇮🇱", Native = "עברית", Key = "Lang_Hebrew" },
                new { Tag = "Persian", Flag = "🇮🇷", Native = "فارسی", Key = "Lang_Persian" }
            };

            foreach (var lang in languages)
            {
                var displayName = LocalizationManager.GetString(lang.Key);
                var item = new ComboBoxItem
                {
                    Content = $"{lang.Flag} {displayName} ({lang.Native})",
                    Tag = lang.Tag
                };
                TargetLanguageComboBox.Items.Add(item);
            }

            // Restore selection
            if (!string.IsNullOrEmpty(currentTag))
            {
                foreach (ComboBoxItem item in TargetLanguageComboBox.Items)
                {
                    if (item.Tag?.ToString() == currentTag)
                    {
                        TargetLanguageComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            if (TargetLanguageComboBox.SelectedItem == null && TargetLanguageComboBox.Items.Count > 0)
                TargetLanguageComboBox.SelectedIndex = 0;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var currentProvider = GetSelectedProvider();
                
                if (currentProvider != AIProvider.GoogleGemini)
                {
                    var apiKey = ApiKeyTextBox.Text.Trim();
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
                
                var geminiApiKey = ApiKeyTextBox.Text.Trim();
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
                
                var apiKey = ApiKeyTextBox.Text.Trim();
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
            var apiKey = ApiKeyTextBox.Text.Trim();
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

            // 评估模型配置
            if (EvalProviderComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem evalItem)
                EvalAiProvider = evalItem.Tag?.ToString() ?? "";
            EvalApiKey = EvalApiKeyTextBox.Text.Trim();
            EvalModel = EvalModelTextBox.Text.Trim();

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

        #region Expert Profile Management

        private void RefreshProfilesList()
        {
            ProfilesListBox.ItemsSource = null;
            ProfilesListBox.ItemsSource = _profileManager.Profiles;
        }

        private void AddProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            ProfileEditorTitle.Text = $"➕ {LocalizationManager.GetString("NewProfile")}";
            ProfileNameTxt.Text = "";
            ProfileDescTxt.Text = "";
            ProfileContextTxt.Text = "";
            ProfileGlossaryTxt.Text = "";
            ProfileEditorPanel.Visibility = Visibility.Visible;
            ProfileNameTxt.Focus();
        }

        private void EditProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var profile = button?.DataContext as ExpertProfile;
            if (profile != null)
            {
                EditProfile(profile);
            }
        }

        private void ProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProfilesListBox.SelectedItem is ExpertProfile profile)
            {
                EditProfile(profile);
            }
        }

        private void EditProfile(ExpertProfile profile)
        {
            ProfileEditorTitle.Text = $"✏️ {LocalizationManager.GetString("EditProfile", profile.Name)}";
            ProfileNameTxt.Text = profile.Name;
            ProfileDescTxt.Text = profile.Description;
            ProfileContextTxt.Text = profile.Context;

            // Convert glossary dictionary to text lines
            var glossaryLines = new System.Text.StringBuilder();
            if (profile.Glossary != null)
            {
                foreach (var kvp in profile.Glossary)
                {
                    glossaryLines.AppendLine($"{kvp.Key} = {kvp.Value}");
                }
            }
            ProfileGlossaryTxt.Text = glossaryLines.ToString().TrimEnd();
            ProfileEditorPanel.Visibility = Visibility.Visible;
        }

        private void DeleteProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var profile = button?.DataContext as ExpertProfile;
            if (profile == null) return;

            var result = MessageBox.Show(
                LocalizationManager.GetString("ConfirmDeleteProfile", profile.Name), 
                LocalizationManager.GetString("MsgConfirm"), 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _profileManager.DeleteProfile(profile.Name);
                if (ActiveExpertProfile == profile.Name)
                    ActiveExpertProfile = "";
                HideProfileEditor();
                RefreshProfilesList();
            }
        }

        private void SaveProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            var name = ProfileNameTxt.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(LocalizationManager.GetString("EnterProfileName"), LocalizationManager.GetString("MsgError"), MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var profile = new ExpertProfile
            {
                Name = name,
                Description = ProfileDescTxt.Text.Trim(),
                Context = ProfileContextTxt.Text.Trim()
            };

            // Parse glossary from text lines
            var glossaryText = ProfileGlossaryTxt.Text.Trim();
            if (!string.IsNullOrEmpty(glossaryText))
            {
                profile.Glossary = new Dictionary<string, string>();
                var lines = glossaryText.Split('\n');
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;

                    var eqIndex = trimmed.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        var term = trimmed.Substring(0, eqIndex).Trim();
                        var translation = trimmed.Substring(eqIndex + 1).Trim();
                        if (!string.IsNullOrEmpty(term) && !string.IsNullOrEmpty(translation))
                        {
                            profile.Glossary[term] = translation;
                        }
                    }
                }
            }

            _profileManager.AddProfile(profile);
            HideProfileEditor();
            RefreshProfilesList();
        }

        private void CancelProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            HideProfileEditor();
        }

        private void HideProfileEditor()
        {
            ProfileEditorPanel.Visibility = Visibility.Collapsed;
            ProfileNameTxt.Text = "";
            ProfileDescTxt.Text = "";
            ProfileContextTxt.Text = "";
            ProfileGlossaryTxt.Text = "";
            ProfilesListBox.SelectedItem = null;
        }

        #endregion
    }
}
