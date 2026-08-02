using System;
using System.Windows;
using System.Windows.Controls;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor
{
    public partial class MainWindow
    {
        private void ApplyLocalization()
        {
            Func<string, string> L = LocalizationManager.GetString;

            this.Title = L("WindowTitle");
            AppNameText.Text = L("AppName");

            LoadBtn.Content = $"📁 {L("Load")}";
            SaveBtn.Content = $"💾 {L("Save")}";
            SaveBtn.ToolTip = L("TipSaveAs");

            foreach (ComboBoxItem item in ExpertProfileCombo.Items)
            {
                if (item.Tag?.ToString() == "")
                {
                    item.Content = L("NoExpertDefault");
                    break;
                }
            }

            StatusText.Text = L("Ready");

            AITranslationTitle.Text = $"🤖 {L("AITranslationCenter")}";
            TranslationDataTitle.Text = $"📋 {L("TranslationData")}";
            ActivityLogTitle.Text = L("ActivityLog");

            TranslateSelectedBtn.Content = $"🎯 {L("TranslateSelected")}";
            TranslateAllBtn.Content = $"🚀 {L("TranslateAll")}";
            ClearLogBtn.Content = "🗑️";

            ClearCacheBtn.Content = $"🗑 {L("ClearCache")}";

            MenuEvaluate.Header = $"🤖 {L("EvaluateBtn")} (F5)";
            MenuEvaluate.ToolTip = L("EvaluateToolTip");
            MenuVote.Header = $"🗳 {L("VoteBtn")} (F6)";
            MenuVote.ToolTip = L("VoteToolTip");
            MenuClearDict.Header = $"🗑️ {L("ClearDict")}";
            MenuExportReview.Header = $"📋 {L("ExportReview")}";

            MenuFile.Header = $"📁 {L("MenuFile")}";
            MenuEdit.Header = $"✏️ {L("MenuEdit")}";
            MenuView.Header = $"👁 {L("MenuView")}";
            MenuTranslate.Header = $"🌐 {L("MenuTranslate")}";
            MenuQuality.Header = $"⭐ {L("MenuQuality")}";
            MenuTools.Header = $"🔧 {L("MenuTools")}";
            MenuHelp.Header = $"❓ {L("MenuHelp")}";

            MenuOpen.Header = $"📂 {L("MenuOpen")} (Ctrl+O)";
            MenuSave.Header = $"💾 {L("MenuSave")} (Ctrl+S)";
            MenuExit.Header = L("MenuExit");
            MenuDarkMode.Header = _isDarkMode ? L("MenuLightMode") : L("MenuDarkMode");
            MenuShowFilter.Header = L("MenuShowFilter");
            MenuShowLog.Header = L("MenuShowLog");
            MenuTranslateSelected.Header = $"🎯 {L("TranslateSelected")}";
            MenuTranslateAll.Header = $"🚀 {L("TranslateAll")}";
            MenuSmartPreTrans.Header = $"🔮 {L("MenuSmartPre")}";
            MenuSmartPreTrans.ToolTip = L("PreTranslateTip");
            MenuConsistency.Header = $"🔍 {L("MenuConsistency")}";
            MenuShortcuts.Header = $"⌨ {L("MenuShortcuts")}";
            MenuAbout.Header = $"ℹ️ {L("MenuAbout")}";

            MenuSettings.Header = $"⚙️ {L("Settings")}";
            MenuStatistics.Header = $"📊 {L("Stats")}";
            MenuGlossary.Header = $"📖 {L("Glossary")}";
            MenuGlossary.ToolTip = L("TipGlossary");
            MenuUndo.Header = $"↩️ {L("Undo")}";
            MenuUndo.ToolTip = L("TipUndo");
            MenuReplace.Header = $"🔄 {L("BatchReplace")}";
            MenuReplace.ToolTip = L("TipBatchReplace");

            BatchLabelText.Text = $"{L("BatchLabel")}:";

            if (EntriesGrid.Columns.Count >= 6)
            {
                EntriesGrid.Columns[0].Header = "✓";
                EntriesGrid.Columns[1].Header = L("Status");
                EntriesGrid.Columns[2].Header = L("Key");
                EntriesGrid.Columns[3].Header = L("Original");
                EntriesGrid.Columns[4].Header = L("Translation");
                EntriesGrid.Columns[5].Header = L("Score");
            }

            FilterKeyBox.ToolTip = L("TipFilterKey");
            FilterBox.ToolTip = L("TipFilterOriginal");
            FilterTranslationBox.ToolTip = L("TipFilterTranslation");
            ClearFilterBtn.ToolTip = L("ClearFilter");
            ClearFilterBtn.Content = $"✕ {L("FilterClear")}";
            UntranslatedToggle.Content = L("ShowUntranslatedOnly");

            CtxCopyKeyMenu.Header = $"📋 {L("CtxCopyKey")}";
            CtxCopyOriginalMenu.Header = $"📋 {L("CtxCopyOriginal")}";
            CtxCopyTranslationMenu.Header = $"📋 {L("CtxCopyTranslation")}";
            CtxClearTranslationMenu.Header = $"🗑️ {L("CtxClearTranslation")}";
            CtxTranslateSelectedMenu.Header = $"🌐 {L("CtxTranslateSelected")}";
            CtxEvaluateMenu.Header = $"🤖 {L("CtxEvaluate")}";
            CtxVoteMenu.Header = $"🗳 {L("CtxVote")}";
            CtxSelectAllMenu.Header = $"☑️ {L("CtxSelectAll")}";
            CtxSelectNoneMenu.Header = $"☐ {L("CtxSelectNone")}";
            CtxInvertSelectionMenu.Header = $"🔄 {L("CtxInvertSelection")}";

            PauseBtn.Content = $"⏸️ {L("Pause")}";
            StopBtn.Content = $"⏹️ {L("Stop")}";

            RealTimeLabel.Text = $"🕒 {L("RealTime")}";
            AutoScrollLabel.Text = $"🔄 {L("AutoScroll")}";

            UpdateInfoLabels();
        }

        private void UpdateInfoLabels()
        {
            CacheInfo.Text = LocalizationManager.GetString("CacheInfo", _viewModel.ConfigService.Cache.Count, _viewModel.CacheHits, _viewModel.ApiCalls, "");
            GlossaryInfo.Text = LocalizationManager.GetString("GlossaryInfo", _viewModel.Glossary.Count, _viewModel.GlossaryHits);
            FilterCountText.Text = LocalizationManager.GetString("TotalCount", _viewModel.Entries.Count);
        }
    }
}
