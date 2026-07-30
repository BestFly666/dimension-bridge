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
    }
}
