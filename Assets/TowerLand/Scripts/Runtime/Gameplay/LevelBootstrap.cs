using System.Collections.Generic;
using TowerLand.Core;
using TowerLand.Levels;
using TowerLand.Tower;
using UnityEngine;

namespace TowerLand.Gameplay
{
    /// <summary>
    /// Composition root for the game scene: generates the level to play and builds its tower. That is the level picked in
    /// the <see cref="LevelSession"/> (debug level list) with its own seed - exactly what its thumbnail shows - or else the
    /// player's next level from the <see cref="LevelList"/> (past its end: its second half again, with fresh seeds), or
    /// without a list the scene's default level. A won level is recorded: the player moves on to the next one, and its
    /// cubes become bricks for the build scene.
    /// </summary>
    public sealed class LevelBootstrap : MonoBehaviour
    {
        [SerializeField] private LevelConfig _level;
        [SerializeField] private LevelList _levels;
        [SerializeField] private LevelSession _session;
        [SerializeField] private TowerView _tower;
        [Tooltip("Default level only (no level list): ignore its seed and roll a new one each play (prototyping).")]
        [SerializeField] private bool _randomSeedOnPlay = true;

        private PlayerProgress _progress;
        private int _levelIndex = -1;   // in the player's progression; -1 = debug pick / default level

        public int CurrentSeed { get; private set; }
        public LevelConfig CurrentLevel { get; private set; }

        private void OnEnable() => _tower.Cleared += OnCleared;
        private void OnDisable() => _tower.Cleared -= OnCleared;

        private void Start()
        {
            if (_session != null && _session.FadeIn)
            {
                _session.FadeIn = false;
                UI.ScreenFade.In(0.3f);
            }

            _progress = PlayerProgress.Local();
            LevelConfig picked = _session != null ? _session.Selected : null;
            if (picked != null)
            {
                CurrentLevel = picked;
                _levelIndex = _levels != null ? _levels.NumberOf(picked) - 1 : -1;
                Generate(picked.Seed);
            }
            else if (_levels != null && _levels.Count > 0)
            {
                _levelIndex = _progress.Level;
                CurrentLevel = LevelAt(_levelIndex, out int seed);
                Generate(seed);
            }
            else
            {
                CurrentLevel = _level;
                Generate(_randomSeedOnPlay ? Random.Range(int.MinValue, int.MaxValue) : _level.Seed);
            }
        }

        /// <summary>Level number <paramref name="index"/> (0-based) of the progression: the list, then its second half on repeat with new seeds.</summary>
        private LevelConfig LevelAt(int index, out int seed)
        {
            int count = _levels.Count;
            if (index < count)
            {
                LevelConfig level = _levels.Levels[index];
                seed = level.Seed;
                return level;
            }
            int loop = count - count / 2;
            LevelConfig repeat = _levels.Levels[count / 2 + (index - count) % loop];
            seed = unchecked(repeat.Seed + index * 104729);
            return repeat;
        }

        public void Generate(int seed)
        {
            CurrentSeed = seed;
            _tower.Build((CurrentLevel != null ? CurrentLevel : _level).GenerateLayers(seed));
        }

        /// <summary>Level won: on to the next one, and the tower's cubes (shuffled) become bricks for the construction.</summary>
        private void OnCleared()
        {
            if (!Application.isPlaying || _progress == null) return;
            if (_levelIndex >= 0) _progress.Level = _levelIndex + 1;
            if (_session != null) _session.Select(null);

            // Sorted into colour bands: the construction grows in layers (order from the chaos of the broken tower).
            List<int> stock = _progress.Bricks();
            _progress.Deposit(Meta.BrickSorting.Bands(_tower.BuiltColours, _tower.Palette, stock.Count > 0 ? stock[stock.Count - 1] : -1));
        }

        [ContextMenu("Preview Level Seed")]
        private void PreviewLevelSeed() => Generate(_level.Seed);

        [ContextMenu("Preview Random Seed")]
        private void PreviewRandomSeed()
        {
            Generate(Random.Range(int.MinValue, int.MaxValue));
            Debug.Log($"[TowerLand] Previewing seed {CurrentSeed}", this);
        }

        [ContextMenu("Clear Preview")]
        private void ClearPreview() => _tower.Clear();
    }
}
