using System;
using System.IO;
using SimpleXmlEditor.Dictionary;
using Xunit;

namespace SimpleXmlEditor.Tests
{
    public class BlacklistManagerTests
    {
        private static string CreateTempPath()
        {
            return Path.Combine(Path.GetTempPath(), $"blacklist_test_{Guid.NewGuid():N}.json");
        }

        [Fact]
        public void AddPrefix_ThenIsBlocked_MatchesPrefix()
        {
            using var temp = new TempFile(CreateTempPath());
            var manager = new BlacklistManager(temp.Path);
            manager.Clear();

            manager.AddPrefix("TEXT_SPEECH_");

            Assert.True(manager.IsBlocked("TEXT_SPEECH_001"));
            Assert.True(manager.IsBlocked("TEXT_SPEECH_HELLO"));
            Assert.False(manager.IsBlocked("TEXT_"));
            Assert.False(manager.IsBlocked("text_speech_001"));
        }

        [Fact]
        public void AddPrefix_Duplicate_ReturnsFalse()
        {
            using var temp = new TempFile(CreateTempPath());
            var manager = new BlacklistManager(temp.Path);
            manager.Clear();

            Assert.True(manager.AddPrefix("UNIT_"));
            Assert.False(manager.AddPrefix("UNIT_"));
            Assert.Equal(1, manager.Count);
        }

        [Fact]
        public void AddPrefix_EmptyOrWhitespace_ReturnsFalse()
        {
            using var temp = new TempFile(CreateTempPath());
            var manager = new BlacklistManager(temp.Path);
            manager.Clear();

            Assert.False(manager.AddPrefix(""));
            Assert.False(manager.AddPrefix("   "));
            Assert.Equal(0, manager.Count);
        }

        [Fact]
        public void RemovePrefix_ThenNotBlocked()
        {
            using var temp = new TempFile(CreateTempPath());
            var manager = new BlacklistManager(temp.Path);
            manager.Clear();
            manager.AddPrefix("SHIP_");

            Assert.True(manager.RemovePrefix("SHIP_"));
            Assert.False(manager.IsBlocked("SHIP_ABC"));
            Assert.Equal(0, manager.Count);
        }

        [Fact]
        public void IsBlocked_NullOrEmptyKey_ReturnsFalse()
        {
            using var temp = new TempFile(CreateTempPath());
            var manager = new BlacklistManager(temp.Path);
            manager.Clear();
            manager.AddPrefix("UNIT_");

            Assert.False(manager.IsBlocked(null));
            Assert.False(manager.IsBlocked(""));
        }

        [Fact]
        public void Persistence_SaveThenReload_RulesRestored()
        {
            using var temp = new TempFile(CreateTempPath());
            var manager1 = new BlacklistManager(temp.Path);
            manager1.Clear();
            manager1.AddPrefix("TEXT_SPEECH_");
            manager1.AddPrefix("BUILDING_");

            // New instance loads from the same file
            var manager2 = new BlacklistManager(temp.Path);
            Assert.Equal(2, manager2.Count);
            Assert.True(manager2.IsBlocked("TEXT_SPEECH_X"));
            Assert.True(manager2.IsBlocked("BUILDING_HQ"));
        }

        [Fact]
        public void Clear_RemovesAllRules()
        {
            using var temp = new TempFile(CreateTempPath());
            var manager = new BlacklistManager(temp.Path);
            manager.AddPrefix("A_");
            manager.AddPrefix("B_");

            manager.Clear();

            Assert.Equal(0, manager.Count);
            Assert.False(manager.IsBlocked("A_1"));
        }

        [Fact]
        public void AddExactOriginalText_ThenIsBlocked_ExactMatchOnly()
        {
            using var temp = new TempFile(CreateTempPath());
            var manager = new BlacklistManager(temp.Path);
            manager.Clear();
            manager.AddExactOriginalText("UNUSED");

            Assert.True(manager.IsBlocked("KEY_1", "UNUSED"));
            // 精确匹配：忽略大小写与首尾空白
            Assert.True(manager.IsBlocked("KEY_1", "unused"));
            Assert.True(manager.IsBlocked("KEY_1", "UNUSED "));
            // 后缀匹配：作者把状态标记写在原文末尾（如 "Borga Besadii Diori:  UNUSED"）
            Assert.True(manager.IsBlocked("KEY_1", "Borga Besadii Diori:  UNUSED"));
            Assert.True(manager.IsBlocked("KEY_1", "Borga Besadii Diori: unused"));
            // 词出现在中间的正常文本不应被误伤（不以规则结尾）
            Assert.False(manager.IsBlocked("KEY_1", "The unused unit remains in play"));
            // 无前缀规则时，key 不以任何前缀命中
            Assert.False(manager.IsBlocked("UNUSED_1", "something"));
        }

        [Fact]
        public void AddExactOriginalText_DuplicateAndEmpty_ReturnsFalse()
        {
            using var temp = new TempFile(CreateTempPath());
            var manager = new BlacklistManager(temp.Path);
            manager.Clear();

            Assert.True(manager.AddExactOriginalText("UNUSED"));
            Assert.False(manager.AddExactOriginalText("UNUSED"));
            Assert.False(manager.AddExactOriginalText(""));
            Assert.False(manager.AddExactOriginalText("   "));
            Assert.Equal(1, manager.Count);
        }

        [Fact]
        public void IsBlocked_MixedRules_AnyDimensionHits()
        {
            using var temp = new TempFile(CreateTempPath());
            var manager = new BlacklistManager(temp.Path);
            manager.Clear();
            manager.AddPrefix("TEXT_SPEECH_");
            manager.AddExactOriginalText("UNUSED");

            // key 前缀命中
            Assert.True(manager.IsBlocked("TEXT_SPEECH_001", "real text"));
            // 原文精确命中
            Assert.True(manager.IsBlocked("ANY_KEY", "UNUSED"));
            // 均不命中
            Assert.False(manager.IsBlocked("ANY_KEY", "real text"));
        }

        [Fact]
        public void RemoveExactOriginalText_ThenNotBlocked()
        {
            using var temp = new TempFile(CreateTempPath());
            var manager = new BlacklistManager(temp.Path);
            manager.Clear();
            manager.AddExactOriginalText("UNUSED");

            Assert.True(manager.RemoveExactOriginalText("UNUSED"));
            Assert.False(manager.IsBlocked("K", "UNUSED"));
            Assert.Equal(0, manager.Count);
        }

        [Fact]
        public void Persistence_ExactOriginals_Restored()
        {
            using var temp = new TempFile(CreateTempPath());
            var manager1 = new BlacklistManager(temp.Path);
            manager1.Clear();
            manager1.AddExactOriginalText("UNUSED");
            manager1.AddExactOriginalText("DISABLED");

            var manager2 = new BlacklistManager(temp.Path);
            Assert.Equal(2, manager2.Count);
            Assert.True(manager2.IsBlocked("K", "UNUSED"));
            Assert.True(manager2.IsBlocked("K", "DISABLED"));
            Assert.True(manager2.IsBlocked("K", "DISABLED "));
        }

        [Fact]
        public void Load_OldArrayFormat_TreatedAsPrefixes()
        {
            using var temp = new TempFile(CreateTempPath());
            File.WriteAllText(temp.Path, "[\"TEXT_SPEECH_\", \"BUILDING_\"]");

            var manager = new BlacklistManager(temp.Path);

            Assert.Equal(2, manager.Count);
            Assert.True(manager.IsBlocked("TEXT_SPEECH_1"));
            // 旧格式条目只作为前缀，不进入原文精确匹配维度
            Assert.False(manager.IsBlocked("ANY_KEY", "TEXT_SPEECH_"));
        }

        private sealed class TempFile : IDisposable
        {
            public string Path { get; }

            public TempFile(string path)
            {
                Path = path;
            }

            public void Dispose()
            {
                if (File.Exists(Path))
                    File.Delete(Path);
            }
        }
    }
}
