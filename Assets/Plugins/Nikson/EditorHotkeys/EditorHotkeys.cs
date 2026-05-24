#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Global editor hotkeys:
///   ` + 1–9    — Load scene by build index (exits play mode first if needed)
///   ` + S      — Save selected object component state in play mode (restored on exit)
///   ` + LMB    — In Game view while playing: add object under cursor to Selection, stacks; Esc to clear. Ignores Terrain and objects with "collider" in name
///   Esc        — Deselect all selected GameObjects
///   F1         — Reset local transform (position, rotation, scale) and RectTransform (anchors, size, pivot) for selected object(s)
///   F2         — [Unity default] Rename selected object in Hierarchy — untouched
///   F3         — Snap selected object(s) to ground (mesh-accurate, not pivot)
///   F4         — Activate/Deactivate selected object(s)
/// 
///   F5         — Align Main Camera with Scene view
///   F6         — Toggle "Mute Audio"
///   F7         — Toggle "Stats" (Game view toolbar)
///   F8         — Toggle "Gizmos"
///
///   F9         — Step one frame forward (auto-pauses if playing)
///   F10        — Toggle Pause
///   F11        — Toggle Play/Stop
///   F12        — Toggle Scene view fullscreen
/// </summary>
[InitializeOnLoad]
public static class EditorHotkeys
{
    static readonly Type gameViewType;
    static readonly PropertyInfo maximizedProp;
    static bool backquoteHeld;

    static EditorHotkeys()
    {
        gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        maximizedProp = typeof(EditorWindow).GetProperty("maximized", BindingFlags.Instance | BindingFlags.Public);

        var globalEventHandler = typeof(EditorApplication).GetField("globalEventHandler", BindingFlags.Static | BindingFlags.NonPublic);
        if (globalEventHandler != null)
        {
            var existing = globalEventHandler.GetValue(null) as EditorApplication.CallbackFunction;
            globalEventHandler.SetValue(null, existing + OnGlobalEvent);
        }
        else Debug.LogWarning("[EditorHotkeys] Could not hook globalEventHandler.");
    }

    static void OnGlobalEvent()
    {
        var e = Event.current;
        if (e == null) return;

        if (e.keyCode == KeyCode.BackQuote)
        {
            if (e.type == EventType.KeyDown) backquoteHeld = true;
            else if (e.type == EventType.KeyUp) backquoteHeld = false;
        }

        // ` + LMB in Game view while playing — add object under cursor to Selection, stacks; Esc to clear
        // Ignores Terrain and any object whose name contains "collider" (case-insensitive)
        if (backquoteHeld && e.type == EventType.MouseDown && e.button == 0 && EditorApplication.isPlaying)
        {
            var gameWins = Resources.FindObjectsOfTypeAll(gameViewType);
            if (gameWins.Length > 0 && EditorWindow.mouseOverWindow != null &&
                EditorWindow.mouseOverWindow.GetType() == gameViewType)
            {
                var gameWin = gameWins[0] as EditorWindow;
                Rect winRect = gameWin.position;
                Vector2 screenMouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
                Vector2 viewportPos = screenMouse - new Vector2(winRect.x, winRect.y);
                float viewX = viewportPos.x / winRect.width;
                float viewY = 1f - (viewportPos.y / winRect.height);
                var cam = Camera.main;
                if (cam != null)
                {
                    Ray ray = cam.ViewportPointToRay(new Vector3(viewX, viewY, 0f));
                    RaycastHit[] hits = Physics.RaycastAll(ray);
                    Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (var hit in hits)
                    {
                        if (hit.collider.GetComponent<Terrain>() != null) continue;
                        if (hit.collider.gameObject.name.IndexOf("collider", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        var clicked = hit.collider.gameObject;
                        var current = new List<GameObject>(Selection.gameObjects);
                        if (!current.Contains(clicked)) current.Add(clicked);
                        Selection.objects = current.ToArray();
                        EditorNotifier.Show("Selected: " + clicked.name);
                        break;
                    }
                }
                e.Use(); return;
            }
        }

        if (e.type != EventType.KeyDown) return;

        // Deselect on Esc
        if (e.keyCode == KeyCode.Escape)
        {
            if (Selection.gameObjects.Length > 0) EditorNotifier.Show($"Deselected ({Selection.gameObjects.Length})");
            Selection.activeGameObject = null;
            e.Use(); return;
        }

        // Reset local transform (position, rotation, scale; anchors and size if RectTransform) for selected object(s)
        if (e.keyCode == KeyCode.F1)
        {
            foreach (var go in Selection.gameObjects)
            {
                Undo.RecordObject(go.transform, "Reset Local Transform");
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                if (go.TryGetComponent<RectTransform>(out var rt))
                {
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = Vector2.zero;
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                }

            }
            if (Selection.gameObjects.Length > 0) EditorNotifier.Show($"Transform reset ({Selection.gameObjects.Length})");
            e.Use(); return;
        }

        // Snap selected object(s) to ground
        if (e.keyCode == KeyCode.F3)
        {
            foreach (var go in Selection.gameObjects)
            {
                var transforms = go.GetComponentsInChildren<Transform>();
                var originalLayers = new int[transforms.Length];
                for (int i = 0; i < transforms.Length; i++)
                {
                    originalLayers[i] = transforms[i].gameObject.layer;
                    transforms[i].gameObject.layer = 2;
                }

                bool didHit = Physics.Raycast(go.transform.position, Vector3.down, out RaycastHit hit, Mathf.Infinity);

                for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = originalLayers[i];

                if (!didHit) continue;

                float lowestWorldY = float.MaxValue;
                bool hasMesh = false;

                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    hasMesh = true;
                    var m = mf.transform.localToWorldMatrix;
                    foreach (var v in mf.sharedMesh.vertices) { float wy = m.MultiplyPoint3x4(v).y; if (wy < lowestWorldY) lowestWorldY = wy; }
                }

                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (smr.sharedMesh == null) continue;
                    hasMesh = true;
                    float wy = smr.bounds.min.y;
                    if (wy < lowestWorldY) lowestWorldY = wy;
                }

                if (!hasMesh) lowestWorldY = go.transform.position.y;

                Undo.RecordObject(go.transform, "Snap To Ground");
                go.transform.position -= new Vector3(0f, lowestWorldY - hit.point.y, 0f);
            }
            if (Selection.gameObjects.Length > 0) EditorNotifier.Show($"Snapped to ground ({Selection.gameObjects.Length})");
            e.Use(); return;
        }

        // Activate/Deactivate selected object(s)
        if (e.keyCode == KeyCode.F4)
        {
            foreach (var go in Selection.gameObjects)
            {
                Undo.RecordObject(go, "Toggle Active");
                go.SetActive(!go.activeSelf);
            }
            if (Selection.gameObjects.Length > 0) EditorNotifier.Show(Selection.gameObjects.Length == 1 ? (Selection.activeGameObject.activeSelf ? "Activated" : "Deactivated") : $"Toggled ({Selection.gameObjects.Length})");
            e.Use(); return;
        }

        // Align Main Camera with Scene view
        if (e.keyCode == KeyCode.F5)
        {
            var sceneCam = SceneView.lastActiveSceneView?.camera;
            var mainCam = Camera.main;
            if (sceneCam && mainCam)
            {
                Undo.RecordObject(mainCam.transform, "Align Camera With Scene View");
                mainCam.transform.SetPositionAndRotation(sceneCam.transform.position, sceneCam.transform.rotation);
                EditorNotifier.Show("Camera aligned with Scene view");
            }
            e.Use(); return;
        }

        // Toggle "Mute Audio"
        if (e.keyCode == KeyCode.F6)
        {
            EditorUtility.audioMasterMute = !EditorUtility.audioMasterMute;
            EditorNotifier.Show(EditorUtility.audioMasterMute ? "Audio Off" : "Audio On");
            e.Use(); return;
        }

        // Toggle "Stats" (Game view toolbar)
        if (e.keyCode == KeyCode.F7)
        {
            var gameWins = Resources.FindObjectsOfTypeAll(gameViewType);
            if (gameWins.Length > 0)
            {
                var statsField = gameViewType.GetField("m_Stats", BindingFlags.Instance | BindingFlags.NonPublic);
                if (statsField != null)
                {
                    bool newValue = !(bool)statsField.GetValue(gameWins[0]);
                    statsField.SetValue(gameWins[0], newValue);
                    EditorNotifier.Show(newValue ? "Stats On" : "Stats Off");
                }
            }
            e.Use(); return;
        }

        // Toggle "Gizmos"
        if (e.keyCode == KeyCode.F8)
        {
            bool newGizmosValue = true;

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.drawGizmos = !sceneView.drawGizmos;
                newGizmosValue = sceneView.drawGizmos;
            }

            var gameWins = Resources.FindObjectsOfTypeAll(gameViewType);
            if (gameWins.Length > 0)
            {
                var gizmosField = gameViewType.GetField("m_Gizmos", BindingFlags.Instance | BindingFlags.NonPublic);
                if (gizmosField != null)
                {
                    bool newValue = !(bool)gizmosField.GetValue(gameWins[0]);
                    gizmosField.SetValue(gameWins[0], newValue);
                    newGizmosValue = newValue; // Game view value takes priority during play
                    (gameWins[0] as EditorWindow).Repaint();
                }
            }

            EditorNotifier.Show(newGizmosValue ? "Gizmos On" : "Gizmos Off");
            e.Use(); return;
        }

        // Step one frame forward
        if (e.keyCode == KeyCode.F9)
        {
            var wins = Resources.FindObjectsOfTypeAll(gameViewType);
            bool wasMaximized = wins.Length > 0 && (bool)maximizedProp.GetValue(wins[0]);
            EditorApplication.Step();
            EditorApplication.delayCall += () => { if (wasMaximized && wins.Length > 0) maximizedProp.SetValue(wins[0], true); };
            EditorNotifier.Show("Frame stepped");
            e.Use(); return;
        }

        // Toggle Pause
        if (e.keyCode == KeyCode.F10)
        {
            var wins = Resources.FindObjectsOfTypeAll(gameViewType);
            bool wasMaximized = wins.Length > 0 && (bool)maximizedProp.GetValue(wins[0]);
            EditorApplication.isPaused = !EditorApplication.isPaused;
            EditorApplication.delayCall += () => { if (wasMaximized && wins.Length > 0) maximizedProp.SetValue(wins[0], true); };
            EditorNotifier.Show(EditorApplication.isPaused ? "Paused" : "Resumed");
            e.Use(); return;
        }

        // Toggle Play/Stop
        if (e.keyCode == KeyCode.F11)
        {
            if (!EditorApplication.isPlaying) { EditorApplication.EnterPlaymode(); EditorNotifier.Show("Play"); }
            else { EditorApplication.ExitPlaymode(); EditorNotifier.Show("Stop"); }
            e.Use(); return;
        }

        // Toggle Scene view fullscreen
        if (e.keyCode == KeyCode.F12)
        {
            if (maximizedProp == null) { e.Use(); return; }
            var gameWin = Resources.FindObjectsOfTypeAll(gameViewType);
            if (gameWin.Length > 0) maximizedProp.SetValue(gameWin[0], false);
            var sceneWin = Resources.FindObjectsOfTypeAll(typeof(SceneView));
            if (sceneWin.Length > 0)
            {
                bool newValue = !(bool)maximizedProp.GetValue(sceneWin[0]);
                maximizedProp.SetValue(sceneWin[0], newValue);
                EditorNotifier.Show(newValue ? "Fullscreen On" : "Fullscreen Off");
            }
            e.Use(); return;
        }

        // ` + S: save selected object(s) component state in play mode (restored on exit)
        // ` + 1–9: load scene by build index (exits play mode first if needed)
        if (backquoteHeld)
        {
            if (e.keyCode == KeyCode.S)
            {
                if (Selection.gameObjects.Length == 0 || !EditorApplication.isPlaying) return;
                PlayModeStateSaver.Save();
                EditorNotifier.Show($"Saved component state ({Selection.gameObjects.Length})");
                e.Use();
                return;
            }

            int sceneIndex = e.keyCode switch { KeyCode.Alpha1 => 0, KeyCode.Alpha2 => 1, KeyCode.Alpha3 => 2, KeyCode.Alpha4 => 3, KeyCode.Alpha5 => 4, KeyCode.Alpha6 => 5, KeyCode.Alpha7 => 6, KeyCode.Alpha8 => 7, KeyCode.Alpha9 => 8, _ => -1 };
            if (sceneIndex < 0) return;

            int sceneCount = EditorBuildSettings.scenes.Length;
            if (sceneCount == 0) { e.Use(); return; }
            if (sceneIndex >= sceneCount) { e.Use(); return; }

            if (EditorApplication.isPlaying)
            {
                int capturedIndex = sceneIndex;
                EditorApplication.isPlaying = false;
                EditorApplication.playModeStateChanged += OnExited;
                void OnExited(PlayModeStateChange state)
                {
                    if (state != PlayModeStateChange.EnteredEditMode) return;
                    EditorApplication.playModeStateChanged -= OnExited;
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    EditorSceneManager.OpenScene(EditorBuildSettings.scenes[capturedIndex].path);
                }
            }
            else
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { e.Use(); return; }
                EditorSceneManager.OpenScene(EditorBuildSettings.scenes[sceneIndex].path);
            }
            e.Use();
        }
    }
}
#endif