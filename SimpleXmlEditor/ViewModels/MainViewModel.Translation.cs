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
        // 进度保存节流状态：并发批次高（如 100）时，若每批都全量遍历 Entries + 序列化 + 写文件，
        // 会同时发生线程池饥饿与并发写同一文件，导致界面卡死。这里用"单飞+合并"控制：
        // 同一时刻只允许一个保存在执行；保存期间的新请求只置脏标记，等当前保存完成后补一次。
        // 另加最小间隔（2s）：批次完成密集时避免保存任务无限追赶（每次全量序列化 5000+ 条很贵）。
        private Task _progressSaveTask;
        private readonly object _progressSaveGate = new();
        private volatile bool _progressSavePending = false;
        private long _lastProgressSaveTicks = 0;
        private const long MinProgressSaveIntervalTicks = 2 * TimeSpan.TicksPerSecond;

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

        /// <summary>
        /// 节流+合并的进度保存（fire-and-forget 安全）。
        /// 并发批次同时完成时，只允许一个保存在执行；保存期间的新请求置脏标记，
        /// 当前保存完成后若仍有脏标记则补保存一次，保证最终进度不丢失。
        /// 返回当前在途的保存任务，调用方可 await 以等待排空。
        /// </summary>
        private Task SaveProgressThrottledAsync()
        {
            lock (_progressSaveGate)
            {
                _progressSavePending = true;
                if (_progressSaveTask != null)
                    return _progressSaveTask; // 已有保存在执行，稍后会自动补一次

                var task = RunProgressSaveLoop();
                _progressSaveTask = task;

                // 清理 _progressSaveTask 统一交给 continuation，而不是 RunProgressSaveLoop 的 finally：
                // RunProgressSaveLoop 可能同步完成（最小间隔内被跳过直接 break，其 finally 会在赋值前
                // 把 _progressSaveTask 置空），若调用方再把"已完成任务"写回 _progressSaveTask，
                // DrainProgressSavesAsync 会永远读到"非 null 且立即完成"的 task 而死循环（UI 卡死，即本 bug），
                // 后续保存请求也会误判"有保存在执行"而不再创建新循环。
                // 仅在 _progressSaveTask 仍指向本任务时才置空，避免误清后续新建的循环任务。
                _ = task.ContinueWith(completed =>
                {
                    lock (_progressSaveGate)
                    {
                        if (ReferenceEquals(_progressSaveTask, completed))
                            _progressSaveTask = null;
                    }
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

                return task;
            }
        }

        private async Task RunProgressSaveLoop()
        {
            while (_progressSavePending)
            {
                _progressSavePending = false;
                // 最小保存间隔：批次完成密集时避免连续全量序列化+写文件（CPU/IO 风暴）。
                // 跳过的保存由最终 SaveCache / SaveProgressFinalAsync 兜底，进度不丢。
                var now = DateTime.UtcNow.Ticks;
                if (now - Interlocked.Read(ref _lastProgressSaveTicks) < MinProgressSaveIntervalTicks)
                    break;
                Interlocked.Exchange(ref _lastProgressSaveTicks, now);
                await _configService.SaveTranslationProgressAsync(Entries);
            }
            // 注意：不再在此处清理 _progressSaveTask —— 该字段由 SaveProgressThrottledAsync 的
            // continuation 统一清理（ReferenceEquals 防误清），避免"同步完成的任务被写回字段"的死循环 bug。
        }

        /// <summary>
        /// 排空在途的节流保存：等待当前保存任务结束。
        /// 用于 DeleteProgressFile / 最终保存前，避免与后台写并发操作同一进度文件。
        /// </summary>
        private async Task DrainProgressSavesAsync()
        {
            while (true)
            {
                Task current;
                lock (_progressSaveGate)
                {
                    current = _progressSaveTask;
                    if (current != null && current.IsCompleted)
                    {
                        // 防御：不持有已完成任务（正常情况下由 continuation 及时清理），
                        // 避免读到"非 null 且立即完成"的 task 时死循环
                        _progressSaveTask = null;
                        current = null;
                    }
                }
                if (current == null) break;
                await current;
            }
        }

        /// <summary>
        /// 强制保存一次最新进度（全部批次完成后调用，等待落盘）。
        /// 先排空在途的节流保存，再写一次，避免并发写同一文件；若被最小间隔跳过，
        /// 这里的直接写保证最新进度一定落盘。
        /// </summary>
        private async Task SaveProgressFinalAsync()
        {
            await DrainProgressSavesAsync();
            await _configService.SaveTranslationProgressAsync(Entries);
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
            // 防重入：翻译运行中禁止再启动第二条流水线。
            // 否则 _translationCts 会被覆盖，旧流水线的 finally 可能误 Dispose 新 CTS、
            // 且两条流水线并发写同一批条目的 Translation 字段导致数据竞争。
            if (IsTranslationRunning)
            {
                OnLogMessage($"⚠️ {LocalizationManager.GetString("LogTranslationAlreadyRunning")}");
                return;
            }

            // 使用局部 CTS：finally 中仅当字段仍指向本流水线的 CTS 时才清空，
            // 避免（理论上）并发启动时旧流水线误 Dispose 新流水线的取消令牌。
            var cts = new CancellationTokenSource();
            _translationCts = cts;
            IsTranslationRunning = true;
            IsTranslationPaused = false;

            try
            {
                // Session begin — UI shows controls and resets progress display
                TranslationStarted?.Invoke(0);

                // 统计为累计值（持久化），翻译会话开始不再清零
                var successCount = 0;
                var failCount = 0;

                // Filter out entries that need translation
                var entriesToTranslate = entries.Where(e => !string.IsNullOrEmpty(e.Value) && string.IsNullOrEmpty(e.Translation)).ToList();

                // Blacklist: exclude entries whose Key matches a blacklist prefix (no API calls)
                // 使用加载/刷新时预计算的 IsBlacklisted 标志，单次遍历即可，避免 O(条目数 × 黑名单数)
                var blocked = entriesToTranslate.Where(e => e.IsBlacklisted).ToList();
                if (blocked.Count > 0)
                {
                    OnLogMessage($"🚫 {LocalizationManager.GetString("LogBlacklistSkipped", blocked.Count)}");
                    entriesToTranslate = entriesToTranslate.Where(e => !e.IsBlacklisted).ToList();
                }

                if (!entriesToTranslate.Any())
                {
                    OnLogMessage($"ℹ️ {LocalizationManager.GetString("LogNoTranslationNeeded")}");
                    RaiseStatusMessage(LocalizationManager.GetString("NoEntriesForTranslation"));
                    return;
                }

                // Record undo snapshot before mutating translations
                // 注意：快照由调用方（清空译文的入口）在清空前记录，
                // 此处不再重复入栈，避免同一操作产生双快照导致首次撤销无效。

                // Create batches based on token limits
                var batches = _orchestrator.CreateBatches(entriesToTranslate, CustomPrompt, BatchSize);

                StartTranslationTracking(entriesToTranslate.Count);
                TranslationStarted?.Invoke(entriesToTranslate.Count);

                OnLogMessage($"🌍 {LocalizationManager.GetString("LogBatchStart", entriesToTranslate.Count, batches.Count, forceRefresh ? " (force refresh)" : "")}");
                OnLogMessage($"📊 {LocalizationManager.GetString("LogBatchModel", _aiTranslationService.Model)}");

                // 并发批次处理：初始并发度 3，429 时动态降低，配额恢复后回升
                var maxConcurrentBatches = MaxConcurrentBatches;
                var batchSemaphore = new SemaphoreSlim(maxConcurrentBatches, maxConcurrentBatches);
                var runningTasks = new List<Task>();

                for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
                {
                    // Check for cancellation before starting new batch
                    if (cts.Token.IsCancellationRequested)
                    {
                        OnLogMessage($"⏹️ {LocalizationManager.GetString("LogBatchCancelled", batchIndex + 1, batches.Count)}");
                        break;
                    }

                    // Handle pause
                    while (IsTranslationPaused && !cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(500, cts.Token);
                    }

                    if (cts.Token.IsCancellationRequested)
                        break;

                    var batch = batches[batchIndex];
                    var localBatchIndex = batchIndex;

                    // Wait for a slot (limit concurrent batches)
                    await batchSemaphore.WaitAsync(cts.Token);

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
                            // 先释放信号量，让下一批立即启动；再保存进度（节流合并，不阻塞并发调度）
                            batchSemaphore.Release();
                            _ = SaveProgressThrottledAsync();
                        }
                    }, cts.Token);

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
                        // 排空在途的节流保存，避免 DeleteProgressFile 与后台写并发操作同一进度文件
                        await DrainProgressSavesAsync();
                        // Translation complete — delete recovery file
                        _configService.DeleteProgressFile();
                    }
                    else
                    {
                        // 全失败/取消：保留最后一次进度文件，便于崩溃恢复
                        await SaveProgressFinalAsync();
                    }
                }

                var statusMessage = cts.Token.IsCancellationRequested
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
                // 仅当字段仍指向本流水线的 CTS 时才清空，避免误 Dispose 后续流水线的令牌
                if (ReferenceEquals(_translationCts, cts))
                    _translationCts = null;
                cts.Dispose();
                // 持久化累计统计（API 调用/命中/费用），重启后仍保留
                SaveConfig();
                TranslationFinished?.Invoke();
            }
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
            {
                // 快照在清空前记录（翻译全部不预先清空，但撤销时需能恢复原状）
                PushUndoSnapshot(untranslated);
                await TranslateEntriesAsync(untranslated);
            }
        }
    }
}
