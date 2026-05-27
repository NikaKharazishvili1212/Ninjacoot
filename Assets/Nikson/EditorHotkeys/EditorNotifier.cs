#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EditorNotifier
{
    internal static string message;
    internal static float endTime = -1f;

    public static int fontSize = 60;
    public static float backgroundAlpha = 1;
    public static float paddingX = 20;
    public static float paddingY = 0;
    public static float offsetFromBottom = 100;
    public static float defaultDuration = 2;

    static EditorNotifier()
    {
        SceneView.duringSceneGui += sv =>
        {
            if (endTime < 0f) return;
            if ((float)EditorApplication.timeSinceStartup > endTime) { endTime = -1f; return; }
            DrawNotification(sv.position.width, sv.position.height);
            sv.Repaint();
        };

        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            var go = new GameObject("EditorNotifier_Runtime") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<EditorNotifierRuntime>();
        };
    }

    public static void Show(string msg, float duration = 0)
    {
        if (!EditorHotkeysSettingsProvider.Get().showNotifications) return;
        if (duration == 0) duration = defaultDuration;
        message = msg;
        endTime = (float)EditorApplication.timeSinceStartup + duration;
        SceneView.RepaintAll();
    }

    internal static void DrawNotification(float width, float height)
    {
        Handles.BeginGUI();
        float alpha = Mathf.Clamp01(endTime - (float)EditorApplication.timeSinceStartup);
        var style = new GUIStyle(GUI.skin.box) { fontSize = fontSize, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = new Color(1f, 1f, 1f, alpha);
        var content = new GUIContent(message);
        Vector2 size = style.CalcSize(content);
        size.x += paddingX; size.y += paddingY;
        var rect = new Rect((width - size.x) * 0.5f, height - size.y - offsetFromBottom, size.x, size.y);
        GUI.color = new Color(0.1f, 0.1f, 0.1f, alpha * backgroundAlpha);
        GUI.Box(rect, GUIContent.none);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(rect, message, style);
        GUI.color = Color.white;
        Handles.EndGUI();
    }
}

public class EditorNotifierRuntime : MonoBehaviour
{
    void OnGUI()
    {
        if (EditorNotifier.endTime < 0f) return;
        float alpha = Mathf.Clamp01(EditorNotifier.endTime - (float)EditorApplication.timeSinceStartup);
        var style = new GUIStyle(GUI.skin.box) { fontSize = EditorNotifier.fontSize, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = new Color(1f, 1f, 1f, alpha);
        var content = new GUIContent(EditorNotifier.message);
        Vector2 size = style.CalcSize(content);
        size.x += EditorNotifier.paddingX; size.y += EditorNotifier.paddingY;
        var rect = new Rect((Screen.width - size.x) * 0.5f, Screen.height - size.y - EditorNotifier.offsetFromBottom, size.x, size.y);
        GUI.color = new Color(0.1f, 0.1f, 0.1f, alpha * EditorNotifier.backgroundAlpha);
        GUI.Box(rect, GUIContent.none);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(rect, EditorNotifier.message, style);
        GUI.color = Color.white;
    }
}
#endif