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
        public bool DisableThinking { get; private set; }
        public string EvalAiProvider { get; private set; } = "";
        public string EvalApiKey { get; private set; } = "";
        public string EvalModel { get; private set; } = "";
        /// <summary>评估/投票模型列表（明文 Key，由 ConfigService 负责加密存储）。</summary>
        public List<(string Provider, string Model, string ApiKey)> EvalModels { get; private set; } = new();
        private readonly List<EvalModelItem> _evalModels = new();
        private readonly MainWindow _mainWindow;
        private readonly IExpertProfileManager _profileManager;

        public SettingsWindow(string currentApiKey, string currentModel, string currentTargetLanguage,
            string currentProgramLanguage, string currentCustomPrompt, string currentActiveExpertProfile,
            AIProvider currentAiProvider, MainWindow mainWindow, IExpertProfileManager profileManager,
            string currentEvalProvider = "", string currentEvalApiKey = "", string currentEvalModel = "",
            List<EvaluationModelConfig> currentEvalModels = null,
            bool currentDisableThinking = true)
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
            
            ApiKeyTextBox.Password = currentApiKey;
            DisableThinkingCheckBox.IsChecked = currentDisableThinking;

            // 评估模型配置
            EvalApiKeyTextBox.Password = currentEvalApiKey;
            EvalModelComboBox.Text = currentEvalModel;
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

            // 加载已配置的评估模型列表（解密 Key 用于再次编辑）
            if (currentEvalModels != null)
            {
                foreach (var m in currentEvalModels)
                {
                    if (string.IsNullOrEmpty(m.Provider) || string.IsNullOrEmpty(m.Model)) continue;
                    _evalModels.Add(new EvalModelItem
                    {
                        Provider = m.Provider,
                        Model = m.Model,
                        ApiKey = _mainWindow.ConfigService.GetEvaluationModelKey(m)
                    });
                }
            }
            EvalModelsListBox.ItemsSource = _evalModels;
            UpdateEvalListEmptyState();
            
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

            // Disable thinking section
            DisableThinkingLabel.Text = $"🧠 {L("DisableThinking")}";
            DisableThinkingHelpText.Text = L("DisableThinkingHelp");

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
            HandyControl.Controls.InfoElement.SetPlaceholder(EvalModelComboBox, L("EvalModelPlaceholder"));
            EvalRefreshModelsBtn.Content = $"🔄 {L("EvalRefreshModels")}";
            EvalAddedModelsLabel.Text = $"📋 {L("EvalAddedModels")}";
            EvalAddModelBtn.Content = $"➕ {L("EvalAddModel")}";
            EvalModelListEmptyText.Text = L("EvalModelListEmpty");
            EvalUseSameKeyHintText.Text = L("EvalUseSameKeyHint");
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
    }
}
