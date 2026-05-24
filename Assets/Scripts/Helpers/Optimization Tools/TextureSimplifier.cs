#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace Nikson
{
    public class TextureSimplifier : EditorWindow
    {
        List<Texture2D> textures = new List<Texture2D>();
        Vector2Int outputSize = new Vector2Int(512, 512);
        string savePath = "Nikson/Generated/";
        Vector2 scrollPosition;

        [MenuItem("Nikson/Optimization/4. Texture Simplifier")]
        public static void ShowWindow() => GetWindow<TextureSimplifier>("Texture Simplifier");

        void OnGUI()
        {
            GUILayout.Label("Texture Simplifier Settings", EditorStyles.boldLabel);

            outputSize = EditorGUILayout.Vector2IntField("Output Size", outputSize);
            savePath = EditorGUILayout.TextField("Save Path", savePath);

            EditorGUILayout.Space();

            GUI.enabled = textures.Count > 0 && textures.Exists(t => t != null);
            if (GUILayout.Button("Generate Resized Textures", GUILayout.Height(30))) Generate();
            GUI.enabled = true;

            // Drag and drop area
            EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);
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
                            if (draggedObject is Texture2D texture)
                                if (!textures.Contains(texture))
                                    textures.Add(texture);
                    }
                    evt.Use();
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Textures ({textures.Count})", EditorStyles.boldLabel);

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

        void Generate()
        {
            if (textures == null || textures.Count == 0)
            {
                Debug.LogError("No textures assigned!");
                return;
            }

            string normalizedPath = savePath.Replace("\\", "/");
            if (!normalizedPath.StartsWith("Assets/")) normalizedPath = "Assets/" + normalizedPath;
            if (!normalizedPath.EndsWith("/")) normalizedPath += "/";

            if (!Directory.Exists(normalizedPath))
            {
                Directory.CreateDirectory(normalizedPath);
                AssetDatabase.Refresh();
            }

            int processedCount = 0;

            foreach (var texture in textures)
            {
                if (texture == null)
                {
                    Debug.LogWarning("Skipping null texture in array");
                    continue;
                }

                Texture2D resized = ResizeTexture(texture, outputSize.x, outputSize.y);
                if (resized == null) continue;

                string textureName = texture.name;
                string outputPath = GetUniquePath(normalizedPath, textureName, ".png");

                byte[] pngData = resized.EncodeToPNG();
                File.WriteAllBytes(outputPath, pngData);

                processedCount++;
            }

            AssetDatabase.Refresh();

            string textureOrTextures = processedCount > 1 ? "textures" : "texture";
            Debug.Log($"Created {processedCount} resized {outputSize.x}x{outputSize.y} {textureOrTextures} at: {normalizedPath}");

            textures = new(); // Reset UI field to prevent accidental regeneration
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

        string GetUniquePath(string folder, string name, string ext)
        {
            string path = Path.Combine(folder, name + ext);
            int index = 1;
            while (File.Exists(path) || AssetDatabase.LoadAssetAtPath<Object>(path) != null) path = Path.Combine(folder, name + index++ + ext);
            return path;
        }
    }
}
#endif