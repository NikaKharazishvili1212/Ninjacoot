#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace Nikson
{
    public class EditorHotkeysSettings : ScriptableObject
    {
        [HideInInspector] public bool showNotifications = true;
        [HideInInspector] public bool playSounds = true;
    }

    public static class EditorHotkeysSettingsProvider
    {
        private static EditorHotkeysSettings cached;

        public static EditorHotkeysSettings Get()
        {
            if (cached != null) return cached;

            string folder = GetFolder();
            string path = folder + "/EditorHotkeysSettings.asset";

            cached = AssetDatabase.LoadAssetAtPath<EditorHotkeysSettings>(path);
            if (cached != null) return cached;

            cached = ScriptableObject.CreateInstance<EditorHotkeysSettings>();
            Directory.CreateDirectory(folder);
            AssetDatabase.CreateAsset(cached, path);
            AssetDatabase.SaveAssets();
            return cached;
        }

        public static string GetFolder()
        {
            var guids = AssetDatabase.FindAssets("EditorHotkeysSettings t:Script");
            if (guids.Length == 0) return "Assets/Nikson/EditorHotkeys";
            string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return Path.GetDirectoryName(scriptPath).Replace("\\", "/");
        }
    }

    public static class EditorHotkeysMenu
    {
        const string MenuRoot = "Tools/Nikson/Editor Hotkeys/";

        [MenuItem(MenuRoot + "Show Notifications", false, 1)]
        static void ToggleNotifications() { var s = EditorHotkeysSettingsProvider.Get(); s.showNotifications = !s.showNotifications; EditorUtility.SetDirty(s); }

        [MenuItem(MenuRoot + "Show Notifications", true)]
        static bool ToggleNotificationsValidate() { Menu.SetChecked(MenuRoot + "Show Notifications", EditorHotkeysSettingsProvider.Get().showNotifications); return true; }

        [MenuItem(MenuRoot + "Play Sound Effects", false, 2)]
        static void ToggleSounds() { var s = EditorHotkeysSettingsProvider.Get(); s.playSounds = !s.playSounds; EditorUtility.SetDirty(s); }

        [MenuItem(MenuRoot + "Play Sound Effects", true)]
        static bool ToggleSoundsValidate() { Menu.SetChecked(MenuRoot + "Play Sound Effects", EditorHotkeysSettingsProvider.Get().playSounds); return true; }

        [MenuItem(MenuRoot + "Hotkeys Reference", false, 50)]
        static void OpenHotkeysReference() => HotkeysReferenceWindow.Open();
    }

    public class HotkeysReferenceWindow : EditorWindow
    {
        static readonly (string key, string action)[] lines =
        {
            ("Shift + LMB",             "—  Select Objects Under Cursor (Play Mode)"),
            ("Esc",                     "—  Deselect"),
            ("\n",                      ""),
            ("Shift + ` + S",           "—  Save Play Mode State (Restored On Exit)"),
            ("Shift + R",               "—  Reset Transform / RectTransform"),
            ("Shift + T",               "—  Toggle Active"),
            ("\n",                      ""),
            ("Shift + G",               "—  Snap To Ground"),
            ("Shift + Arrow Keys",      "—  Move Relative To Camera"),
            ("Shift + ` + Up / Down",   "—  Move Up / Down"),
            ("\n",                      ""),
            ("Shift + C",               "—  Clear Console"),
            ("Shift + 1–9",             "—  Load Scene By Index"),
            ("\n",                      ""),
            ("F5",                      "—  Align Camera With Scene View"),
            ("F6",                      "—  Toggle Mute Audio"),
            ("F7",                      "—  Toggle Stats"),
            ("F8",                      "—  Toggle Gizmos"),
            ("\n",                      ""),
            ("F9",                      "—  Step Frame"),
            ("F10",                     "—  Toggle Pause"),
            ("F11",                     "—  Game View: Toggle Play | Scene View: Focus Game View"),
            ("F12",                     "—  Focus Scene View"),
        };

        Vector2 scroll;

        public static void Open()
        {
            var win = GetWindow<HotkeysReferenceWindow>(true, "Hotkeys Reference", true);
            win.minSize = new Vector2(502, 465);
            win.maxSize = new Vector2(502, 465);
        }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var (key, action) in lines)
            {
                if (string.IsNullOrEmpty(key)) { EditorGUILayout.Space(4); continue; }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(key, GUILayout.Width(140));
                EditorGUILayout.LabelField(action);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
#endif