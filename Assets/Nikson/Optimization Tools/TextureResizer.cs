#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using static Nikson.OptimizationHub;

namespace Nikson
{
    public class TextureResizer : ScriptableObject
    {
        public void DrawGUI() => OnGUI();

        void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Resize one or more textures to a target resolution and save them as new assets.\n\n" +
                "Drag textures into the list below, or select a folder to process all textures inside it. " +
                "Set the output size and click \"Generate\". " +
                "If a file already exists, a number will be appended automatically (e.g. MyTexture1).",
                GetStyle());

            EditorGUILayout.Space();
            DrawResetButton();
            EditorGUILayout.Space();

            DrawSavePathField();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Output Size");
            int newW = EditorGUILayout.IntField(OutputSize.x);
            EditorGUILayout.LabelField("x", GUILayout.Width(12));
            int newH = EditorGUILayout.IntField(OutputSize.y);
            OutputSize = new Vector2Int(Mathf.Max(1, newW), Mathf.Max(1, newH));
            EditorGUILayout.EndHorizontal();
            InputModeHandling = (int)(InputMode)EditorGUILayout.EnumPopup("Input Mode", (InputMode)InputModeHandling);

            if (InputModeHandling == (int)InputMode.Folder)
            {
                if (!AssetDatabase.IsValidFolder(FolderPath)) EditorGUILayout.LabelField($"Folder not found inside the project.", GetStyle(alignment: TextAnchor.MiddleLeft, color: Color.red));
                else
                {
                    // Count textures in folder for feedback
                    var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { FolderPath });
                    if (guids.Length > 0) EditorGUILayout.LabelField($"Textures ({guids.Length})", GetStyle(alignment: TextAnchor.MiddleLeft));
                    else EditorGUILayout.LabelField($"No textures found in folder", GetStyle(color: Color.red));
                }

                EditorGUILayout.BeginHorizontal();
                FolderPath = EditorGUILayout.TextField("Folder", FolderPath);
                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Select Texture Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        // Convert absolute path to relative Assets/ path
                        if (selected.StartsWith(Application.dataPath)) FolderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                        else SetStatus(2, "Selected folder must be inside the project's Assets folder.");
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                // Drag and drop area
                EditorGUILayout.LabelField($"Textures ({Textures.Count})", GetStyle(alignment: TextAnchor.MiddleLeft));
                Rect dropArea = GUILayoutUtility.GetRect(0f, 50f, GUILayout.ExpandWidth(true));
                GUI.Box(dropArea, "Drag & Drop Textures Here", EditorStyles.helpBox);

                Event evt = Event.current;
                if (dropArea.Contains(evt.mousePosition))
                {
                    if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        if (evt.type == EventType.DragPerform)
                        {
                            DragAndDrop.AcceptDrag();
                            foreach (Object draggedObject in DragAndDrop.objectReferences)
                                if (draggedObject is Texture2D texture && !Textures.Contains(texture))
                                    Textures.Add(texture);
                        }
                        evt.Use();
                    }
                }

                ScrollPosition = EditorGUILayout.BeginScrollView(ScrollPosition, GUILayout.Height(110));
                for (int i = Textures.Count - 1; i >= 0; i--)
                {
                    EditorGUILayout.BeginHorizontal();
                    Textures[i] = (Texture2D)EditorGUILayout.ObjectField(Textures[i], typeof(Texture2D), false);
                    if (GUILayout.Button("X", GUILayout.Width(25))) Textures.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.Space();

            bool canGenerate = InputModeHandling == (int)InputMode.Folder ? AssetDatabase.IsValidFolder(FolderPath) : Textures.Count > 0 && Textures.Exists(t => t != null);
            GUI.enabled = canGenerate;
            if (CenteredButton("Generate")) Generate();
            GUI.enabled = true;
        }

        void Generate()
        {
            // Collect textures based on input mode
            List<Texture2D> toProcess = new List<Texture2D>();

            if (InputModeHandling == (int)InputMode.Folder)
            {
                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { FolderPath });
                foreach (var guid in guids)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                    if (tex != null) toProcess.Add(tex);
                }
            }
            else
                foreach (var t in Textures)
                    if (t != null) toProcess.Add(t);

            if (toProcess.Count == 0)
            {
                SetStatus(2, "No valid textures to process!");
                return;
            }

            string normalizedPath = SavePath.Replace("\\", "/");
            if (!normalizedPath.EndsWith("/")) normalizedPath += "/";

            if (!Directory.Exists(normalizedPath))
            {
                Directory.CreateDirectory(normalizedPath);
                AssetDatabase.Refresh();
            }

            int processedCount = 0;
            var upscaled = toProcess.Where(t => t.width < OutputSize.x || t.height < OutputSize.y).Select(t => t.name).ToList();
            foreach (var texture in toProcess)
            {
                Texture2D resized = ResizeTexture(texture, OutputSize.x, OutputSize.y);
                if (resized == null) continue;

                string outputPath = GetUniquePath(normalizedPath, texture.name, ".png");
                File.WriteAllBytes(outputPath, resized.EncodeToPNG());
                processedCount++;
            }

            AssetDatabase.Refresh();

            string textureOrTextures = processedCount == 1 ? "texture" : "textures";
            if (upscaled.Count > 0) SetStatus(3, $"Created {processedCount} {textureOrTextures} at: {normalizedPath}.\nBut {upscaled.Count} {(upscaled.Count > 1 ? "were" : "was")} upscaled: {string.Join(", ", upscaled)}.");
            else SetStatus(1, $"Created {processedCount} resized {textureOrTextures} at: {normalizedPath}.");
            if (InputModeHandling == (int)InputMode.IndividualTextures) Textures = new List<Texture2D>();
        }

        Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture.active = rt;

            Graphics.Blit(source, rt);

            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }
    }
}
#endif