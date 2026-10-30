using NUnit.Framework;
using TowerLand.Levels;
using UnityEngine;

namespace TowerLand.Tests
{
    public class LevelListTests
    {
        [Test]
        public void NumberOf_IsOneBased_AndZeroWhenMissing()
        {
            var list = ScriptableObject.CreateInstance<LevelList>();
            var a = ScriptableObject.CreateInstance<LevelConfig>();
            var b = ScriptableObject.CreateInstance<LevelConfig>();
            typeof(LevelList).GetField("_levels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(list, new[] { a, b });

            Assert.AreEqual(2, list.Count);
            Assert.AreEqual(1, list.NumberOf(a));
            Assert.AreEqual(2, list.NumberOf(b));
            Assert.AreEqual(0, list.NumberOf(ScriptableObject.CreateInstance<LevelConfig>()));
        }

        [Test]
        public void Session_RemembersThePick_AndSurvivesUnloading()
        {
            var session = ScriptableObject.CreateInstance<LevelSession>();
            Assert.IsNull(session.Selected);
            var level = ScriptableObject.CreateInstance<LevelConfig>();
            session.Select(level);
            Assert.AreSame(level, session.Selected);
            Assert.IsTrue((session.hideFlags & HideFlags.DontUnloadUnusedAsset) != 0, "kept alive across the scene switch");
        }
    }
}
