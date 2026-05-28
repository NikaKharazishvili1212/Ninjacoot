#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace Nikson
{
    public class TextureSimplifier : EditorWindow
    {
        const string PREF_SAVE_PATH = "Nikson_TextureSimplifier_SavePath";
        const string PREF_OUTPUT_WIDTH = "Nikson_TextureSimplifier_Width";
        const string PREF_OUTPUT_HEIGHT = "Nikson_TextureSimplifier_Height";
        const string PREF_INPUT_MODE = "Nikson_TextureSimplifier_InputMode";
        const string PREF_FOLDER_PATH = "Nikson_TextureSimplifier_FolderPath";

        string savePath;
        Vector2Int outputSize;
        List<Texture2D> textures = new List<Texture2D>();
        enum InputMode { IndividualTextures, Folder }
        InputMode inputMode;
        string folderPath;
        Vector2 scrollPosition;

        [MenuItem("Tools/Nikson/Optimization/4. Texture Simplifier")]
        public static void ShowWindow() => GetWindow<TextureSimplifier>("Texture Simplifier");

        void OnEnable()
        {
            savePath = EditorPrefs.GetString(PREF_SAVE_PATH, "Assets/Nikson/Optimization/Generated/");
            outputSize = new Vector2Int(EditorPrefs.GetInt(PREF_OUTPUT_WIDTH, 512), EditorPrefs.GetInt(PREF_OUTPUT_HEIGHT, 512));
            inputMode = (InputMode)EditorPrefs.GetInt(PREF_INPUT_MODE, (int)InputMode.IndividualTextures);
            folderPath = EditorPrefs.GetString(PREF_FOLDER_PATH, "Assets/");
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "\nResize one or more textures to a target resolution and save them as new assets. " +
                "Drag textures into the list below, set the output size, and click \"Generate\".\n\n" +
                "If a file with the chosen name already exists, a number will be appended automatically (e.g. MyTexture1, MyTexture2).\n",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();

            // Browse save path
            EditorGUILayout.BeginHorizontal();
            savePath = EditorGUILayout.TextField("Save Path", savePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Save Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    if (selected.StartsWith(Application.dataPath))
                        savePath = "Assets" + selected.Substring(Application.dataPath.Length);
                    else
                        Debug.LogWarning("Selected folder must be inside the project's Assets folder.");
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Output Size");
            int newW = EditorGUILayout.IntField(outputSize.x);
            EditorGUILayout.LabelField("x", GUILayout.Width(12));
            int newH = EditorGUILayout.IntField(outputSize.y);
            outputSize = new Vector2Int(Mathf.Max(1, newW), Mathf.Max(1, newH));
            EditorGUILayout.EndHorizontal();
            inputMode = (InputMode)EditorGUILayout.EnumPopup("Input Mode", inputMode);

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PREF_SAVE_PATH, savePath);
                EditorPrefs.SetInt(PREF_OUTPUT_WIDTH, outputSize.x);
                EditorPrefs.SetInt(PREF_OUTPUT_HEIGHT, outputSize.y);
                EditorPrefs.SetInt(PREF_INPUT_MODE, (int)inputMode);
            }

            EditorGUILayout.Space();

            if (inputMode == InputMode.Folder)
            {
                EditorGUILayout.BeginHorizontal();
                folderPath = EditorGUILayout.TextField("Folder", folderPath);
                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Select Texture Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        // Convert absolute path to relative Assets/ path
                        if (selected.StartsWith(Application.dataPath))
                            folderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                        else
                            Debug.LogWarning("Selected folder must be inside the project's Assets folder.");
                    }
                }
                EditorGUILayout.EndHorizontal();

                bool folderValid = AssetDatabase.IsValidFolder(folderPath);
                if (!folderValid)
                    EditorGUILayout.HelpBox("Folder not found inside the project.", MessageType.Warning);
                else
                {
                    // Count textures in folder for feedback
                    var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                    EditorGUILayout.LabelField($"Textures found in folder: {guids.Length}", EditorStyles.boldLabel);
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

            bool canGenerate = inputMode == InputMode.Folder
                ? AssetDatabase.IsValidFolder(folderPath)
                : textures.Count > 0 && textures.Exists(t => t != null);

            GUI.enabled = canGenerate;
            if (GUILayout.Button("Generate", GUILayout.Height(30))) Generate();
            GUI.enabled = true;
        }

        void Generate()
        {
            // Collect textures based on input mode
            List<Texture2D> toProcess = new List<Texture2D>();

            if (inputMode == InputMode.Folder)
            {
                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                foreach (var guid in guids)
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                    if (tex != null) toProcess.Add(tex);
                }
            }
            else
            {
                foreach (var t in textures)
                    if (t != null) toProcess.Add(t);
            }

            if (toProcess.Count == 0)
            {
                Debug.LogError("No valid textures to process!");
                return;
            }

            // Warn if any texture would be upscaled
            foreach (var tex in toProcess)
                if (tex.width < outputSize.x || tex.height < outputSize.y)
                    Debug.LogWarning($"Texture \"{tex.name}\" ({tex.width}x{tex.height}) is smaller than the output size ({outputSize.x}x{outputSize.y}) — it will be upscaled, not simplified.");

            string normalizedPath = savePath.Replace("\\", "/");
            if (!normalizedPath.EndsWith("/")) normalizedPath += "/";

            if (!Directory.Exists(normalizedPath))
            {
                Directory.CreateDirectory(normalizedPath);
                AssetDatabase.Refresh();
            }

            int processedCount = 0;
            foreach (var texture in toProcess)
            {
                Texture2D resized = ResizeTexture(texture, outputSize.x, outputSize.y);
                if (resized == null) continue;

                string outputPath = GetUniquePath(normalizedPath, texture.name, ".png");
                File.WriteAllBytes(outputPath, resized.EncodeToPNG());
                processedCount++;
            }

            AssetDatabase.Refresh();

            string textureOrTextures = processedCount == 1 ? "texture" : "textures";
            Debug.Log($"Created {processedCount} resized {outputSize.x}x{outputSize.y} {textureOrTextures} at: {normalizedPath}");

            if (inputMode == InputMode.IndividualTextures)
                textures = new List<Texture2D>();
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