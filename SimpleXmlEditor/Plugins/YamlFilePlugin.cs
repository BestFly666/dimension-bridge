using System.Collections.Generic;
using System.IO;
using System.Text;
using SimpleXmlEditor.Services;
using YamlDotNet.Serialization;

namespace SimpleXmlEditor.Plugins
{
    /// <summary>
    /// YAML 支持（Unity/各类引擎与配置文件，.yml/.yaml）。
    /// 用 YamlDotNet 解析（可靠、无安全风险），嵌套字典按点分拼接 Key（与 JsonI18nPlugin 一致），
    /// 数组按 [i] 索引展开；保存时重建嵌套结构，译文为空回退原文。
    /// Non-Goals（V1）：数组仅在加载时展开为 [i] 字面 Key，保存时按普通 key 写回（与 JSON 插件行为一致）；
    ///       多文档 YAML（---）只读第一份。
    /// </summary>
    public class YamlFilePlugin : IFileFormatPlugin
    {
        public string FormatName => "YAML";
        public string[] FileExtensions => new[] { ".yaml", ".yml" };

        public List<LocalizationEntry> Load(string filePath)
        {
            var text = File.ReadAllText(filePath, Encoding.UTF8);
            var deserializer = new DeserializerBuilder().Build();
            var root = deserializer.Deserialize<object>(text);

            var entries = new List<LocalizationEntry>();
            if (root is IDictionary<object, object> dict)
                FlattenNode(dict, "", entries);

            for (int i = 0; i < entries.Count; i++)
                entries[i].RowNumber = i + 1;

            return entries;
        }

        public void Save(string filePath, List<LocalizationEntry> entries)
        {
            var root = new Dictionary<object, object>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key)) continue;
                var value = string.IsNullOrEmpty(entry.Translation) ? entry.Value : entry.Translation;
                SetNested(root, entry.Key, value);
            }

            var serializer = new SerializerBuilder().Build();
            var yaml = serializer.Serialize(root);
            File.WriteAllText(filePath, yaml, Encoding.UTF8);
        }

        /// <summary>递归展平：字典按点分拼接，数组按 [i] 展开，标量生成条目。</summary>
        private void FlattenNode(object node, string prefix, List<LocalizationEntry> entries)
        {
            switch (node)
            {
                case IDictionary<object, object> dict:
                    foreach (var kvp in dict)
                        FlattenNode(kvp.Value, prefix + kvp.Key + ".", entries);
                    break;
                case IList<object> list:
                    for (int i = 0; i < list.Count; i++)
                        FlattenNode(list[i], prefix + "[" + i + "].", entries);
                    break;
                case null:
                    break;
                default:
                    var key = prefix.TrimEnd('.');
                    if (key.Length > 0)
                        entries.Add(new LocalizationEntry { Key = key, Value = node.ToString(), Translation = "" });
                    break;
            }
        }

        /// <summary>按点分路径重建嵌套字典（数组 Key "[i]" 按字面处理，与 JSON 插件一致）。</summary>
        private static void SetNested(Dictionary<object, object> root, string key, string value)
        {
            var parts = key.Split('.');
            var current = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i];
                if (current.TryGetValue(part, out var existing) && existing is Dictionary<object, object> child)
                {
                    current = child;
                }
                else
                {
                    var newChild = new Dictionary<object, object>();
                    current[part] = newChild;
                    current = newChild;
                }
            }
            current[parts[^1]] = value;
        }
    }
}
