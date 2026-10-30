using System.Collections.Generic;
using TowerLand.Core;

namespace TowerLand.Tests
{
    /// <summary>In-memory <see cref="IProgressStore"/> for progress tests.</summary>
    internal sealed class MemoryStore : IProgressStore
    {
        public readonly Dictionary<string, int> Values = new Dictionary<string, int>();
        public readonly Dictionary<string, string> Strings = new Dictionary<string, string>();
        public int Saves;
        public int GetInt(string key, int fallback) => Values.TryGetValue(key, out int v) ? v : fallback;
        public void SetInt(string key, int value) => Values[key] = value;
        public string GetString(string key, string fallback) => Strings.TryGetValue(key, out string v) ? v : fallback;
        public void SetString(string key, string value) => Strings[key] = value;
        public void Save() => Saves++;
    }
}
