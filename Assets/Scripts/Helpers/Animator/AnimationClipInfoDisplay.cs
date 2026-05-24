#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(AnimationClip))]
public class AnimationClipInfoDisplay : Editor
{
    private Editor defaultEditor;

    private void OnEnable() => defaultEditor = CreateEditor(target, System.Type.GetType("UnityEditor.AnimationClipEditor, UnityEditor"));
    private void OnDisable() { if (defaultEditor != null) DestroyImmediate(defaultEditor); }

    public override void OnInspectorGUI()
    {
        if (defaultEditor != null) defaultEditor.OnInspectorGUI();

        AnimationClip clip = (AnimationClip)target;

        GUILayout.Space(5);

        var prevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel);
        labelStyle.fontSize = 15;
        labelStyle.alignment = TextAnchor.MiddleCenter;

        string animationType = clip.isHumanMotion ? "Humanoid" : (clip.legacy ? "Legacy" : "Generic");

        // Get file size
        string assetPath = AssetDatabase.GetAssetPath(clip);
        string fileSize = "Unknown";
        if (!string.IsNullOrEmpty(assetPath))
        {
            FileInfo fileInfo = new FileInfo(assetPath);
            if (fileInfo.Exists)
            {
                float sizeKB = fileInfo.Length / 1024f;
                if (sizeKB < 1024) fileSize = $"{sizeKB:F2} KB";
                else fileSize = $"{(sizeKB / 1024f):F2} MB";
            }
        }

        float frameRate = clip.frameRate;
        int totalFrames = Mathf.RoundToInt(clip.length * frameRate);

        EditorGUILayout.LabelField($"Type: {animationType}", labelStyle);
        EditorGUILayout.LabelField($"Root Motion: {(clip.hasRootCurves ? "Yes" : "No")}", labelStyle);
        EditorGUILayout.LabelField($"Length: {clip.length:F3} Seconds", labelStyle);
        EditorGUILayout.LabelField($"({totalFrames} Frames, {frameRate} FPS)", labelStyle);
        EditorGUILayout.LabelField($"File Size: {fileSize}", labelStyle);

        // Event detection section - now inside the main box
        AnimationEvent[] events = clip.events;

        GUILayout.Space(15);

        if (events != null && events.Length > 0)
        {
            GUIStyle eventLabelStyle = new GUIStyle(EditorStyles.boldLabel);
            eventLabelStyle.fontSize = 15;
            eventLabelStyle.alignment = TextAnchor.MiddleCenter;

            EditorGUILayout.LabelField($"🔔 Events: {events.Length}   ", eventLabelStyle);

            GUIStyle eventDetailStyle = new GUIStyle(EditorStyles.boldLabel);
            eventDetailStyle.fontSize = 12;
            eventDetailStyle.alignment = TextAnchor.MiddleLeft;

            EditorGUI.indentLevel++;
            foreach (var e in events)
            {
                string timeStr = $"{e.time:F3}s";
                string frameStr = $"(Frame {Mathf.RoundToInt(e.time * frameRate)})";
                string funcStr = string.IsNullOrEmpty(e.functionName) ? "<No Function>" : e.functionName;

                EditorGUILayout.LabelField($"• {timeStr} {frameStr}: {funcStr}", eventDetailStyle);
            }
            EditorGUI.indentLevel--;
        }
        else
        {
            GUIStyle noEventStyle = new GUIStyle(EditorStyles.label);
            noEventStyle.fontSize = 15;
            noEventStyle.alignment = TextAnchor.MiddleCenter;
            noEventStyle.normal.textColor = Color.white;

            EditorGUILayout.LabelField("No Animation Events", noEventStyle);
        }

        EditorGUILayout.EndVertical();

        GUI.backgroundColor = prevColor;
    }
}
#endif