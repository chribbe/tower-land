using System;
using UnityEngine;

namespace TowerLand.Levels
{
    /// <summary>
    /// Which level the Game scene should play, handed over between scenes (e.g. picked in the debug level list).
    /// A shared asset referenced by both scenes instead of a static: the choice lives only for the app session
    /// (not serialized, so picking a level in the editor never dirties the asset).
    /// </summary>
    [CreateAssetMenu(menuName = "TowerLand/Level Session", fileName = "LevelSession")]
    public sealed class LevelSession : ScriptableObject
    {
        [NonSerialized] private LevelConfig _selected;

        /// <summary>The level picked for the next game, or null to play the scene's default level.</summary>
        public LevelConfig Selected => _selected;

        /// <summary>The next scene opens out of black (the previous one faded out to switch).</summary>
        public bool FadeIn
        {
            get => _fadeIn;
            set
            {
                _fadeIn = value;
                hideFlags |= HideFlags.DontUnloadUnusedAsset;
            }
        }

        [NonSerialized] private bool _fadeIn;

        public void Select(LevelConfig level)
        {
            _selected = level;
            // Loading the next scene unloads unused assets: keep this one (and the choice) alive across the switch.
            hideFlags |= HideFlags.DontUnloadUnusedAsset;
        }
    }
}
