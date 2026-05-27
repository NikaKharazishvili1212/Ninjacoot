#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class EditorHotkeys
{
    static bool backquoteHeld;

    static EditorHotkeys()
    {
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

        if (backquoteHeld && e.type == EventType.MouseDown && e.button == 0 && EditorApplication.isPlaying) { EditorMethods.SelectUnderCursor(e); e.Use(); return; }

        if (e.type != EventType.KeyDown) return;

        if (e.keyCode == KeyCode.Escape) { EditorMethods.Deselect(); e.Use(); return; }
        if (e.keyCode == KeyCode.F5) { EditorMethods.AlignCameraWithSceneView(); e.Use(); return; }
        if (e.keyCode == KeyCode.F6) { EditorMethods.ToggleMuteAudio(); e.Use(); return; }
        if (e.keyCode == KeyCode.F7) { EditorMethods.ToggleStats(); e.Use(); return; }
        if (e.keyCode == KeyCode.F8) { EditorMethods.ToggleGizmos(); e.Use(); return; }
        if (e.keyCode == KeyCode.F9) { EditorMethods.StepFrame(); e.Use(); return; }
        if (e.keyCode == KeyCode.F10) { EditorMethods.TogglePause(); e.Use(); return; }
        if (e.keyCode == KeyCode.F11) { EditorMethods.TogglePlayOrFocusGameView(); e.Use(); return; }
        if (e.keyCode == KeyCode.F12) { EditorMethods.ToggleSceneView(); e.Use(); return; }

        if (!backquoteHeld) return;
        if (e.keyCode == KeyCode.S) { EditorMethods.SavePlayModeState(); e.Use(); return; }
        if (e.keyCode == KeyCode.R) { EditorMethods.ResetTransform(); e.Use(); return; }
        if (e.keyCode == KeyCode.T) { EditorMethods.ToggleActive(); e.Use(); return; }
        if (e.keyCode == KeyCode.G) { EditorMethods.SnapToGround(); e.Use(); return; }
        if (e.keyCode == KeyCode.UpArrow) { EditorMethods.MoveWithArrow(KeyCode.UpArrow, e.shift); e.Use(); return; }
        if (e.keyCode == KeyCode.DownArrow) { EditorMethods.MoveWithArrow(KeyCode.DownArrow, e.shift); e.Use(); return; }
        if (e.keyCode == KeyCode.RightArrow) { EditorMethods.MoveWithArrow(KeyCode.RightArrow, false); e.Use(); return; }
        if (e.keyCode == KeyCode.LeftArrow) { EditorMethods.MoveWithArrow(KeyCode.LeftArrow, false); e.Use(); return; }
        if (e.keyCode == KeyCode.C) { EditorMethods.ClearConsole(); e.Use(); return; }

        if (e.shift) { EditorMethods.LoadSceneByIndex(e.keyCode); e.Use(); return; }
    }
}
#endif