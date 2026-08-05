using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace SimpleXmlEditor.Dictionary
{
    /// <summary>
    /// GlossaryManager: persistence responsibilities — Load/Save, CSV/JSON import/export,
    /// and expert-profile merge.
    /// </summary>
    public partial class GlossaryManager
    {
        // ─── Load / Save ────────────────────────────────────────────

        public void Load()
        {
            try
            {
                if (File.Exists(GlossaryFile))
                {
                    var json = File.ReadAllText(GlossaryFile, Encoding.UTF8);
                    Terms = new Dictionary<string, GlossaryTerm>(StringComparer.OrdinalIgnoreCase);

                    // Try loading new format (array of GlossaryTerm) first
                    try
                    {
                        var termsList = JsonConvert.DeserializeObject<List<GlossaryTerm>>(json);
                        if (termsList != null && termsList.Count > 0)
                        {
                            foreach (var t in termsList.Where(t => !string.IsNullOrEmpty(t.English)))
                                Terms[t.English] = t;
                        }
                    }
                    catch
                    {
                        // Fall back to old format (Dictionary<string, string>)
                        var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        if (loaded != null)
                        {
                            foreach (var kvp in loaded)
                            {
                                if (kvp.Key == "\uFEFFEn" || kvp.Key == "En" || kvp.Key.Length < 2)
                                    continue;
                                Terms[kvp.Key] = new GlossaryTerm(kvp.Key, kvp.Value);
                            }
                        }
                    }
                }
                else if (File.Exists(DictFile))
                {
                    var json = File.ReadAllText(DictFile, Encoding.UTF8);
                    var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    Terms = new Dictionary<string, GlossaryTerm>(StringComparer.OrdinalIgnoreCase);
                    if (loaded != null)
                    {
                        foreach (var kvp in loaded)
                        {
                            if (kvp.Key == "\uFEFFEn" || kvp.Key == "En" || kvp.Key == "\u04F5" ||
                                string.IsNullOrWhiteSpace(kvp.Key) || kvp.Key.Length < 2)
                                continue;
                            Terms[kvp.Key] = new GlossaryTerm(kvp.Key, kvp.Value);
                        }
                    }
                }
                _regexCache.Clear();
                RebuildSortedList();
            }
            catch (Exception ex)
            {
                Terms = new Dictionary<string, GlossaryTerm>(StringComparer.OrdinalIgnoreCase);
                _sortedTerms = new List<KeyValuePair<string, GlossaryTerm>>();
                System.Diagnostics.Debug.WriteLine($"Glossary load error: {ex.Message}");
            }
        }

        public void Save()
        {
            try
            {
                // Save as array of full GlossaryTerm objects (new format)
                var list = Terms.Values.OrderBy(t => t.English).ToList();
                var json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(GlossaryFile, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Glossary save error: {ex.Message}");
            }
        }

        // ─── Import ─────────────────────────────────────────────────

        public (int added, int updated, int skipped) ImportCsv(string filePath)
        {
            int added = 0, updated = 0, skipped = 0;

            try
            {
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                if (lines.Length < 2) return (0, 0, 0);

                var firstLine = lines[0].Trim().TrimStart('\uFEFF');
                bool hasHeader = CsvHelper.IsHeaderLine(firstLine);
                int startIndex = hasHeader ? 1 : 0;

                for (int i = startIndex; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    var parts = CsvHelper.ParseCsvLine(line);
                    if (parts.Count < 2) { skipped++; continue; }

                    var english = parts[0].Trim();
                    var chinese = parts[1].Trim();
                    var category = parts.Count >= 3 ? parts[2].Trim() : "";

                    if (string.IsNullOrEmpty(english) || string.IsNullOrEmpty(chinese))
                    { skipped++; continue; }
                    if (english == "\uFEFFEn" || english == "En" || english.Length < 2)
                    { skipped++; continue; }
                    if (chinese == "Ch" || chinese == "Cat")
                    { skipped++; continue; }

                    var key = english;
                    if (Terms.ContainsKey(key))
                    {
                        if (Terms[key].Chinese != chinese)
                            updated++;
                    }
                    else
                    {
                        added++;
                    }
                    Terms[key] = new GlossaryTerm(key, chinese, category);
                }

                _regexCache.Clear();
                RebuildSortedList();
                Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Glossary CSV import error: {ex.Message}");
            }

            return (added, updated, skipped);
        }

        public (int added, int updated) ImportJson(string filePath)
        {
            int added = 0, updated = 0;

            try
            {
                var json = File.ReadAllText(filePath, Encoding.UTF8);
                var entries = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                if (entries != null)
                {
                    foreach (var kvp in entries)
                    {
                        var key = kvp.Key.Trim();
                        var value = kvp.Value?.Trim() ?? "";
                        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value) || key.Length < 2)
                            continue;

                        if (Terms.ContainsKey(key))
                        {
                            if (Terms[key].Chinese != value)
                                updated++;
                        }
                        else
                        {
                            added++;
                        }
                        Terms[key] = new GlossaryTerm(key, value, "");
                    }

                    _regexCache.Clear();
                    RebuildSortedList();
                    Save();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Glossary JSON import error: {ex.Message}");
            }

            return (added, updated);
        }

        // ─── Merge from ExpertProfile ────────────────────────────────

        /// <summary>
        /// Merge terms from an expert profile's glossary into the unified glossary.
        /// Uses the profile name as category for imported terms.
        /// </summary>
        public (int added, int updated) MergeFromProfile(string profileName, Dictionary<string, string> profileGlossary)
        {
            int added = 0, updated = 0;
            if (profileGlossary == null) return (added, updated);

            foreach (var kvp in profileGlossary)
            {
                if (string.IsNullOrEmpty(kvp.Key) || string.IsNullOrEmpty(kvp.Value))
                    continue;

                if (Terms.ContainsKey(kvp.Key))
                {
                    if (Terms[kvp.Key].Chinese != kvp.Value)
                    {
                        updated++;
                        Terms[kvp.Key].Chinese = kvp.Value;
                        Terms[kvp.Key].Category = profileName;
                        Terms[kvp.Key].UpdatedAt = DateTime.Now;
                    }
                }
                else
                {
                    added++;
                    Terms[kvp.Key] = new GlossaryTerm(kvp.Key, kvp.Value, profileName);
                }
            }

            if (added + updated > 0)
            {
                _regexCache.Clear();
                RebuildSortedList();
                Save();
            }

            return (added, updated);
        }

        // ─── Export ──────────────────────────────────────────────────

        public void ExportCsv(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("English,Chinese,Category,Status,Tags");
            foreach (var term in Terms.Values.OrderBy(t => t.English))
            {
                var english = CsvHelper.EscapeCsvField(term.English);
                var chinese = CsvHelper.EscapeCsvField(term.Chinese);
                var category = CsvHelper.EscapeCsvField(term.Category);
                var status = CsvHelper.EscapeCsvField(term.Status);
                var tags = CsvHelper.EscapeCsvField(term.Tags);
                sb.AppendLine($"{english},{chinese},{category},{status},{tags}");
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public void ExportJson(string filePath)
        {
            var list = Terms.Values.OrderBy(t => t.English).ToList();
            var json = JsonConvert.SerializeObject(list, Formatting.Indented);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }
    }
}
