using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SimpleXmlEditor.Dictionary;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor
{
    public partial class MainWindow
    {
        private void EvaluateBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.EvaluateCommand.Execute(GetSelectedEntries());
        }

        private void VoteBtn_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.VoteCommand.Execute(GetSelectedEntries());
        }

        private void CtxEvaluate_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.EvaluateCommand.Execute(GetSelectedEntries());
        }

        private void CtxVote_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.VoteCommand.Execute(GetSelectedEntries());
        }

        private void MenuSmartPreTrans_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SmartPreTranslateCommand.Execute(GetSelectedEntries());
        }

        private void MenuConsistency_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ConsistencyScanCommand.Execute(null);
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var cfg = _viewModel.ConfigService.Config;
            var settings = new SettingsWindow(
                _viewModel.AiTranslationService.ApiKey,
                _viewModel.AiTranslationService.Model,
                _viewModel.AiTranslationService.TargetLanguage,
                _viewModel.ProgramLanguage,
                _viewModel.CustomPrompt,
                _viewModel.ActiveExpertProfileName,
                _viewModel.AiProvider,
                this,
                _viewModel.ProfileManager,
                cfg.EvaluationAiProvider,
                _viewModel.ConfigService.GetEvaluationApiKey(),
                cfg.EvaluationModel,
                cfg.EvaluationModels,
                cfg.DisableThinking);
            if (settings.ShowDialog() == true)
            {
                _viewModel.AiTranslationService.ApiKey = settings.ApiKey;
                _viewModel.AiTranslationService.Model = settings.Model;
                _viewModel.AiTranslationService.TargetLanguage = settings.TargetLanguage;
                _viewModel.AiProvider = settings.AiProvider;

                // 评估模型配置
                _viewModel.ConfigService.UpdateConfig(c =>
                {
                    c.EvaluationAiProvider = settings.EvalAiProvider;
                    c.EvaluationModel = settings.EvalModel;
                    c.DisableThinking = settings.DisableThinking;
                });
                _viewModel.ConfigService.SetEvaluationApiKey(settings.EvalApiKey);
                _viewModel.ConfigService.SaveEvaluationModels(settings.EvalModels);

                if (_viewModel.ProgramLanguage != settings.ProgramLanguage)
                {
                    _viewModel.ProgramLanguage = settings.ProgramLanguage;
                    LocalizationManager.CurrentLanguage = _viewModel.ProgramLanguage;
                    ApplyLocalization();
                }

                _viewModel.CustomPrompt = settings.CustomPrompt;
                _viewModel.ActiveExpertProfileName = settings.ActiveExpertProfile;

                _viewModel.SaveConfig();
                RefreshExpertProfileCombo();
                AddLog($"✅ {LocalizationManager.GetString("LogSettingsUpdated", _viewModel.AiProvider, _viewModel.AiTranslationService.Model, _viewModel.AiTranslationService.TargetLanguage, _viewModel.ActiveExpertProfileName.Length > 0 ? _viewModel.ActiveExpertProfileName : "None")}");
            }
        }

        private void StatsBtn_Click(object sender, RoutedEventArgs e)
        {
            var total = _viewModel.Entries.Count;
            var translated = _viewModel.Entries.Count(entry => !string.IsNullOrEmpty(entry.Translation));
            var untranslated = total - translated;
            var progress = total > 0 ? (translated * 100.0 / total) : 0;

            var stats = LocalizationManager.GetString("StatsInfo", total, translated, untranslated, progress, _viewModel.Glossary.Count, _viewModel.GlossaryHits, _viewModel.ConfigService.Cache.Count, _viewModel.CacheHits, _viewModel.ApiCalls);

            MessageBox.Show(stats, LocalizationManager.GetString("StatsTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GlossaryBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = new GlossaryWindow(_viewModel.Glossary);
            window.Owner = this;
            window.ConflictsDetected += (_) =>
            {
                var entryList = _viewModel.Entries
                    .Where(ent => !string.IsNullOrEmpty(ent.Translation))
                    .Select(ent => (ent.Key, ent.Value, ent.Translation))
                    .ToList();

                AddLog(LocalizationManager.GetString("LogConflictStart", entryList.Count));

                Task.Run(() => _viewModel.Glossary.DetectConflicts(entryList, (processed, total) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                        AddLog(LocalizationManager.GetString("LogConflictProgress", processed, total))));
                }))
                    .ContinueWith(t =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                var conflicts = t.Result;
                                AddLog(LocalizationManager.GetString("LogConflictDone", conflicts.Count));
                                ShowConflictResults(conflicts);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(this, ex.Message, "Error",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }));
                    });
            };
            window.ShowDialog();
            var candidates = _viewModel.Entries.Where(en => string.IsNullOrEmpty(en.Translation)).ToList();
            if (candidates.Count > 0)
                _viewModel.PushUndoSnapshot(candidates);
            int applied = 0;
            foreach (var entry in candidates)
            {
                if (_viewModel.TryApplyDictionary(entry))
                    applied++;
            }
            UpdateGlossaryInfo();
        }

        private void BlacklistBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = new BlacklistWindow(_viewModel.BlacklistManager)
            {
                Owner = this
            };
            window.ShowDialog();

            // 规则变更后刷新条目黑名单标记，状态列立即反映跳过状态
            _viewModel.RefreshBlacklistFlags();
            ApplyFilter();
            UpdateGlossaryInfo();
        }

        private void ClearDictBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(LocalizationManager.GetString("ConfirmClearDict", _viewModel.Glossary.Count),
                LocalizationManager.GetString("MsgConfirm"), MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.Glossary.Clear();
                _viewModel.GlossaryHits = 0;
                UpdateGlossaryInfo();
                AddLog($"🗑️ {LocalizationManager.GetString("LogDictCleared")}");
            }
        }
    }
}
