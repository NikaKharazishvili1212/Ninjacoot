#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using static Nikson.OptimizationHub;

namespace Nikson
{
    public class SpriteSheetGenerator : ScriptableObject
    {
        public void DrawGUI() => OnGUI();

        void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Drag individual sprites into the list below. They will be packed into a sprite sheet in row-major order (left to right, top to bottom). " +
                "Columns and rows are calculated automatically from the sprite count.\n\n" +
                "Drag sprites into the list, select options and then click \"Generate\". " +
                "If a file already exists, a number will be appended automatically (e.g. SpriteSheet1).",
                GetStyle());

            EditorGUILayout.Space();
            DrawResetButton();
            EditorGUILayout.Space();

            DrawSavePathField();

            SpriteSheetName = EditorGUILayout.TextField("Sheet Name", SpriteSheetName);
            SpritePadding = EditorGUILayout.IntSlider("Padding", SpritePadding, 0, 64);

            EditorGUILayout.LabelField($"Sprites ({Textures.Count})", GetStyle(alignment: TextAnchor.MiddleLeft));
            Rect dropArea = GUILayoutUtility.GetRect(0f, 50f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag & Drop Sprites Here", EditorStyles.helpBox);

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
                            if (draggedObject is Texture2D tex && !Textures.Contains(tex))
                                Textures.Add(tex);
                    }
                    evt.Use();
                }
            }

            ScrollPosition = EditorGUILayout.BeginScrollView(ScrollPosition, GUILayout.Height(110));
            for (int i = Textures.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                Textures[i] = (Texture2D)EditorGUILayout.ObjectField(Textures[i], typeof(Texture2D), false);
                if (GUILayout.Button("↑", GUILayout.Width(25)) && i < Textures.Count - 1) { var tmp = Textures[i]; Textures[i] = Textures[i + 1]; Textures[i + 1] = tmp; }
                if (GUILayout.Button("↓", GUILayout.Width(25)) && i > 0) { var tmp = Textures[i]; Textures[i] = Textures[i - 1]; Textures[i - 1] = tmp; }
                if (GUILayout.Button("X", GUILayout.Width(25))) Textures.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            GUI.enabled = Textures.Count > 0 && Textures.TrueForAll(s => s != null);
            if (CenteredButton("Generate")) Generate();
            GUI.enabled = true;
        }

        void Generate()
        {
            int count = Textures.Count;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt((float)count / cols);

            // Make all Sprites readable
            List<TextureImportData> importSettings = new List<TextureImportData>();
            foreach (var sprite in Textures)
            {
                string path = AssetDatabase.GetAssetPath(sprite);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importSettings.Add(new TextureImportData { importer = importer, wasReadable = importer.isReadable });
                    if (!importer.isReadable) { importer.isReadable = true; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
                }
            }

            int cellW = 0, cellH = 0;
            foreach (var sprite in Textures)
            {
                if (sprite.width > cellW) cellW = sprite.width;
                if (sprite.height > cellH) cellH = sprite.height;
            }

            int sheetW = cols * cellW + (cols - 1) * SpritePadding;
            int sheetH = rows * cellH + (rows - 1) * SpritePadding;

            Texture2D sheet = new Texture2D(sheetW, sheetH, TextureFormat.RGBA32, false);
            Color[] clear = new Color[sheetW * sheetH];
            for (int i = 0; i < clear.Length; i++) clear[i] = Color.clear;
            sheet.SetPixels(clear);

            for (int i = 0; i < count; i++)
            {
                int col = i % cols;
                int row = i / cols;

                int x = col * (cellW + SpritePadding);
                // top to bottom: flip row so row 0 is at the top
                int y = sheetH - (row + 1) * cellH - row * SpritePadding;

                sheet.SetPixels(x, y, Textures[i].width, Textures[i].height, Textures[i].GetPixels());
            }

            sheet.Apply();

            string normalizedPath = SavePath.Replace("\\", "/");
            if (!normalizedPath.EndsWith("/")) normalizedPath += "/";
            if (!Directory.Exists(normalizedPath)) { Directory.CreateDirectory(normalizedPath); AssetDatabase.Refresh(); }

            string outputPath = GetUniquePath(normalizedPath, SpriteSheetName, ".png");
            File.WriteAllBytes(outputPath, sheet.EncodeToPNG());
            AssetDatabase.Refresh();

            TextureImporter sheetImporter = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (sheetImporter != null)
            {
                sheetImporter.textureType = TextureImporterType.Sprite;
                sheetImporter.spriteImportMode = SpriteImportMode.Multiple;
                sheetImporter.isReadable = false;
                sheetImporter.mipmapEnabled = false;
                sheetImporter.filterMode = FilterMode.Point;

                var spriteMetaData = new SpriteMetaData[count];
                for (int i = 0; i < count; i++)
                {
                    int col = i % cols;
                    int row = i / cols;
                    int x = col * (cellW + SpritePadding);
                    int y = sheetH - (row + 1) * cellH - row * SpritePadding;

                    spriteMetaData[i] = new SpriteMetaData
                    {
                        name = Textures[i].name,
                        rect = new Rect(x, y, Textures[i].width, Textures[i].height),
                        pivot = new Vector2(0.5f, 0.5f),
                        alignment = (int)SpriteAlignment.Center
                    };
                }

#pragma warning disable // Disable 'obsolete' warning
                sheetImporter.spritesheet = spriteMetaData;
#pragma warning restore
                sheetImporter.SaveAndReimport();
            }

            // Restore readable state
            foreach (var data in importSettings)
                if (!data.wasReadable)
                {
                    data.importer.isReadable = false;
                    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(data.importer), ImportAssetOptions.ForceUpdate);
                }

            SetStatus(1, $"Created sprite sheet: {outputPath}.\n{cols}x{rows} grid, {count} sprites, cell size: {cellW}x{cellH}, padding: {SpritePadding}px.");
            Textures.Clear();
        }

        class TextureImportData
        {
            public TextureImporter importer;
            public bool wasReadable;
        }
    }
}
#endif