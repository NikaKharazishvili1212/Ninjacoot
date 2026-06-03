#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using static Nikson.OptimizationHub;

namespace Nikson
{
    public class TextureSimplifier : EditorWindow
    {
        List<Texture2D> textures = new List<Texture2D>();
        Vector2 scrollPosition;

        public void DrawGUI() => OnGUI();

        void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Resize one or more textures to a target resolution and save them as new assets. " +
                "Drag textures into the list below, set the output size, and click \"Generate\".\n\n" +
                "If a file with the chosen name already exists, a number will be appended automatically (e.g. MyTexture1, MyTexture2).",
                NiksonStyle);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            SavePath = EditorGUILayout.TextField("Save Path", SavePath);

            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Save Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    if (selected.StartsWith(Application.dataPath)) SavePath = "Assets" + selected.Substring(Application.dataPath.Length);
                    else Debug.LogWarning("Selected folder must be inside the project's Assets folder.");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Output Size");
            int newW = EditorGUILayout.IntField(OutputSize.x);
            EditorGUILayout.LabelField("x", GUILayout.Width(12));
            int newH = EditorGUILayout.IntField(OutputSize.y);
            OutputSize = new Vector2Int(Mathf.Max(1, newW), Mathf.Max(1, newH));
            EditorGUILayout.EndHorizontal();
            InputModeHandling = (int)(InputMode)EditorGUILayout.EnumPopup("Input Mode", (InputMode)InputModeHandling);

            EditorGUILayout.Space();

            if (InputModeHandling == (int)InputMode.Folder)
            {
                EditorGUILayout.BeginHorizontal();
                FolderPath = EditorGUILayout.TextField("Folder", FolderPath);
                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Select Texture Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        // Convert absolute path to relative Assets/ path
                        if (selected.StartsWith(Application.dataPath)) FolderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                        else Debug.LogWarning("Selected folder must be inside the project's Assets folder.");
                    }
                }
                EditorGUILayout.EndHorizontal();

                bool folderValid = AssetDatabase.IsValidFolder(FolderPath);
                if (!folderValid) EditorGUILayout.HelpBox("Folder not found inside the project.", MessageType.Warning);
                else
                {
                    // Count textures in folder for feedback
                    var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { FolderPath });
                    EditorGUILayout.LabelField($"Textures found in folder: {guids.Length}", NiksonStyle);
                }
            }
            else
            {
                // Drag and drop area
                EditorGUILayout.LabelField($"Textures ({textures.Count})", EditorStyles.boldLabel);
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
                                if (draggedObject is Texture2D texture && !textures.Contains(texture))
                                    textures.Add(texture);
                        }
                        evt.Use();
                    }
                }

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
                for (int i = textures.Count - 1; i >= 0; i--)
                {
                    EditorGUILayout.BeginHorizontal();
                    textures[i] = (Texture2D)EditorGUILayout.ObjectField(textures[i], typeof(Texture2D), false);
                    if (GUILayout.Button("X", GUILayout.Width(25))) textures.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space();

            bool canGenerate = InputModeHandling == (int)InputMode.Folder ? AssetDatabase.IsValidFolder(FolderPath) : textures.Count > 0 && textures.Exists(t => t != null);

            GUI.enabled = canGenerate;
            if (GUILayout.Button("Generate", GUILayout.Height(30))) Generate();
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
                foreach (var t in textures)
                    if (t != null) toProcess.Add(t);

            if (toProcess.Count == 0)
            {
                Debug.LogError("No valid textures to process!");
                return;
            }

            // Warn if any texture would be upscaled
            foreach (var tex in toProcess)
                if (tex.width < OutputSize.x || tex.height < OutputSize.y)
                    Debug.LogWarning($"Texture \"{tex.name}\" ({tex.width}x{tex.height}) is smaller than the output size ({OutputSize.x}x{OutputSize.y}) — it will be upscaled, not simplified.");

            string normalizedPath = SavePath.Replace("\\", "/");
            if (!normalizedPath.EndsWith("/")) normalizedPath += "/";

            if (!Directory.Exists(normalizedPath))
            {
                Directory.CreateDirectory(normalizedPath);
                AssetDatabase.Refresh();
            }

            int processedCount = 0;
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
            Debug.Log($"Created {processedCount} resized {OutputSize.x}x{OutputSize.y} {textureOrTextures} at: {normalizedPath}");

            if (InputModeHandling == (int)InputMode.IndividualTextures) textures = new List<Texture2D>();
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