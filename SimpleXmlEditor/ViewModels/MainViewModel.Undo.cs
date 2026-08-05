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
            foreach (var entry in Entries)
            {
                if (snapshot.TryGetValue(entry.Key, out var original))
                {
                    entry.Translation = original;
                    restored.Add(entry);
                }
            }
            return restored;
        }
    }
}
