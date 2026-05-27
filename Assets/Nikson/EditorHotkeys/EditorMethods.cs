#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

public static class EditorMethods
{
    static readonly Type gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
    static readonly PropertyInfo maximizedProp = typeof(EditorWindow).GetProperty("maximized", BindingFlags.Instance | BindingFlags.Public);

    public static void PlaySound(string name)
    {
        if (!EditorHotkeysSettingsProvider.Get().playSounds) return;

        string clipPath = $"{EditorHotkeysSettingsProvider.GetFolder()}/Sounds/{name}.wav";
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        if (clip == null) { Debug.LogWarning($"[EditorHotkeys] Sound not found: {clipPath}"); return; }

        var audioUtilClass = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        var method = audioUtilClass?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
        method?.Invoke(null, new object[] { clip, 0, false });
    }

    public static void Deselect()
    {
        if (Selection.gameObjects.Length > 0) EditorNotifier.Show($"Deselected ({Selection.gameObjects.Length})");
        Selection.activeGameObject = null;
    }

    public static void SelectUnderCursor(Event e)
    {
        var gameWins = Resources.FindObjectsOfTypeAll(gameViewType);
        if (gameWins.Length == 0 || EditorWindow.mouseOverWindow?.GetType() != gameViewType) return;

        var gameWin = gameWins[0] as EditorWindow;
        Rect winRect = gameWin.position;
        Vector2 screenMouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
        Vector2 viewportPos = screenMouse - new Vector2(winRect.x, winRect.y);
        float viewX = viewportPos.x / winRect.width;
        float viewY = 1f - (viewportPos.y / winRect.height);

        var cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ViewportPointToRay(new Vector3(viewX, viewY, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            var go = hit.collider.gameObject;
            if (go.GetComponent<Terrain>() != null) continue;
            if (go.GetComponentInChildren<MeshFilter>() == null && go.GetComponentInChildren<SkinnedMeshRenderer>() == null) continue;

            if (e.shift)
            {
                var current = new List<GameObject>(Selection.gameObjects);
                if (!current.Contains(go)) current.Add(go);
                Selection.objects = current.ToArray();
            }
            else Selection.activeGameObject = go;

            EditorNotifier.Show("Selected: " + go.name);
            PlaySound("Action");
            break;
        }
    }

    public static void ToggleActive()
    {
        foreach (var go in Selection.gameObjects) { Undo.RecordObject(go, "Toggle Active"); go.SetActive(!go.activeSelf); }
        if (Selection.gameObjects.Length == 0) return;
        EditorNotifier.Show(Selection.gameObjects.Length == 1 ? (Selection.activeGameObject.activeSelf ? "Activated" : "Deactivated") : $"Toggled ({Selection.gameObjects.Length})");
        PlaySound(Selection.activeGameObject.activeSelf ? "Activate" : "Deactivate");
    }

    public static void AlignCameraWithSceneView()
    {
        var sceneCam = SceneView.lastActiveSceneView?.camera;
        var mainCam = Camera.main;
        if (sceneCam && mainCam)
        {
            Undo.RecordObject(mainCam.transform, "Align Camera With Scene View");
            mainCam.transform.SetPositionAndRotation(sceneCam.transform.position, sceneCam.transform.rotation);
            EditorNotifier.Show("Camera aligned with Scene view");
            PlaySound("Action");
        }
    }

    public static void ToggleMuteAudio()
    {
        EditorUtility.audioMasterMute = !EditorUtility.audioMasterMute;
        EditorNotifier.Show(EditorUtility.audioMasterMute ? "Audio On" : "Audio Off");
        PlaySound(EditorUtility.audioMasterMute ? "Activate" : "Deactivate");
    }

    public static void ToggleStats()
    {
        var gameWins = Resources.FindObjectsOfTypeAll(gameViewType);
        if (gameWins.Length == 0) return;
        var statsField = gameViewType.GetField("m_Stats", BindingFlags.Instance | BindingFlags.NonPublic);
        if (statsField == null) return;
        bool newValue = !(bool)statsField.GetValue(gameWins[0]);
        statsField.SetValue(gameWins[0], newValue);
        EditorNotifier.Show(newValue ? "Stats On" : "Stats Off");
        PlaySound(newValue ? "Activate" : "Deactivate");
    }

    public static void ToggleGizmos()
    {
        bool newGizmosValue = true;
        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null) { sceneView.drawGizmos = !sceneView.drawGizmos; newGizmosValue = sceneView.drawGizmos; }

        var gameWins = Resources.FindObjectsOfTypeAll(gameViewType);
        if (gameWins.Length > 0)
        {
            var gizmosField = gameViewType.GetField("m_Gizmos", BindingFlags.Instance | BindingFlags.NonPublic);
            if (gizmosField != null)
            {
                bool newValue = !(bool)gizmosField.GetValue(gameWins[0]);
                gizmosField.SetValue(gameWins[0], newValue);
                newGizmosValue = newValue;
                (gameWins[0] as EditorWindow).Repaint();
            }
        }
        EditorNotifier.Show(newGizmosValue ? "Gizmos On" : "Gizmos Off");
        PlaySound(newGizmosValue ? "Activate" : "Deactivate");
    }

    public static void StepFrame()
    {
        EditorApplication.Step();
        EditorNotifier.Show("Frame stepped");
        PlaySound("Action");
    }

    public static void TogglePause()
    {
        EditorApplication.isPaused = !EditorApplication.isPaused;
        EditorNotifier.Show(!EditorApplication.isPaused ? "Resumed" : "Paused");
        PlaySound(!EditorApplication.isPaused ? "Activate" : "Deactivate");
    }

    public static void TogglePlay()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.EnterPlaymode(); EditorNotifier.Show("Play"); }
        else { EditorApplication.ExitPlaymode(); EditorNotifier.Show("Stop"); }
    }

    public static void ToggleSceneFullscreen()
    {
        if (maximizedProp == null) return;
        var gameWin = Resources.FindObjectsOfTypeAll(gameViewType);
        if (gameWin.Length > 0) maximizedProp.SetValue(gameWin[0], false);
        var sceneWin = Resources.FindObjectsOfTypeAll(typeof(SceneView));
        if (sceneWin.Length > 0)
        {
            bool newValue = !(bool)maximizedProp.GetValue(sceneWin[0]);
            maximizedProp.SetValue(sceneWin[0], newValue);
            EditorNotifier.Show(newValue ? "Fullscreen On" : "Fullscreen Off");
        }
    }

    public static void SaveComponentState()
    {
        if (Selection.gameObjects.Length == 0 || !EditorApplication.isPlaying) return;
        PlayModeStateSaver.Save();
        EditorNotifier.Show($"Saved component state ({Selection.gameObjects.Length})");
    }

    public static void ResetTransform()
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
    }

    public static void SnapToGround()
    {
        foreach (var go in Selection.gameObjects)
        {
            var transforms = go.GetComponentsInChildren<Transform>();
            var originalLayers = new int[transforms.Length];
            for (int i = 0; i < transforms.Length; i++) { originalLayers[i] = transforms[i].gameObject.layer; transforms[i].gameObject.layer = 2; }

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

        if (Selection.gameObjects.Length == 0) return;
        EditorNotifier.Show($"Snapped to ground ({Selection.gameObjects.Length})");
        PlaySound("Action");
    }

    public static void MoveWithArrow(KeyCode key, bool vertical = false)
    {
        if (Selection.gameObjects.Length == 0) return;

        Vector3 delta;
        if (vertical)
        {
            float step = 1f;
            delta = key == KeyCode.UpArrow ? Vector3.up * step : Vector3.down * step;
        }
        else
        {
            var gameViewCam = Camera.main;
            bool mouseOverGame = EditorWindow.mouseOverWindow?.GetType() == gameViewType;
            Camera cam = (mouseOverGame && gameViewCam != null) ? gameViewCam : SceneView.lastActiveSceneView?.camera;

            Vector3 right = cam != null ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;
            Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;

            float step = 1f;
            delta = key switch
            {
                KeyCode.UpArrow => fwd * step,
                KeyCode.DownArrow => -fwd * step,
                KeyCode.RightArrow => right * step,
                KeyCode.LeftArrow => -right * step,
                _ => Vector3.zero
            };
        }

        if (delta == Vector3.zero) return;

        foreach (var go in Selection.gameObjects)
        {
            Undo.RecordObject(go.transform, "Move With Arrow");
            go.transform.position += delta;
        }
        EditorNotifier.Show($"Moved {key} ({Selection.gameObjects.Length})");
        PlaySound("Action");
    }


    public static void ClearConsole()
    {
        var logEntries = Type.GetType("UnityEditor.LogEntries, UnityEditor");
        logEntries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        EditorNotifier.Show("Console cleared");
    }

    public static void LoadSceneByIndex(KeyCode keyCode)
    {
        int sceneIndex = keyCode switch { KeyCode.Alpha1 => 0, KeyCode.Alpha2 => 1, KeyCode.Alpha3 => 2, KeyCode.Alpha4 => 3, KeyCode.Alpha5 => 4, KeyCode.Alpha6 => 5, KeyCode.Alpha7 => 6, KeyCode.Alpha8 => 7, KeyCode.Alpha9 => 8, _ => -1 };
        if (sceneIndex < 0) return;

        int sceneCount = EditorBuildSettings.scenes.Length;
        if (sceneCount == 0 || sceneIndex >= sceneCount) return;

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
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(EditorBuildSettings.scenes[sceneIndex].path);
        }
    }
}
#endif