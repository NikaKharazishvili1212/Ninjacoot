#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using static Nikson.OptimizationHub;

namespace Nikson
{
    public class TextureTools : ScriptableObject
    {
        List<Texture2D> textures = new List<Texture2D>();

        public void ClearTextures() => textures.Clear();

        public void OnGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            SelectedTextureToolsMode = GUILayout.SelectionGrid(SelectedTextureToolsMode, TEXTURE_TOOLS_MODE_LABELS, 2, GUILayout.Width(250), GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();

            DrawIcon();
            if (SelectedTextureToolsMode == 0) DrawTextureResizer();
            else DrawSpriteSheet();
        }

        void DrawTextureResizer()
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
            CurrentInputMode = (int)(InputMode)EditorGUILayout.EnumPopup("Input Mode", (InputMode)CurrentInputMode);

            EditorGUILayout.Space();
            bool canGenerate = CurrentInputMode == (int)InputMode.Folder
                ? AssetDatabase.IsValidFolder(TexturesFolderPath)
                : textures.Count > 0 && textures.Exists(t => t != null);
            GUI.enabled = canGenerate;
            if (CenteredButton("Generate")) GenerateResized();
            GUI.enabled = true;
            EditorGUILayout.Space();

            if (CurrentInputMode == (int)InputMode.Folder)
            {
                if (!AssetDatabase.IsValidFolder(TexturesFolderPath))
                    EditorGUILayout.LabelField("Folder not found inside the project.", GetStyle(alignment: TextAnchor.MiddleLeft, color: Color.red));
                else
                {
                    var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesFolderPath });
                    if (guids.Length > 0) EditorGUILayout.LabelField($"Textures ({guids.Length})", GetStyle(alignment: TextAnchor.MiddleLeft));
                    else EditorGUILayout.LabelField("No textures found in folder", GetStyle(color: Color.red));
                }

                EditorGUILayout.BeginHorizontal();
                TexturesFolderPath = EditorGUILayout.TextField("Folder", TexturesFolderPath);
                if (GUILayout.Button("Browse", GUILayout.Width(60)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Select Texture Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selected))
                    {
                        if (selected.StartsWith(Application.dataPath)) TexturesFolderPath = "Assets" + selected.Substring(Application.dataPath.Length);
                        else SetStatus(2, "Selected folder must be inside the project's Assets folder.");
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField($"Textures ({textures.Count})", GetStyle(alignment: TextAnchor.MiddleLeft));
                DrawDropArea();

                // FIX: constrain scroll height so status message is visible below it
                float scrollHeight = Mathf.Clamp(textures.Count * 22f + 10f, 44f, 150f);
                ScrollPosition = EditorGUILayout.BeginScrollView(ScrollPosition, GUILayout.Height(scrollHeight));
                for (int i = textures.Count - 1; i >= 0; i--)
                {
                    EditorGUILayout.BeginHorizontal();
                    textures[i] = (Texture2D)EditorGUILayout.ObjectField(textures[i], typeof(Texture2D), false);
                    if (GUILayout.Button("X", GUILayout.Width(25))) textures.RemoveAt(i);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
        }

        void GenerateResized()
        {
            List<Texture2D> toProcess = new List<Texture2D>();

            if (CurrentInputMode == (int)InputMode.Folder)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesFolderPath }))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                    if (tex != null) toProcess.Add(tex);
                }
            }
            else
                foreach (var t in textures)
                    if (t != null)
                        toProcess.Add(t);

            if (toProcess.Count == 0) { SetStatus(2, "No valid textures to process!"); return; }

            string normalizedPath = NormalizePath(SavePath);
            EnsureDirectory(normalizedPath);

            int processedCount = 0;
            var upscaled = toProcess.Where(t => t.width < OutputSize.x || t.height < OutputSize.y).Select(t => t.name).ToList();

            foreach (var texture in toProcess)
            {
                Texture2D resized = ResizeTexture(texture, OutputSize.x, OutputSize.y);
                if (resized == null) continue;
                File.WriteAllBytes(GetUniquePath(normalizedPath, texture.name, ".png"), resized.EncodeToPNG());
                processedCount++;
            }

            AssetDatabase.Refresh();

            string noun = processedCount == 1 ? "texture" : "textures";
            if (upscaled.Count > 0) SetStatus(3, $"Created {processedCount} {noun} at: {normalizedPath}.\nBut {upscaled.Count} {(upscaled.Count > 1 ? "were" : "was")} upscaled: {string.Join(", ", upscaled)}.");
            else SetStatus(1, $"Created {processedCount} resized {noun} at: {normalizedPath}.");

            if (CurrentInputMode == (int)InputMode.IndividualTextures) textures.Clear();
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

        void DrawSpriteSheet()
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
            UseCustomSpriteNames = EditorGUILayout.Toggle("Custom Sprite Names", UseCustomSpriteNames);
            if (UseCustomSpriteNames) CustomSpriteName = EditorGUILayout.TextField("Sprite Name", CustomSpriteName);

            EditorGUILayout.Space();
            GUI.enabled = textures.Count > 0 && textures.TrueForAll(s => s != null);
            if (CenteredButton("Generate")) GenerateSpriteSheet();
            GUI.enabled = true;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField($"Sprites ({textures.Count})", GetStyle(alignment: TextAnchor.MiddleLeft));
            DrawDropArea();

            // FIX: constrain scroll height so status message is visible below it
            float scrollHeight = Mathf.Clamp(textures.Count * 22f + 10f, 44f, 150f);
            ScrollPosition = EditorGUILayout.BeginScrollView(ScrollPosition, GUILayout.Height(scrollHeight));
            for (int i = textures.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                textures[i] = (Texture2D)EditorGUILayout.ObjectField(textures[i], typeof(Texture2D), false);
                if (GUILayout.Button("↑", GUILayout.Width(25)) && i < textures.Count - 1) { var tmp = textures[i]; textures[i] = textures[i + 1]; textures[i + 1] = tmp; }
                if (GUILayout.Button("↓", GUILayout.Width(25)) && i > 0) { var tmp = textures[i]; textures[i] = textures[i - 1]; textures[i - 1] = tmp; }
                if (GUILayout.Button("X", GUILayout.Width(25))) textures.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        void GenerateSpriteSheet()
        {
            int count = textures.Count;

            // Make all sprites readable temporarily
            List<TextureImportData> importSettings = new List<TextureImportData>();
            foreach (var sprite in textures)
            {
                string path = AssetDatabase.GetAssetPath(sprite);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importSettings.Add(new TextureImportData { importer = importer, wasReadable = importer.isReadable });
                    if (!importer.isReadable) { importer.isReadable = true; AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); }
                }
            }

            // Sort largest-first for packing, but remember original list indices for naming
            int[] sortedIndices = Enumerable.Range(0, count).ToArray();
            System.Array.Sort(sortedIndices, (a, b) =>
            {
                int areaA = textures[a].width * textures[a].height;
                int areaB = textures[b].width * textures[b].height;
                return areaA != areaB ? areaB.CompareTo(areaA) : textures[b].height.CompareTo(textures[a].height);
            });

            // Bin-pack using the shared AtlasSpace helpers from OptimizationHub
            List<AtlasPlacement> placements = new List<AtlasPlacement>();
            List<AtlasSpace> freeSpaces = new List<AtlasSpace> { new AtlasSpace(0, 0, MAX_ATLAS_SIZE, MAX_ATLAS_SIZE) };
            int packedW = 0, packedH = 0;

            foreach (int si in sortedIndices)
            {
                Texture2D tex = textures[si];
                int tw = tex.width + SpritePadding;
                int th = tex.height + SpritePadding;

                AtlasSpace bestSpace = null;
                int bestWaste = int.MaxValue, bestScore = int.MaxValue;

                foreach (var space in freeSpaces)
                {
                    if (space.width < tw || space.height < th) continue;
                    int waste = (space.width - tw) + (space.height - th);
                    int newW = Mathf.Max(packedW, space.x + tw);
                    int newH = Mathf.Max(packedH, space.y + th);
                    int dW = newW - packedW, dH = newH - packedH;
                    int score = (dW == 0 && dH == 0) ? 0 :
                                (packedW < packedH ? dH * 1000 + dW : (packedH < packedW ? dW * 1000 + dH : dW + dH));

                    if (bestSpace == null || score < bestScore || (score == bestScore && waste < bestWaste))
                    { bestSpace = space; bestWaste = waste; bestScore = score; }
                }

                if (bestSpace == null) { SetStatus(2, $"Failed to pack sprite '{tex.name}' — sheet would exceed {MAX_ATLAS_SIZE}px!"); return; }

                int px = bestSpace.x, py = bestSpace.y;
                placements.Add(new AtlasPlacement { texture = tex, originalIndex = si, x = px, y = py });

                packedW = Mathf.Max(packedW, px + tw);
                packedH = Mathf.Max(packedH, py + th);
                freeSpaces.Remove(bestSpace);

                // Split remaining free space
                var newSpaces = new List<AtlasSpace>();
                if (bestSpace.width > tw) newSpaces.Add(new AtlasSpace(px + tw, py, bestSpace.width - tw, th));
                if (bestSpace.height > th) newSpaces.Add(new AtlasSpace(px, py + th, tw, bestSpace.height - th));
                if (bestSpace.width > tw && bestSpace.height > th)
                    newSpaces.Add(new AtlasSpace(px + tw, py + th, bestSpace.width - tw, bestSpace.height - th));
                foreach (var ns in newSpaces)
                    if (ns.width > 0 && ns.height > 0)
                        AddAndMergeSpace(freeSpaces, ns, placements);
            }

            // Trim padding from final sheet dimensions — padding is between sprites, not on the outer edge
            int sheetW = packedW - SpritePadding;
            int sheetH = packedH - SpritePadding;

            // Draw the sheet — texture coordinates are bottom-left origin in Unity
            Texture2D sheet = new Texture2D(sheetW, sheetH, TextureFormat.RGBA32, false);
            sheet.SetPixels(Enumerable.Repeat(Color.clear, sheetW * sheetH).ToArray());

            foreach (var p in placements)
                sheet.SetPixels(p.x, sheetH - p.y - p.texture.height, p.texture.width, p.texture.height, p.texture.GetPixels());

            sheet.Apply();

            string normalizedPath = NormalizePath(SavePath);
            EnsureDirectory(normalizedPath);

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

                // One SpriteMetaData per placement — use originalIndex to preserve list-order naming
                var spriteMetaData = new SpriteMetaData[count];
                for (int i = 0; i < placements.Count; i++)
                {
                    var p = placements[i];
                    int listIndex = p.originalIndex;
                    string spriteName = UseCustomSpriteNames
                        ? (string.IsNullOrEmpty(CustomSpriteName) ? $"{listIndex}" : $"{CustomSpriteName}{listIndex}")
                        : textures[listIndex].name;
                    spriteMetaData[i] = new SpriteMetaData
                    {
                        name = spriteName,
                        rect = new Rect(p.x, sheetH - p.y - p.texture.height, p.texture.width, p.texture.height),
                        pivot = new Vector2(0.5f, 0.5f),
                        alignment = (int)SpriteAlignment.Center
                    };
                }
#pragma warning disable
                sheetImporter.spritesheet = spriteMetaData;
#pragma warning restore
                sheetImporter.SaveAndReimport();
            }

            foreach (var data in importSettings)
                if (!data.wasReadable)
                {
                    data.importer.isReadable = false;
                    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(data.importer), ImportAssetOptions.ForceUpdate);
                }

            SetStatus(1, $"Created sprite sheet: {outputPath}.\n{sheetW}x{sheetH}px, {count} sprites packed, padding: {SpritePadding}px.");
            textures.Clear();
        }

        void DrawDropArea()
        {
            Rect dropArea = GUILayoutUtility.GetRect(0f, 50f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag & Drop Here", EditorStyles.helpBox);
            Event evt = Event.current;
            if (dropArea.Contains(evt.mousePosition) && (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (Object obj in DragAndDrop.objectReferences)
                        if (obj is Texture2D tex && !textures.Contains(tex))
                            textures.Add(tex);
                }
                evt.Use();
            }
        }
    }
}
#endif