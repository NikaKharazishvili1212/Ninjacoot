# Editor Hotkeys

## Overview

Editor Hotkeys is a lightweight productivity toolkit for the Unity Editor focused on faster workflow, scene editing, debugging, and play mode iteration.
Originally created for personal game development projects and later prepared for public release.

---

# Installation

1. Import the package into your Unity project.
2. Open Unity.
3. Hotkeys become available automatically.

Menu location:
Tools/Nikson/Editor Hotkeys

---

# Requirements

- Tested on Unity 6
- May also work on earlier Unity versions, but only Unity 6 has been officially tested

---

# Hotkeys Reference

` + LMB               —  Select Object Under Cursor (Play Mode)
` + Shift + LMB       —  Select Multiple Objects Under Cursor (Play Mode)
Esc                   —  Deselect

` + S                 —  Save Play Mode State (Restored On Exit)
` + R                 —  Reset Transform / RectTransform
` + T                 —  Toggle Active

` + G                 —  Snap To Ground
` + Arrow Keys        —  Move Relative To Camera
` + Shift + Up / Down —  Move Up / Down

` + C                 —  Clear Console
` + Shift + 1–9       —  Load Scene By Index

F5                    —  Align Camera With Scene View
F6                    —  Toggle Mute Audio
F7                    —  Toggle Stats
F8                    —  Toggle Gizmos

F9                    —  Step Frame
F10                   —  Toggle Pause
F11                   —  Game View: Toggle Play | Scene View: Focus Game View
F12                   —  Focus Scene View

---

# Notes

Some advanced editor functionality relies on Unity internal editor APIs accessed through reflection.
Future Unity updates may change internal Unity systems which could affect some features until updated.
The package is fully editor-only and does not affect builds.