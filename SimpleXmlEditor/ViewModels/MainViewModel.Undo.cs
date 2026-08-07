using System;
using System.Collections.Generic;
using System.Linq;
using SimpleXmlEditor.Services;

namespace SimpleXmlEditor.ViewModels
{
    public partial class MainViewModel
    {
        // ── Undo infrastructure ──
        private readonly object _undoLock = new object();
        private readonly Stack<Dictionary<string, string>> _undoStack = new Stack<Dictionary<string, string>>();

        /// <summary>Record a snapshot of affected entries before a bulk mutation.</summary>
        public void PushUndoSnapshot(IEnumerable<LocalizationEntry> affected)
        {
            var snapshot = new Dictionary<string, string>();
            foreach (var entry in affected)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Key)) continue;
                snapshot[entry.Key] = entry.Translation ?? "";
            }
            if (snapshot.Count == 0) return;

            lock (_undoLock)
            {
                _undoStack.Push(snapshot);
                // Keep the latest 50 snapshots to bound memory usage.
                if (_undoStack.Count > 50)
                {
                    var keep = _undoStack.Take(50).Reverse().ToList();
                    _undoStack.Clear();
                    foreach (var s in keep) _undoStack.Push(s);
                }
            }
        }

        /// <summary>
        /// 撤销编辑提交时调用：若栈顶是该条目的单条快照且值未变（用户没改就提交/按 Esc 取消），
        /// 丢弃该快照，避免无意义快照挤占 50 条上限导致批量操作快照被挤出、Ctrl+Z 失效。
        /// </summary>
        public bool DiscardUndoSnapshotIfUnchanged(string key, string editedValue)
        {
            lock (_undoLock)
            {
                if (_undoStack.Count == 0) return false;
                var top = _undoStack.Peek();
                if (top.Count != 1) return false; // 只处理单条编辑快照
                if (!top.TryGetValue(key, out var oldValue)) return false;
                if (string.Equals(oldValue, editedValue, StringComparison.Ordinal))
                {
                    _undoStack.Pop();
                    return true;
                }
                return false;
            }
        }

        /// <summary>Revert the most recent mutation. Returns the list of restored entries (empty if nothing to undo).</summary>
        public List<LocalizationEntry> UndoLast()
        {
            Dictionary<string, string> snapshot;
            lock (_undoLock)
            {
                if (_undoStack.Count == 0) return new List<LocalizationEntry>();
                snapshot = _undoStack.Pop();
            }

            var restored = new List<LocalizationEntry>();
            // 静默还原 + 调用方统一 view.Refresh()：避免逐条触发 PropertyChanged 导致大批量撤销时 UI 假死
            foreach (var entry in Entries)
            {
                if (snapshot.TryGetValue(entry.Key, out var original))
                {
                    entry.SetTranslationSilent(original);
                    restored.Add(entry);
                }
            }
            return restored;
        }
    }
}
