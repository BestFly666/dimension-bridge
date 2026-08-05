using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleXmlEditor.Localization;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.ViewModels
{
    public partial class MainViewModel
    {
        public void StartTranslationTracking(int totalToTranslate)
        {
            _translationStartTime = DateTime.Now;
            _totalToTranslate = totalToTranslate;
            TranslatedCount = 0;
            ProgressPercentage = 0;
            TranslationSpeed = 0;
            EstimatedTimeRemaining = "...";
        }

        public void UpdateTranslationProgress(int translatedCount)
        {
            TranslatedCount = translatedCount;
            if (_totalToTranslate > 0)
            {
                ProgressPercentage = Math.Round(translatedCount * 100.0 / _totalToTranslate, 1);
            }
            var elapsed = (DateTime.Now - _translationStartTime).TotalSeconds;
            if (elapsed > 0.5 && translatedCount > 0)
            {
                TranslationSpeed = Math.Round(translatedCount / elapsed, 1);
                var remaining = _totalToTranslate - translatedCount;
                if (TranslationSpeed > 0)
                {
                    var remainingSeconds = remaining / TranslationSpeed;
                    EstimatedTimeRemaining = remainingSeconds < 60
                        ? $"{remainingSeconds:F0}s"
                        : $"{remainingSeconds / 60:F0}m {remainingSeconds % 60:F0}s";
                }
            }
        }

        public string GetTranslationStatusIndicator()
        {
            if (!IsTranslationRunning) return "⚪";
            return IsTranslationPaused ? "🟡" : "🟢";
        }

        public void TrackRequest()
        {
            _aiTranslationService.TrackRequest();
            _lastRequestTime = DateTime.Now;
        }

        /// <summary>Cancel the running translation pipeline.</summary>
        public void CancelTranslation()
        {
            _translationCts?.Cancel();
        }

        /// <summary>
        /// Full translation pipeline: batch scheduling, pause/resume, cancellation,
        /// progress tracking, incremental saves, and auto-save. UI renders via events.
        /// </summary>
        public async Task TranslateEntriesAsync(List<LocalizationEntry> entries, bool forceRefresh = false)
        {
            _translationCts = new CancellationTokenSource();
            IsTranslationRunning = true;
            IsTranslationPaused = false;

            try
            {
                // Session begin — UI shows controls and resets progress display
                TranslationStarted?.Invoke(0);

                // Reset cost tracking for this translation session
                TotalCost = 0;
                TotalInputChars = 0;
                TotalOutputChars = 0;

                var successCount = 0;
                var failCount = 0;

                // Filter out entries that need translation
                var entriesToTranslate = entries.Where(e => !string.IsNullOrEmpty(e.Value) && string.IsNullOrEmpty(e.Translation)).ToList();

                // Blacklist: exclude entries whose Key matches a blacklist prefix (no API calls)
                if (_blacklistManager.Count > 0)
                {
                    var blocked = entriesToTranslate.Where(e => _blacklistManager.IsBlocked(e.Key, e.Value)).ToList();
                    if (blocked.Count > 0)
                    {
                        OnLogMessage($"🚫 {LocalizationManager.GetString("LogBlacklistSkipped", blocked.Count)}");
                        entriesToTranslate = entriesToTranslate.Where(e => !blocked.Contains(e)).ToList();
                    }
                }

                if (!entriesToTranslate.Any())
                {
                    OnLogMessage($"ℹ️ {LocalizationManager.GetString("LogNoTranslationNeeded")}");
                    RaiseStatusMessage(LocalizationManager.GetString("NoEntriesForTranslation"));
                    return;
                }

                // Record undo snapshot before mutating translations
                PushUndoSnapshot(entriesToTranslate);

                // Create batches based on token limits
                var batches = _orchestrator.CreateBatches(entriesToTranslate, CustomPrompt, BatchSize);

                StartTranslationTracking(entriesToTranslate.Count);
                TranslationStarted?.Invoke(entriesToTranslate.Count);

                OnLogMessage($"🌍 {LocalizationManager.GetString("LogBatchStart", entriesToTranslate.Count, batches.Count, forceRefresh ? " (force refresh)" : "")}");
                OnLogMessage($"📊 {LocalizationManager.GetString("LogBatchModel", _aiTranslationService.Model)}");

                // 并发批次处理：初始并发度 3，429 时动态降低，配额恢复后回升
                var maxConcurrentBatches = 3;
                var batchSemaphore = new SemaphoreSlim(maxConcurrentBatches, maxConcurrentBatches);
                var runningTasks = new List<Task>();

                for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
                {
                    // Check for cancellation before starting new batch
                    if (_translationCts.Token.IsCancellationRequested)
                    {
                        OnLogMessage($"⏹️ {LocalizationManager.GetString("LogBatchCancelled", batchIndex + 1, batches.Count)}");
                        break;
                    }

                    // Handle pause
                    while (IsTranslationPaused && !_translationCts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(500, _translationCts.Token);
                    }

                    if (_translationCts.Token.IsCancellationRequested)
                        break;

                    var batch = batches[batchIndex];
                    var localBatchIndex = batchIndex;

                    // Wait for a slot (limit concurrent batches)
                    await batchSemaphore.WaitAsync(_translationCts.Token);

                    // Start batch task (returns immediately, runs in background)
                    var batchTask = Task.Run(async () =>
                    {
                        try
                        {
                            // Track request for rate limiting
                            TrackRequest();

                            // 批次计时：Stopwatch 包裹 API 调用，含拆半重试的总耗时
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            var batchResults = await _orchestrator.TranslateBatchAsync(batch, forceRefresh, CustomPrompt);
                            sw.Stop();

                            // Apply translations
                            var batchSuccessCount = 0;
                            var batchFailCount = 0;

                            foreach (var entry in batch)
                            {
                                if (batchResults.ContainsKey(entry.Value))
                                {
                                    entry.Translation = batchResults[entry.Value];
                                    batchSuccessCount++;
                                }
                                else
                                {
                                    batchFailCount++;
                                }
                            }

                            // Thread-safe counters update
                            lock (_translationLock)
                            {
                                successCount += batchSuccessCount;
                                failCount += batchFailCount;

                                var totalTranslated = successCount + failCount;
                                UpdateTranslationProgress(totalTranslated);
                                TranslationProgressChanged?.Invoke(totalTranslated, entriesToTranslate.Count);
                            }

                            if (batchFailCount > 0)
                            {
                                var failedKeys = batch.Where(e => !batchResults.ContainsKey(e.Value))
                                    .Select(e => e.Key.Length > 40 ? e.Key[..40] : e.Key);
                                OnLogMessage($"❌ {LocalizationManager.GetString("LogBatchFails", batchFailCount, string.Join(", ", failedKeys.Take(5)))}");
                            }

                            OnLogMessage($"📊 {LocalizationManager.GetString("LogBatchDone", localBatchIndex + 1, batches.Count, batchSuccessCount, batchFailCount)}");
                            OnLogMessage($"⏱️ {LocalizationManager.GetString("LogBatchElapsed", localBatchIndex + 1, batches.Count, sw.Elapsed.TotalSeconds.ToString("F1"), batch.Count)}");
                        }
                        finally
                        {
                            // 先释放信号量，让下一批立即启动；再保存进度（不阻塞并发调度）
                            batchSemaphore.Release();
                            await _configService.SaveTranslationProgressAsync(Entries);
                        }
                    }, _translationCts.Token);

                    runningTasks.Add(batchTask);

                    // Log batch start (after task is queued)
                    RaiseStatusMessage(LocalizationManager.GetString("TranslatingBatch", batchIndex + 1, batches.Count, batch.Count));
                    OnLogMessage($"🔄 {LocalizationManager.GetString("LogBatchProgress", batchIndex + 1, batches.Count, batch.Count)}");
                }

                // Wait for all batches to complete (or cancellation)
                try
                {
                    await Task.WhenAll(runningTasks);
                }
                catch (OperationCanceledException)
                {
                    // Some tasks were cancelled, that's fine
                }
                catch (AggregateException)
                {
                    // Some batch tasks failed (e.g., truncation detected), but continue to save completed translations
                }
                finally
                {
                    UpdateTranslationProgress(entriesToTranslate.Count);
                    TranslationProgressChanged?.Invoke(entriesToTranslate.Count, entriesToTranslate.Count);

                    // 无论成功、取消、失败，都保存已完成的翻译（finally 确保 Cache 持久化）
                    // 注意：不写回 XML 原文文件——译文经缓存持久化，重开加载时自动恢复；
                    // 显式"保存"按钮才导出 XML（译文替换，供游戏使用），避免覆盖英文原文
                    if (successCount > 0)
                    {
                        SaveCache();
                        OnLogMessage($"💾 {LocalizationManager.GetString("LogCacheSaved")}");
                        // Translation complete — delete recovery file
                        _configService.DeleteProgressFile();
                    }
                }

                var statusMessage = _translationCts.Token.IsCancellationRequested
                    ? LocalizationManager.GetString("StatusStoppedResult", successCount, failCount)
                    : LocalizationManager.GetString("StatusBatchComplete", successCount, failCount);

                RaiseStatusMessage(statusMessage);
                OnLogMessage($"🎉 {LocalizationManager.GetString("LogTranslationDone", statusMessage)}");

                if (failCount > 0)
                {
                    OnLogMessage($"💡 {LocalizationManager.GetString("LogTipHeader")}");
                    OnLogMessage(LocalizationManager.GetString("LogTip1"));
                    OnLogMessage(LocalizationManager.GetString("LogTip2"));
                    OnLogMessage(LocalizationManager.GetString("LogTip3"));
                }

                // Show efficiency stats
                var efficiency = entriesToTranslate.Count > 0 ? (successCount * 100.0 / entriesToTranslate.Count) : 0;
                OnLogMessage($"📈 {LocalizationManager.GetString("LogEfficiency", efficiency.ToString("F1"), successCount, entriesToTranslate.Count)}");
                OnLogMessage($"⚡ {LocalizationManager.GetString("LogBatchEfficiency", batches.Count, entriesToTranslate.Count, entriesToTranslate.Count - batches.Count)}");

                // Show rate limit summary
                if (_aiTranslationService.ModelLimits.ContainsKey(_aiTranslationService.Model))
                {
                    var limits = _aiTranslationService.ModelLimits[_aiTranslationService.Model];
                    var requestsInLastMinute = _aiTranslationService.RecentRequests.Count;
                    OnLogMessage($"📊 {LocalizationManager.GetString("LogRateLimitStatus", requestsInLastMinute, limits.requestsPerMinute)}");
                }
            }
            catch (OperationCanceledException)
            {
                OnLogMessage($"⏹️ {LocalizationManager.GetString("LogTranslationCancelled")}");
                RaiseStatusMessage(LocalizationManager.GetString("TranslationCancelled"));
            }
            catch (Exception ex)
            {
                OnLogMessage($"❌ {LocalizationManager.GetString("TranslationError", ex.Message)}");
                RaiseStatusMessage(LocalizationManager.GetString("TranslationError", ex.Message));
                TranslationErrorOccurred?.Invoke(LocalizationManager.GetString("TranslationError", ex.Message));
            }
            finally
            {
                IsTranslationRunning = false;
                IsTranslationPaused = false;
                _translationCts?.Dispose();
                _translationCts = null;
                TranslationFinished?.Invoke();
            }
        }

        private async void ExecuteTranslateSelected()
        {
            var selected = Entries.Where(entry => entry.IsSelected).ToList();
            if (!selected.Any())
            {
                MessageRequested?.Invoke(LocalizationManager.GetString("SelectEntriesFirst"), LocalizationManager.GetString("MsgTip"));
                return;
            }

            var toClear = selected.Where(en => !string.IsNullOrEmpty(en.Translation)).ToList();
            if (toClear.Count > 0)
                PushUndoSnapshot(toClear);

            foreach (var entry in selected)
            {
                entry.Translation = "";
            }

            await TranslateEntriesAsync(selected, forceRefresh: true);
        }

        private async Task ExecuteTranslateAllAsync()
        {
            // 排除黑名单条目：隐藏的条目不参与"翻译全部"统计与执行
            var untranslated = Entries.Where(e => string.IsNullOrEmpty(e.Translation) && !string.IsNullOrEmpty(e.Value) && !e.IsBlacklisted).ToList();
            if (!untranslated.Any())
            {
                MessageRequested?.Invoke(LocalizationManager.GetString("NoUntranslatedEntries"), LocalizationManager.GetString("MsgTip"));
                return;
            }

            var confirmed = await (ConfirmationRequested?.Invoke(
                LocalizationManager.GetString("ConfirmTranslate", untranslated.Count),
                LocalizationManager.GetString("MsgConfirm")) ?? Task.FromResult(true));

            if (confirmed)
                await TranslateEntriesAsync(untranslated);
        }
    }
}
