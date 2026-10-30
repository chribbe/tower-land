using UnityEngine;

namespace TowerLand.UI
{
    /// <summary>
    /// One tab of the <see cref="DebugMenu"/>. Add a subclass component next to (or under) the menu and it gets a tab:
    /// <see cref="Build"/> fills its panel the first time the tab is opened, <see cref="Tick"/> runs every frame while
    /// it is the open tab.
    /// </summary>
    public abstract class DebugCategory : MonoBehaviour
    {
        [Tooltip("Tab order, lowest first.")]
        [SerializeField] private int _order;

        public int Order => _order;
        public abstract string Title { get; }

        /// <summary>Fills <paramref name="content"/> (a vertical, scrolling column) using the menu's widget helpers.</summary>
        public abstract void Build(DebugMenu menu, RectTransform content);

        public virtual void Tick() { }
    }
}
