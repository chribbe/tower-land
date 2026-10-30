using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TowerLand.Core
{
    /// <summary>Where <see cref="PlayerProgress"/> keeps its values (PlayerPrefs in the game, in-memory in tests).</summary>
    public interface IProgressStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        void Save();
    }

    /// <summary>Saved player progress. Plain C#: build it on an <see cref="IProgressStore"/>.</summary>
    public sealed class PlayerProgress
    {
        private const string DebuggerKey = "progress.debugger";
        private const string LevelKey = "progress.level";
        private const string ConstructionKey = "progress.construction";
        private const string BricksKey = "progress.bricks";
        private const string BricksShownKey = "progress.bricksShown";
        // One character per brick: its palette colour in base 36.
        private const string Digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        private readonly IProgressStore _store;

        public PlayerProgress(IProgressStore store) => _store = store;

        /// <summary>The device's game data on PlayerPrefs.</summary>
        public static PlayerProgress Local() => new PlayerProgress(new PlayerPrefsStore());

        /// <summary>Unlocked the debug scene with the password: from then on a short gesture opens it.</summary>
        public bool IsDebugger
        {
            get => _store.GetInt(DebuggerKey, 0) != 0;
            set
            {
                _store.SetInt(DebuggerKey, value ? 1 : 0);
                _store.Save();
            }
        }

        /// <summary>0-based number of the next level to play (levels won so far).</summary>
        public int Level
        {
            get => _store.GetInt(LevelKey, 0);
            set
            {
                _store.SetInt(LevelKey, Mathf.Max(0, value));
                _store.Save();
            }
        }

        // ---------------------------------------------------------------- meta: construction being built
        /// <summary>0-based index of the construction being built (constructions completed so far).</summary>
        public int Construction => _store.GetInt(ConstructionKey, 0);

        /// <summary>Bricks earned for the current construction, in placing order (palette colours). Can exceed its size: the rest carries over.</summary>
        public int BrickCount => _store.GetString(BricksKey, "").Length;

        /// <summary>Bricks of the current construction already shown landing in the build scene; the rest still has to fly in.</summary>
        public int BricksShown
        {
            get => Mathf.Min(_store.GetInt(BricksShownKey, 0), BrickCount);
            set
            {
                _store.SetInt(BricksShownKey, Mathf.Clamp(value, 0, BrickCount));
                _store.Save();
            }
        }

        public List<int> Bricks()
        {
            string bricks = _store.GetString(BricksKey, "");
            var colours = new List<int>(bricks.Length);
            foreach (char c in bricks)
                colours.Add(Mathf.Max(0, Digits.IndexOf(c)));
            return colours;
        }

        /// <summary>A won level's cubes become bricks for the construction, in this order.</summary>
        public void Deposit(IEnumerable<int> colours)
        {
            var bricks = new StringBuilder(_store.GetString(BricksKey, ""));
            foreach (int colour in colours)
                bricks.Append(Digits[Mathf.Clamp(colour, 0, Digits.Length - 1)]);
            _store.SetString(BricksKey, bricks.ToString());
            _store.Save();
        }

        /// <summary>The current construction (<paramref name="size"/> bricks) is finished: start the next one with the leftover bricks.</summary>
        public void CompleteConstruction(int size)
        {
            string bricks = _store.GetString(BricksKey, "");
            _store.SetString(BricksKey, size < bricks.Length ? bricks.Substring(size) : "");
            _store.SetInt(BricksShownKey, 0);
            _store.SetInt(ConstructionKey, Construction + 1);
            _store.Save();
        }
    }

    public sealed class PlayerPrefsStore : IProgressStore
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Save() => PlayerPrefs.Save();
    }
}
