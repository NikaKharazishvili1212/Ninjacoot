#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using static Nikson.OptimizationHub;

namespace Nikson
{
    public class MissingScriptsCleaner : EditorWindow
    {
        Vector2 scrollPosition;
        List<GameObject> objectsWithMissing = new List<GameObject>();
        bool searched = false;

        public void DrawGUI() => OnGUI();

        void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Scans all GameObjects in the current scene for missing script components.\n\n" +
                "Click \"Find\" to populate the list (you can review the affected objects), then click \"Delete\" to remove all missing script components.",
                NiksonStyle);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Find", GUILayout.Height(30))) Find();
            GUI.enabled = searched && objectsWithMissing.Count > 0;
            if (GUILayout.Button("Delete", GUILayout.Height(30))) Delete();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (!searched) return;

            if (objectsWithMissing.Count == 0) EditorGUILayout.HelpBox("No missing scripts found in the scene.", MessageType.Info);
            else
            {
                EditorGUILayout.LabelField($"Found {objectsWithMissing.Count} object(s) with missing scripts:", EditorStyles.boldLabel);
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                foreach (var go in objectsWithMissing)
                    EditorGUILayout.ObjectField(go, typeof(GameObject), true);
                EditorGUILayout.EndScrollView();
            }
        }

        void Find()
        {
            objectsWithMissing.Clear();
            searched = true;

            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.scene.name == null) continue; // Skip prefabs/assets not in scene

                Component[] components = go.GetComponents<Component>();
                foreach (var c in components)
                {
                    if (c == null)
                    {
                        objectsWithMissing.Add(go);
                        break;
                    }
                }
            }

            if (objectsWithMissing.Count == 0) Debug.Log("Missing Scripts Cleaner: No missing scripts found.");
            else Debug.Log($"Missing Scripts Cleaner: Found {objectsWithMissing.Count} object(s) with missing scripts.");
        }

        void Delete()
        {
            int totalRemoved = 0;

            foreach (var go in objectsWithMissing)
            {
                if (go == null) continue;
                Undo.RegisterCompleteObjectUndo(go, "Remove Missing Scripts");
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                totalRemoved += removed;
                EditorUtility.SetDirty(go);
            }

            Debug.Log($"Missing Scripts Cleaner: Removed {totalRemoved} missing script(s) from {objectsWithMissing.Count} object(s).");
            objectsWithMissing.Clear();
            searched = false;
        }
    }
}
#endif