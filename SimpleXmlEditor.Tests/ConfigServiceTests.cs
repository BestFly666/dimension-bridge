using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Moq;
using SimpleXmlEditor.Services;
using Xunit;

namespace SimpleXmlEditor.Tests
{
    public class ConfigServiceTests
    {
        [Fact]
        public void GetCacheKey_NullOrEmpty_ReturnsNull()
        {
            var service = new ConfigService();
            Assert.Null(service.GetCacheKey(null));
            Assert.Null(service.GetCacheKey(""));
            Assert.Null(service.GetCacheKey("   "));
        }

        [Fact]
        public void GetCacheKey_SameText_ReturnsSameKey()
        {
            var service = new ConfigService();
            var key1 = service.GetCacheKey("Hello World");
            var key2 = service.GetCacheKey("Hello World");
            Assert.Equal(key1, key2);
        }

        [Fact]
        public void GetCacheKey_DifferentText_ReturnsDifferentKeys()
        {
            var service = new ConfigService();
            var key1 = service.GetCacheKey("Hello");
            var key2 = service.GetCacheKey("World");
            Assert.NotEqual(key1, key2);
        }

        [Fact]
        public void Cache_IsConcurrentDictionary()
        {
            var service = new ConfigService();
            Assert.IsType<ConcurrentDictionary<string, string>>(service.Cache);
        }

        [Fact]
        public void SetCacheEntry_WritesBothKeys()
        {
            var service = new ConfigService();
            service.SetCacheEntry("KEY1", "original text", "译文");

            // Key 键
            Assert.Equal("译文", service.Cache["KEY1"]);
            // MD5(原文) 键（与 SyncEntriesToCache 对称）
            var md5Key = service.GetCacheKey("original text");
            Assert.NotNull(md5Key);
            Assert.Equal("译文", service.Cache[md5Key]);
        }

        [Fact]
        public void SetCacheEntry_EmptyOriginal_DoesNotWrite()
        {
            var service = new ConfigService();
            service.SetCacheEntry("KEY1", "  ", "译文");
            Assert.False(service.Cache.ContainsKey("KEY1"));
        }

        [Fact]
        public void SyncEntriesToCache_RemovesBothKeys_WhenTranslationEmpty()
        {
            var service = new ConfigService();
            var md5Key = service.GetCacheKey("original text");
            service.SetCacheEntry("KEY1", "original text", "译文");
            Assert.Equal("译文", service.Cache["KEY1"]);

            service.SyncEntriesToCache(new[]
            {
                new SimpleXmlEditor.Services.LocalizationEntry { Key = "KEY1", Value = "original text", Translation = "" }
            });

            Assert.False(service.Cache.ContainsKey("KEY1"));
            Assert.False(service.Cache.ContainsKey(md5Key));
        }

        [Fact]
        public void CumulativeStats_ArePersistedAcrossReload()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "SimpleXmlEditorTests_" + Guid.NewGuid().ToString("N"));

            try
            {
                // 写入：设置累计统计并保存
                var writer = new ConfigService(tempDir);
                writer.Config.TotalApiCalls = 123;
                writer.Config.TotalCacheHits = 456;
                writer.Config.TotalGlossaryHits = 789;
                writer.Config.TotalInputChars = 10000;
                writer.Config.TotalOutputChars = 20000;
                writer.Config.TotalCostUsd = 12.34;
                writer.SaveConfig();

                // 重新加载：新实例从同一目录恢复统计（模拟重启）
                var reader = new ConfigService(tempDir);
                reader.LoadConfig();

                Assert.Equal(123, reader.Config.TotalApiCalls);
                Assert.Equal(456, reader.Config.TotalCacheHits);
                Assert.Equal(789, reader.Config.TotalGlossaryHits);
                Assert.Equal(10000, reader.Config.TotalInputChars);
                Assert.Equal(20000, reader.Config.TotalOutputChars);
                Assert.Equal(12.34, reader.Config.TotalCostUsd);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void CurrencyExchangeRate_DefaultsTo7_2_And_CanBeOverridden()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "SimpleXmlEditorTests_" + Guid.NewGuid().ToString("N"));

            try
            {
                // 默认值 7.2
                var writer = new ConfigService(tempDir);
                Assert.Equal(7.2, writer.Config.CurrencyExchangeRate);

                // 修改后持久化并恢复
                writer.Config.CurrencyExchangeRate = 7.0;
                writer.SaveConfig();

                var reader = new ConfigService(tempDir);
                reader.LoadConfig();
                Assert.Equal(7.0, reader.Config.CurrencyExchangeRate);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}
