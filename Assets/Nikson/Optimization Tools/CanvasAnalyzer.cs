#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using static Nikson.OptimizationHub;

namespace Nikson
{
    public class CanvasAnalyzer : ScriptableObject
    {
        public void DrawGUI() => OnGUI();

        void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Unity rebuilds the entire Canvas mesh whenever any Graphic component on it changes. " +
                "The more Graphic components a Canvas has, the more expensive that rebuild is.\n\n" +
                "This tool counts all Graphic components (Image, Text, RawImage, etc.) on the selected Canvas " +
                "and tells you whether it is safe for frequent updates, or whether you should split it " +
                "into smaller canvases to avoid performance issues.\n\n" +
                "Select a GameObject with a Canvas component in the Hierarchy, then click Analyze. ",
                GetStyle());

            EditorGUILayout.Space();

            GUI.enabled = Selection.gameObjects.Length == 1 && Selected.GetComponent<Canvas>() != null;
            if (CenteredButton("Analyze")) Analyze();
            GUI.enabled = true;
        }

        void Analyze()
        {
            Graphic[] graphics = Selected.GetComponentsInChildren<Graphic>(true);
            int count = graphics.Length;

            if (count < 50) SetStatus(1, $"Graphics count: {count}.\nSafe for frequent changes. Canvas rebuild cost is low.");
            else if (count < 150) SetStatus(3, $"Graphics count: {count}.\nAcceptable for occasional updates. Avoid per-frame changes.");
            else SetStatus(2, $"Graphics count: {count}.\nNOT suitable for frequent updates.\nAny change rebuilds a large amount of UI.\nRecommended: keep mostly static or split into smaller canvases.");
        }
    }
}
#endif