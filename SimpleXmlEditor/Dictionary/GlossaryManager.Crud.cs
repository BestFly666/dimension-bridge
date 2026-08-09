using System;

namespace SimpleXmlEditor.Dictionary
{
    /// <summary>
    /// GlossaryManager: term CRUD responsibilities.
    /// </summary>
    public partial class GlossaryManager
    {
        // ─── CRUD ───────────────────────────────────────────────────

        public void SetEntry(string source, string translation, string category = "", string status = "confirmed", string tags = "")
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(translation))
                return;
            var term = new GlossaryTerm(source, translation, category, status, tags);
            if (Terms.ContainsKey(source))
                term.CreatedAt = Terms[source].CreatedAt;
            Terms[source] = term;
            _regexCache.Clear();
            RebuildSortedList();
            Save();
        }

        public void SetTerm(GlossaryTerm term)
        {
            if (string.IsNullOrEmpty(term.English) || string.IsNullOrEmpty(term.Chinese))
                return;
            if (Terms.ContainsKey(term.English))
                term.CreatedAt = Terms[term.English].CreatedAt;
            term.UpdatedAt = DateTime.Now;
            Terms[term.English] = term;
            _regexCache.Clear();
            RebuildSortedList();
            Save();
        }

        public bool RemoveEntry(string source)
        {
            var removed = Terms.TryRemove(source, out _);
            if (removed)
            {
                _regexCache.Clear();
                RebuildSortedList();
                Save();
            }
            return removed;
        }

        public void Clear()
        {
            Terms.Clear();
            _sortedTerms.Clear();
            _regexCache.Clear();
            Save();
        }
    }
}
