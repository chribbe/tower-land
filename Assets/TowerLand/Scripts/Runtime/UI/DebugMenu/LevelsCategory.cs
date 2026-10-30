using System.Collections.Generic;
using TMPro;
using TowerLand.Levels;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerLand.UI
{
    /// <summary>
    /// Debug tab: every level of the <see cref="LevelList"/> as a card (baked thumbnail of its tower, number, size);
    /// tapping one plays it (picked in the <see cref="LevelSession"/>, then the Game scene loads).
    /// </summary>
    public sealed class LevelsCategory : DebugCategory
    {
        [SerializeField] private LevelList _levels;
        [SerializeField] private LevelSession _session;
        [Tooltip("Cards per row.")]
        [SerializeField, Range(1, 5)] private int _columns = 3;
        [Tooltip("Thumbnail width / height (the baked thumbnails' aspect).")]
        [SerializeField] private float _thumbnailAspect = 0.84f;

        private const float Spacing = 24f, CardPadding = 12f, CaptionHeight = 112f;

        private GridLayoutGroup _grid;
        private readonly List<RectTransform> _plates = new List<RectTransform>();
        private float _laidOutWidth = -1f;

        public override string Title => "Levels";

        public override void Build(DebugMenu menu, RectTransform content)
        {
            int count = _levels != null ? _levels.Count : 0;
            TextMeshProUGUI hint = menu.Text(menu.Row(content, "Hint", 60f), $"{count} levels - tap one to play it", 36f, DebugMenu.TextDim);
            DebugMenu.Stretch(hint.rectTransform);
            if (count == 0) return;

            RectTransform grid = DebugMenu.NewRect(content, "Grid");
            _grid = grid.gameObject.AddComponent<GridLayoutGroup>();
            _grid.spacing = new Vector2(Spacing, Spacing);
            _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = _columns;
            for (int i = 0; i < count; i++)
                Card(menu, grid, _levels.Levels[i], i + 1);
            Tick();
        }

        /// <summary>Cards fill the row: sized from the grid's real width (only known once the canvas has been scaled).</summary>
        public override void Tick()
        {
            if (_grid == null) return;
            float width = ((RectTransform)_grid.transform).rect.width;
            if (width <= 0f || Mathf.Abs(width - _laidOutWidth) < 0.5f) return;
            _laidOutWidth = width;
            float cell = (width - Spacing * (_columns - 1)) / _columns;
            float thumbHeight = (cell - 2f * CardPadding) / _thumbnailAspect;
            _grid.cellSize = new Vector2(cell, thumbHeight + 2f * CardPadding + CaptionHeight);
            foreach (RectTransform plate in _plates)
                plate.offsetMin = new Vector2(CardPadding, -CardPadding - thumbHeight);
        }

        private void Card(DebugMenu menu, RectTransform grid, LevelConfig level, int number)
        {
            if (level == null) return;
            bool current = _session != null && _session.Selected == level;
            Image card = menu.Box(grid, $"Level {number}", current ? DebugMenu.Accent : DebugMenu.Panel);
            var button = card.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => Play(menu, level));

            // Thumbnail on a slightly lighter plate (shows while a level has no baked thumbnail yet).
            Image plate = menu.Box(card.transform, "Plate", DebugMenu.PanelLight);
            RectTransform plateRect = plate.rectTransform;
            plateRect.anchorMin = new Vector2(0f, 1f);
            plateRect.anchorMax = Vector2.one;
            plateRect.pivot = new Vector2(0.5f, 1f);
            plateRect.offsetMax = new Vector2(-CardPadding, -CardPadding);   // height set by Tick
            _plates.Add(plateRect);
            plate.raycastTarget = false;
            if (level.Thumbnail != null)
            {
                RawImage thumbnail = DebugMenu.NewRect(plate.transform, "Thumbnail").gameObject.AddComponent<RawImage>();
                thumbnail.texture = level.Thumbnail;
                thumbnail.raycastTarget = false;
                DebugMenu.Stretch(thumbnail.rectTransform);
                plate.gameObject.AddComponent<Mask>().showMaskGraphic = true;   // rounded corners
            }
            else
            {
                TextMeshProUGUI missing = menu.Text(plate.transform, "no thumbnail", 28f, DebugMenu.TextDim);
                missing.alignment = TextAlignmentOptions.Center;
                DebugMenu.Stretch(missing.rectTransform);
            }

            Color ink = current ? DebugMenu.Background : Color.white;
            TextMeshProUGUI title = menu.Text(card.transform, $"LEVEL {number}", 48f, ink, menu.TitleFont);
            Caption(title.rectTransform, 62f, 50f);
            TextMeshProUGUI info = menu.Text(card.transform, $"{level.Floors} floors · {level.Layers} layers · {level.ColourCount} col.",
                26f, current ? DebugMenu.Background : DebugMenu.TextDim);
            Caption(info.rectTransform, 20f, 34f);
        }

        /// <summary>Caption line anchored to the card's bottom, <paramref name="bottom"/> units up.</summary>
        private static void Caption(RectTransform rect, float bottom, float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(CardPadding, bottom);
            rect.offsetMax = new Vector2(-CardPadding, bottom + height);
            rect.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }

        private void Play(DebugMenu menu, LevelConfig level)
        {
            if (_session != null) _session.Select(level);
            SceneManager.LoadScene(menu.GameScene);
        }
    }
}
