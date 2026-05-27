#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

namespace Nikson
{
    public class CanvasAnalyzer : EditorWindow
    {
        Canvas canvas;

        [MenuItem("Nikson/Optimization/5. Canvas Analyzer")]
        static void ShowWindow() => GetWindow<CanvasAnalyzer>("Canvas Analyzer");

        void OnGUI()
        {
            GUILayout.Label("Canvas Analyzer", EditorStyles.boldLabel);

            canvas = (Canvas)EditorGUILayout.ObjectField("Canvas", canvas, typeof(Canvas), true);

            EditorGUILayout.Space();

            GUI.enabled = canvas != null;
            if (GUILayout.Button("Analyze Canvas", GUILayout.Height(30))) Analyze();
            GUI.enabled = true;
        }

        void Analyze()
        {
            if (canvas == null) return;

            Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(true);
            int count = graphics.Length;

            string verdict;

            if (count < 50) verdict = "Safe for frequent changes. Canvas rebuild cost is low.";
            else if (count < 150) verdict = "Acceptable for occasional updates. Avoid per-frame changes.";
            else verdict = "NOT suitable for frequent updates. Any change rebuilds a large amount of UI. Recommended: keep mostly static or split into smaller canvases.";
            Debug.Log($"[Canvas Analyzer] Graphics count: {count}. {verdict}");
        }

        string GetHierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
#endif