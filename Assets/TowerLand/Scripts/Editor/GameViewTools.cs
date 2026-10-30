using System;
using System.Reflection;
using UnityEditor;

namespace TowerLand.Editor
{
    /// <summary>Selects portrait phone resolutions in the Game view (Unity has no public API for this).</summary>
    public static class GameViewTools
    {
        [MenuItem("Tools/TowerLand/Game View/1080x1920 (9:16, mockup)")]
        public static void Portrait916() => SetSize(1080, 1920);

        [MenuItem("Tools/TowerLand/Game View/1080x2340 (9:19.5)")]
        public static void Portrait195() => SetSize(1080, 2340);

        public static void SetSize(int width, int height)
        {
            Assembly editor = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
            Type groupClass = group.GetType();

            int count = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null);
            int index = -1;
            for (int i = 0; i < count; i++)
            {
                object size = groupClass.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                Type sizeClass = size.GetType();
                if ((int)sizeClass.GetProperty("width").GetValue(size) == width &&
                    (int)sizeClass.GetProperty("height").GetValue(size) == height)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                Type sizeType = editor.GetType("UnityEditor.GameViewSize");
                Type kind = editor.GetType("UnityEditor.GameViewSizeType");
                object fixedResolution = Enum.Parse(kind, "FixedResolution");
                object newSize = Activator.CreateInstance(sizeType, fixedResolution, width, height, $"TowerLand {width}x{height}");
                groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { newSize });
                index = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null) - 1;
            }

            EditorWindow gameView = EditorWindow.GetWindow(editor.GetType("UnityEditor.GameView"), false, null, false);
            const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo select = gameView.GetType().GetMethod("SizeSelectionCallback", any);
            if (select != null)
                select.Invoke(gameView, new object[] { index, null });
            else
                gameView.GetType().GetProperty("selectedSizeIndex", any)?.SetValue(gameView, index);
            gameView.Repaint();
        }
    }
}
