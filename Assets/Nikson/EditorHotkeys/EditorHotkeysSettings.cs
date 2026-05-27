#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public class EditorHotkeysSettings : ScriptableObject
{
    [HideInInspector] public bool showNotifications = true;
    [HideInInspector] public bool playSounds = true;
}

public static class EditorHotkeysSettingsProvider
{
    private static EditorHotkeysSettings _cached;

    public static EditorHotkeysSettings Get()
    {
        if (_cached != null) return _cached;

        string folder = GetFolder();
        string path = folder + "/EditorHotkeysSettings.asset";

        _cached = AssetDatabase.LoadAssetAtPath<EditorHotkeysSettings>(path);
        if (_cached != null) return _cached;

        _cached = ScriptableObject.CreateInstance<EditorHotkeysSettings>();
        Directory.CreateDirectory(folder);
        AssetDatabase.CreateAsset(_cached, path);
        AssetDatabase.SaveAssets();
        return _cached;
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
    const string MenuRoot = "Nikson/Editor Hotkeys/";
    const string NotifItem = MenuRoot + "Show Notifications";
    const string SoundItem = MenuRoot + "Play Sound Effects";

    [MenuItem(NotifItem, false, 1)]
    static void ToggleNotifications() { var s = EditorHotkeysSettingsProvider.Get(); s.showNotifications = !s.showNotifications; EditorUtility.SetDirty(s); }

    [MenuItem(NotifItem, true)]
    static bool ToggleNotificationsValidate() { Menu.SetChecked(NotifItem, EditorHotkeysSettingsProvider.Get().showNotifications); return true; }

    [MenuItem(SoundItem, false, 2)]
    static void ToggleSounds() { var s = EditorHotkeysSettingsProvider.Get(); s.playSounds = !s.playSounds; EditorUtility.SetDirty(s); }

    [MenuItem(SoundItem, true)]
    static bool ToggleSoundsValidate() { Menu.SetChecked(SoundItem, EditorHotkeysSettingsProvider.Get().playSounds); return true; }

    // separator then hotkey reference list
    [MenuItem(MenuRoot + "HOTKEYS", false, 50)] static void Sep() { }
    [MenuItem(MenuRoot + "HOTKEYS", true)] static bool SepV() => false;

    [MenuItem(MenuRoot + "Esc  —  Deselect", false, 51)] static void H3() { }
    [MenuItem(MenuRoot + "Esc  —  Deselect", true)] static bool H3V() => false;

    [MenuItem(MenuRoot + "` + LMB  —  Select under cursor", false, 52)] static void H1() { }
    [MenuItem(MenuRoot + "` + LMB  —  Select under cursor", true)] static bool H1V() => false;

    [MenuItem(MenuRoot + "` + Shift + LMB  —  Add to selection", false, 53)] static void H2() { }
    [MenuItem(MenuRoot + "` + Shift + LMB  —  Add to selection", true)] static bool H2V() => false;

    [MenuItem(MenuRoot + "` + S  —  Save component state", false, 64)] static void H4() { }
    [MenuItem(MenuRoot + "` + S  —  Save component state", true)] static bool H4V() => false;

    [MenuItem(MenuRoot + "` + R  —  Reset transform ∕ rectransform", false, 65)] static void H5() { }
    [MenuItem(MenuRoot + "` + R  —  Reset transform ∕ rectransform", true)] static bool H5V() => false;

    [MenuItem(MenuRoot + "` + T  —  Toggle active", false, 66)] static void H6() { }
    [MenuItem(MenuRoot + "` + T  —  Toggle active", true)] static bool H6V() => false;

    [MenuItem(MenuRoot + "` + G  —  Snap to ground", false, 67)] static void H7() { }
    [MenuItem(MenuRoot + "` + G  —  Snap to ground", true)] static bool H7V() => false;

    [MenuItem(MenuRoot + "` + Arrow Keys  —  Move relative to camera", false, 68)] static void H8() { }
    [MenuItem(MenuRoot + "` + Arrow Keys  —  Move relative to camera", true)] static bool H8V() => false;

    [MenuItem(MenuRoot + "` + Shift + Up ∕ Down Keys  —  Move up ∕ down", false, 69)] static void H9() { }
    [MenuItem(MenuRoot + "` + Shift + Up ∕ Down Keys  —  Move up ∕ down", true)] static bool H9V() => false;

    [MenuItem(MenuRoot + "` + C  —  Clear console", false, 80)] static void H10() { }
    [MenuItem(MenuRoot + "` + C  —  Clear console", true)] static bool H10V() => false;

    [MenuItem(MenuRoot + "` + Shift + 1–9  —  Load scene by index", false, 81)] static void H11() { }
    [MenuItem(MenuRoot + "` + Shift + 1–9  —  Load scene by index", true)] static bool H11V() => false;

    [MenuItem(MenuRoot + "F5  —  Align camera with Scene view", false, 92)] static void H12() { }
    [MenuItem(MenuRoot + "F5  —  Align camera with Scene view", true)] static bool H12V() => false;

    [MenuItem(MenuRoot + "F6  —  Toggle mute audio", false, 93)] static void H13() { }
    [MenuItem(MenuRoot + "F6  —  Toggle mute audio", true)] static bool H13V() => false;

    [MenuItem(MenuRoot + "F7  —  Toggle stats", false, 94)] static void H14() { }
    [MenuItem(MenuRoot + "F7  —  Toggle stats", true)] static bool H14V() => false;

    [MenuItem(MenuRoot + "F8  —  Toggle gizmos", false, 95)] static void H15() { }
    [MenuItem(MenuRoot + "F8  —  Toggle gizmos", true)] static bool H15V() => false;

    [MenuItem(MenuRoot + "F9  —  Step frame", false, 106)] static void H16() { }
    [MenuItem(MenuRoot + "F9  —  Step frame", true)] static bool H16V() => false;

    [MenuItem(MenuRoot + "F10  —  Toggle pause", false, 107)] static void H17() { }
    [MenuItem(MenuRoot + "F10  —  Toggle pause", true)] static bool H17V() => false;

    [MenuItem(MenuRoot + "F11  —  Toggle play ∕ stop", false, 108)] static void H18() { }
    [MenuItem(MenuRoot + "F11  —  Toggle play ∕ stop", true)] static bool H18V() => false;

    [MenuItem(MenuRoot + "F12  —  Toggle scene fullscreen", false, 109)] static void H19() { }
    [MenuItem(MenuRoot + "F12  —  Toggle scene fullscreen", true)] static bool H19V() => false;
}
#endif