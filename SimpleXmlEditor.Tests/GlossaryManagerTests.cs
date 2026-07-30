using System.Linq;
using SimpleXmlEditor.Dictionary;
using Xunit;

namespace SimpleXmlEditor.Tests
{
    public class GlossaryManagerTests
    {
        [Fact]
        public void TryGetValue_ExactMatch_ReturnsTrue()
        {
            var manager = new GlossaryManager();
            manager.SetEntry("Jedi", "绝地");
            
            Assert.True(manager.TryGetValue("Jedi", out var result));
            Assert.Equal("绝地", result);
        }

        [Fact]
        public void TryGetValue_NoMatch_ReturnsFalse()
        {
            var manager = new GlossaryManager();
            Assert.False(manager.TryGetValue("NonExistent", out _));
        }

        [Fact]
        public void SetEntry_UpdatesExistingTerm()
        {
            var manager = new GlossaryManager();
            manager.SetEntry("Test", "测试1");
            manager.SetEntry("Test", "测试2");
            
            Assert.True(manager.TryGetValue("Test", out var result));
            Assert.Equal("测试2", result);
        }

        [Fact]
        public void RemoveEntry_RemovesExistingTerm()
        {
            var manager = new GlossaryManager();
            manager.SetEntry("Test", "测试");
            Assert.True(manager.RemoveEntry("Test"));
            Assert.False(manager.TryGetValue("Test", out _));
        }

        [Fact]
        public void Count_ReflectsActualCount()
        {
            var manager = new GlossaryManager();
            manager.Clear();
            Assert.Equal(0, manager.Count);
            
            manager.SetEntry("A", "甲");
            manager.SetEntry("B", "乙");
            Assert.Equal(2, manager.Count);
            
            manager.Clear();
        }
    }
}
