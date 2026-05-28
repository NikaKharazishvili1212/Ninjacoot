#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;

namespace Nikson
{
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
            Vector2 screenMouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
            Vector2 localMouse = screenMouse - new Vector2(gameWin.position.x, gameWin.position.y);

            var gameViewField = gameViewType.GetMethod("GetMainGameViewRenderRect", BindingFlags.NonPublic | BindingFlags.Static);
            Rect renderRect = gameViewField != null ? (Rect)gameViewField.Invoke(null, null) : new Rect(0, 21, gameWin.position.width, gameWin.position.height - 21);

            float viewX = (localMouse.x - renderRect.x) / renderRect.width;
            float viewY = 1f - (localMouse.y - renderRect.y) / renderRect.height;

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

                var current = new List<GameObject>(Selection.gameObjects);
                if (!current.Contains(go)) current.Add(go);
                Selection.objects = current.ToArray();

                EditorNotifier.Show("Selected: " + go.name);
                PlaySound("Action");
                break;
            }
        }

        public static void ToggleActive()
        {
            if (Selection.gameObjects.Length == 0) return;
            bool targetState = !Selection.gameObjects[0].activeSelf;
            foreach (var go in Selection.gameObjects) { Undo.RecordObject(go, "Toggle Active"); go.SetActive(targetState); }
            EditorNotifier.Show(Selection.gameObjects.Length == 1 ? (targetState ? "Activated" : "Deactivated") : (targetState ? $"Activated ({Selection.gameObjects.Length})" : $"Deactivated ({Selection.gameObjects.Length})"));
            PlaySound(targetState ? "Activate" : "Deactivate");
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
            bool isAudioOff = !EditorUtility.audioMasterMute;
            EditorNotifier.Show(isAudioOff ? "Audio On" : "Audio Off");
            PlaySound(isAudioOff ? "Activate" : "Deactivate");
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
            if (!EditorApplication.isPaused) return;
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

        public static void TogglePlayOrFocusGameView()
        {
            var focusedType = EditorWindow.focusedWindow?.GetType();
            bool sceneViewFocused = focusedType == typeof(SceneView);

            if (sceneViewFocused)
            {
                if (maximizedProp != null)
                {
                    var sceneWins = Resources.FindObjectsOfTypeAll(typeof(SceneView));
                    if (sceneWins.Length > 0 && (bool)maximizedProp.GetValue(sceneWins[0]))
                        maximizedProp.SetValue(sceneWins[0], false);
                }

                var gameWins = Resources.FindObjectsOfTypeAll(gameViewType);
                if (gameWins.Length == 0) return;
                (gameWins[0] as EditorWindow).Focus();
                EditorNotifier.Show("Game view");
                return;
            }

            if (!EditorApplication.isPlaying) { EditorApplication.EnterPlaymode(); EditorNotifier.Show("Play"); }
            else { EditorApplication.ExitPlaymode(); EditorNotifier.Show("Stop"); }
        }

        public static void ToggleSceneView()
        {
            if (maximizedProp != null)
            {
                var gameWins = Resources.FindObjectsOfTypeAll(gameViewType);
                if (gameWins.Length > 0 && (bool)maximizedProp.GetValue(gameWins[0])) maximizedProp.SetValue(gameWins[0], false);
            }

            var sceneWins = Resources.FindObjectsOfTypeAll(typeof(SceneView));
            if (sceneWins.Length == 0) return;
            (sceneWins[0] as EditorWindow).Focus();
            EditorNotifier.Show("Scene view");
        }

        public static void SavePlayModeState()
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
            int snappedCount = 0;

            foreach (var go in Selection.gameObjects)
            {
                var transforms = go.GetComponentsInChildren<Transform>();
                var originalLayers = new int[transforms.Length];
                for (int i = 0; i < transforms.Length; i++) { originalLayers[i] = transforms[i].gameObject.layer; transforms[i].gameObject.layer = 2; }

                float lowestY = float.MaxValue;
                bool hasMesh = false;

                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null) continue;
                    hasMesh = true;
                    var m = mf.transform.localToWorldMatrix;
                    foreach (var v in mf.sharedMesh.vertices) { float wy = m.MultiplyPoint3x4(v).y; if (wy < lowestY) lowestY = wy; }
                }
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (smr.sharedMesh == null) continue;
                    hasMesh = true;
                    float wy = smr.bounds.min.y;
                    if (wy < lowestY) lowestY = wy;
                }

                if (!hasMesh) lowestY = go.transform.position.y;

                RaycastHit hit = default;
                bool didHit = false;
                for (int i = 0; i < 10; i++)
                {
                    float castY = go.transform.position.y + i * 0.5f;
                    Vector3 castOrigin = new Vector3(go.transform.position.x, castY, go.transform.position.z);
                    if (Physics.Raycast(castOrigin, Vector3.down, out hit, Mathf.Infinity)) { didHit = true; break; }
                }

                for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = originalLayers[i];
                if (!didHit) continue;

                Undo.RecordObject(go.transform, "Snap To Ground");
                go.transform.position -= new Vector3(0f, lowestY - hit.point.y, 0f);
                snappedCount++;
            }

            if (snappedCount == 0) return;
            EditorNotifier.Show($"Snapped to ground ({snappedCount})");
            PlaySound("Action");
        }

        public static void MoveWithArrow(KeyCode key, bool vertical = false)
        {
            if (Selection.gameObjects.Length == 0) return;

            Vector3 delta;
            string dirLabel;
            if (vertical)
            {
                float step = 1f;
                if (key == KeyCode.UpArrow) { delta = Vector3.up * step; dirLabel = "Up"; }
                else { delta = Vector3.down * step; dirLabel = "Down"; }
            }
            else
            {
                var gameViewCam = Camera.main;
                bool mouseOverGame = EditorWindow.mouseOverWindow?.GetType() == gameViewType;
                Camera cam = (mouseOverGame && gameViewCam != null) ? gameViewCam : SceneView.lastActiveSceneView?.camera;

                Vector3 right = cam != null ? Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized : Vector3.right;
                Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;

                float step = 1f;
                (delta, dirLabel) = key switch
                {
                    KeyCode.UpArrow => (fwd * step, "Forward"),
                    KeyCode.DownArrow => (-fwd * step, "Back"),
                    KeyCode.RightArrow => (right * step, "Right"),
                    KeyCode.LeftArrow => (-right * step, "Left"),
                    _ => (Vector3.zero, "")
                };
            }

            if (delta == Vector3.zero) return;

            foreach (var go in Selection.gameObjects)
            {
                Undo.RecordObject(go.transform, "Move With Arrow");
                go.transform.position += delta;
            }
            EditorNotifier.Show($"Moved {dirLabel} ({Selection.gameObjects.Length})");
            PlaySound("Action");
        }


        public static void ClearConsole()
        {
            var logEntries = Type.GetType("UnityEditor.LogEntries, UnityEditor");
            logEntries?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
            EditorNotifier.Show("Console cleared");
            PlaySound("Action");
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
}
#endif