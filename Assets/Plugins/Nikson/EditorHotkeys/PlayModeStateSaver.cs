#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayModeStateSaver
{
    struct SavedComponent { public string typeName; public string json; public bool useEditorJson; }
    struct SavedObject { public int sceneId; public string path; public List<SavedComponent> components; }

    static List<SavedObject> snapshots;
    static float flashEndTime = -1f;
    static readonly Type gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");

    static readonly System.Reflection.PropertyInfo inspectorModeProp = typeof(SerializedObject).GetProperty("inspectorMode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    static PlayModeStateSaver()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredEditMode || snapshots == null) return;
            EditorApplication.delayCall += () =>
            {
                foreach (var snap in snapshots)
                {
                    var go = FindObject(snap);
                    if (!go) continue;

                    foreach (var cs in snap.components)
                    {
                        var type = Type.GetType(cs.typeName);
                        if (type == null) continue;
                        var target = go.GetComponent(type);
                        if (!target) continue;
                        try
                        {
                            Undo.RecordObject(target, "Restore Play-Mode State");
                            if (cs.useEditorJson) EditorJsonUtility.FromJsonOverwrite(cs.json, target);
                            else JsonUtility.FromJsonOverwrite(cs.json, target);
                            EditorUtility.SetDirty(target);
                        }
                        catch { }
                    }
                }
                snapshots = null;
            };
        };

        EditorApplication.update += () =>
        {
            if (flashEndTime < 0f) return;
            if ((float)EditorApplication.timeSinceStartup > flashEndTime) { flashEndTime = -1f; return; }
            foreach (var w in Resources.FindObjectsOfTypeAll(gameViewType)) (w as EditorWindow)?.Repaint();
            SceneView.RepaintAll();
            if (Event.current != null && Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(0, 0, Screen.width, Screen.height),
                    new Color(1f, 0.1f, 0.1f, Mathf.Clamp01(flashEndTime - (float)EditorApplication.timeSinceStartup) * 0.45f));
        };
    }

    public static void Save()
    {
        if (!EditorApplication.isPlaying || Selection.gameObjects.Length == 0) return;

        snapshots = new List<SavedObject>();
        foreach (var go in Selection.gameObjects)
        {
            int id = GetSceneId(go);

            // Build full hierarchy path as fallback for prefabs where sceneId is -1
            string path = go.name;
            var t = go.transform.parent;
            while (t != null) { path = t.name + "/" + path; t = t.parent; }

            var compList = new List<SavedComponent>();
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                try
                {
                    bool useEditorJson = comp is not MonoBehaviour;
                    compList.Add(new SavedComponent
                    {
                        typeName = comp.GetType().AssemblyQualifiedName,
                        json = useEditorJson ? EditorJsonUtility.ToJson(comp) : JsonUtility.ToJson(comp),
                        useEditorJson = useEditorJson
                    });
                }
                catch { }
            }
            snapshots.Add(new SavedObject { sceneId = id, path = path, components = compList });
        }

        EditorApplication.Beep();
        flashEndTime = (float)EditorApplication.timeSinceStartup + 0.35f;
        foreach (var w in Resources.FindObjectsOfTypeAll(gameViewType)) (w as EditorWindow)?.Repaint();
        SceneView.RepaintAll();
    }

    static GameObject FindObject(SavedObject snap)
    {
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        // Try by sceneId first (reliable for non-prefabs)
        if (snap.sceneId != -1)
        {
            foreach (var root in roots)
            {
                var found = FindBySceneId(root, snap.sceneId);
                if (found) return found;
            }
        }

        // Fall back to hierarchy path (for prefabs)
        string[] parts = snap.path.Split('/');
        foreach (var root in roots)
        {
            if (root.name != parts[0]) continue;
            if (parts.Length == 1) return root;
            var tr = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                tr = tr.Find(parts[i]);
                if (tr == null) break;
                if (i == parts.Length - 1) return tr.gameObject;
            }
        }

        return null;
    }

    static GameObject FindBySceneId(GameObject go, int id)
    {
        if (GetSceneId(go) == id) return go;
        foreach (Transform child in go.transform)
        {
            var found = FindBySceneId(child.gameObject, id);
            if (found != null) return found;
        }
        return null;
    }

    static int GetSceneId(UnityEngine.Object obj)
    {
        var so = new SerializedObject(obj);
        inspectorModeProp.SetValue(so, InspectorMode.Debug, null);
        var prop = so.FindProperty("m_LocalIdentfierInFile"); // Unity's intentional misspelling
        return prop != null && prop.intValue != 0 ? prop.intValue : -1;
    }
}
#endif