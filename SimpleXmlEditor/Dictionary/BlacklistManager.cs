using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.Dictionary
{
    /// <summary>
    /// 黑名单规则管理（IBlacklistManager 实现）。
    /// 两种匹配方式，任一命中即跳过翻译：
    ///   1. Key 前缀匹配（Ordinal 大小写敏感）
    ///   2. 原文精确匹配（Ordinal 精确相等，避免误过滤需要翻译的条目）
    /// 规则全局生效，持久化到 AppData 的 blacklist.json（新格式对象；兼容旧版 List&lt;string&gt; 数组）。
    /// 线程安全：所有读写经锁保护，适配并发翻译场景。
    /// </summary>
    public class BlacklistManager : IBlacklistManager
    {
        private readonly string _blacklistPath;
        private readonly object _lock = new object();
        private readonly List<string> _prefixes = new();
        private readonly List<string> _exactOriginalTexts = new();

        public event Action<string> LogMessage;

        /// <summary>当前前缀规则列表（只读视图，调用方不应修改）。</summary>
        public IReadOnlyList<string> Prefixes
        {
            get { lock (_lock) return _prefixes.AsReadOnly(); }
        }

        /// <summary>当前原文精确匹配规则列表（只读视图，调用方不应修改）。</summary>
        public IReadOnlyList<string> ExactOriginalTexts
        {
            get { lock (_lock) return _exactOriginalTexts.AsReadOnly(); }
        }

        public int Count
        {
            get { lock (_lock) return _prefixes.Count + _exactOriginalTexts.Count; }
        }

        public BlacklistManager() : this(null) { }

        /// <summary>指定持久化文件路径（测试用）。null 时使用 AppData 默认路径。</summary>
        public BlacklistManager(string blacklistPath)
        {
            _blacklistPath = blacklistPath ?? BuildDefaultPath();
            Load();
        }

        private static string BuildDefaultPath()
        {
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SimpleXmlEditor");
            Directory.CreateDirectory(appDataDir);
            return Path.Combine(appDataDir, "blacklist.json");
        }

        private void RaiseLog(string message)
        {
            LogMessage?.Invoke(message);
        }

        /// <summary>按 Key 前缀判断是否命中任一黑名单前缀。</summary>
        public bool IsBlocked(string key)
        {
            return IsBlocked(key, null);
        }

        /// <summary>按 Key 前缀 + 原文匹配判断是否命中（任一命中即 true）。</summary>
        public bool IsBlocked(string key, string originalText)
        {
            lock (_lock)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    foreach (var prefix in _prefixes)
                    {
                        if (!string.IsNullOrEmpty(prefix) &&
                            key.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(originalText))
                {
                    var text = originalText.Trim();
                    foreach (var exact in _exactOriginalTexts)
                    {
                        if (string.IsNullOrEmpty(exact)) continue;

                        // 精确匹配（忽略大小写与首尾空白）：覆盖 "UNUSED"、"unused"、" UNUSED " 等写法
                        if (string.Equals(text, exact, StringComparison.OrdinalIgnoreCase))
                            return true;

                        // 后缀匹配（忽略大小写）：覆盖 "Borga Besadii Diori:  UNUSED" 这类
                        // 作者把状态标记写在原文末尾的写法；不命中词出现在中间的普通文本，避免误伤
                        if (text.EndsWith(exact, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            return false;
        }

        /// <summary>新增前缀规则（去重，忽略空值）。返回是否实际新增。</summary>
        public bool AddPrefix(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return false;
            prefix = prefix.Trim();

            lock (_lock)
            {
                if (_prefixes.Contains(prefix)) return false;
                _prefixes.Add(prefix);
            }
            Save();
            return true;
        }

        /// <summary>删除前缀规则。返回是否实际删除。</summary>
        public bool RemovePrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return false;

            bool removed;
            lock (_lock)
            {
                removed = _prefixes.Remove(prefix);
            }
            if (removed) Save();
            return removed;
        }

        /// <summary>新增原文精确匹配规则（去重，忽略空值）。返回是否实际新增。</summary>
        public bool AddExactOriginalText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            text = text.Trim();

            lock (_lock)
            {
                if (_exactOriginalTexts.Contains(text)) return false;
                _exactOriginalTexts.Add(text);
            }
            Save();
            return true;
        }

        /// <summary>删除原文精确匹配规则。返回是否实际删除。</summary>
        public bool RemoveExactOriginalText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            bool removed;
            lock (_lock)
            {
                removed = _exactOriginalTexts.Remove(text);
            }
            if (removed) Save();
            return removed;
        }

        /// <summary>清空全部规则并持久化。</summary>
        public void Clear()
        {
            lock (_lock)
            {
                _prefixes.Clear();
                _exactOriginalTexts.Clear();
            }
            Save();
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(_blacklistPath)) return;

                var json = File.ReadAllText(_blacklistPath, Encoding.UTF8);
                lock (_lock)
                {
                    _prefixes.Clear();
                    _exactOriginalTexts.Clear();

                    if (json.TrimStart().StartsWith("["))
                    {
                        // 旧版格式：纯 List<string>，全部按前缀加载
                        var loaded = JsonConvert.DeserializeObject<List<string>>(json);
                        if (loaded == null) return;
                        foreach (var prefix in loaded)
                        {
                            if (!string.IsNullOrWhiteSpace(prefix) && !_prefixes.Contains(prefix))
                                _prefixes.Add(prefix.Trim());
                        }
                    }
                    else
                    {
                        // 新版格式：{ "Prefixes": [...], "ExactOriginals": [...] }
                        var data = JsonConvert.DeserializeObject<BlacklistData>(json);
                        if (data == null) return;
                        if (data.Prefixes != null)
                        {
                            foreach (var prefix in data.Prefixes)
                            {
                                if (!string.IsNullOrWhiteSpace(prefix) && !_prefixes.Contains(prefix))
                                    _prefixes.Add(prefix.Trim());
                            }
                        }
                        if (data.ExactOriginals != null)
                        {
                            foreach (var text in data.ExactOriginals)
                            {
                                if (!string.IsNullOrWhiteSpace(text) && !_exactOriginalTexts.Contains(text))
                                    _exactOriginalTexts.Add(text.Trim());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RaiseLog($"Blacklist load error: {ex.Message}");
            }
        }

        public void Save()
        {
            try
            {
                var snapshot = new BlacklistData();
                lock (_lock)
                {
                    snapshot.Prefixes = new List<string>(_prefixes);
                    snapshot.ExactOriginals = new List<string>(_exactOriginalTexts);
                }
                File.WriteAllText(_blacklistPath, JsonConvert.SerializeObject(snapshot, Formatting.Indented), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                RaiseLog($"Blacklist save error: {ex.Message}");
            }
        }

        private class BlacklistData
        {
            public List<string> Prefixes { get; set; } = new();
            public List<string> ExactOriginals { get; set; } = new();
        }
    }
}
