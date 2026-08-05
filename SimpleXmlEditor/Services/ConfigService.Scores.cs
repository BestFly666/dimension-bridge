using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SimpleXmlEditor.Localization;

namespace SimpleXmlEditor.Services
{
    /// <summary>评分缓存条目：分数 + 改进建议（按条目 Key 关联）。</summary>
    public class ScoreCacheItem
    {
        public double Score { get; set; }
        public string Improvement { get; set; } = "";
    }

    public partial class ConfigService
    {
        public ConcurrentDictionary<string, ScoreCacheItem> ScoreCache { get; private set; } = new();

        public void SaveScoreCache()
        {
            try
            {
                File.WriteAllText(_scoreCachePath, JsonConvert.SerializeObject(ScoreCache, Formatting.Indented));
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("LogScoreCacheWriteError", ex.Message));
            }
        }

        /// <summary>清空评分缓存并持久化（空文件），用于"清除缓存"时同步清理评分数据。</summary>
        public void ClearScoreCache()
        {
            ScoreCache.Clear();
            try
            {
                File.WriteAllText(_scoreCachePath, "{}");
            }
            catch (Exception ex)
            {
                RaiseLog(LocalizationManager.GetString("LogScoreCacheWriteError", ex.Message));
            }
        }

        /// <summary>
        /// 把已评估条目的评分与改进建议同步到评分缓存（仅 Key 非空 + 已评估）。
        /// 按条目 Key 关联，重新打开文件后可通过 RestoreScores 恢复。
        /// </summary>
        public void SyncScoresToCache(IEnumerable<LocalizationEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key) || entry.EvaluationScore < 0) continue;
                ScoreCache[entry.Key] = new ScoreCacheItem
                {
                    Score = entry.EvaluationScore,
                    Improvement = entry.EvaluationImprovement ?? ""
                };
            }
        }

        /// <summary>
        /// 按条目 Key 恢复缓存的评分与改进建议（仅恢复未评估的条目）。
        /// 返回恢复的条目数。
        /// </summary>
        public int RestoreScores(IEnumerable<LocalizationEntry> entries)
        {
            if (ScoreCache.Count == 0) return 0;
            int restored = 0;
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key) || entry.EvaluationScore >= 0) continue;
                if (ScoreCache.TryGetValue(entry.Key, out var item))
                {
                    entry.EvaluationScore = item.Score;
                    entry.EvaluationImprovement = item.Improvement;
                    restored++;
                }
            }
            if (restored > 0)
                RaiseLog(LocalizationManager.GetString("LogScoresRestored", restored));
            return restored;
        }
    }
}
