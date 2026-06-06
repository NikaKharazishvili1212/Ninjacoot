#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Nikson
{
    // Main hub window — manages tabs, tool instances, and shared GUI utilities
    public partial class OptimizationHub : EditorWindow
    {
        // Opens the hub window from the Unity menu
        [MenuItem("Tools/Nikson/Optimization Tools")]
        static void ShowWindow()
        {
            var hub = GetWindow<OptimizationHub>("Optimization Tools");
            hub.minSize = new Vector2(420, 500);
        }

        // Tab labels shown in the selection grid
        static readonly string[] TAB_LABELS =
        {
            "Mesh Combiner",
            "Sk.Mesh Combiner",
            "UV Atlas Mapper",
            "Mesh Simplifier\nLOD Generator",
            "Sprite Sheet",
            "Texture Resizer",
            "Canvas Analyzer",
            "Scene Analyzer",
        };

        int selectedTab = 0;
        int lastTab = -1;

        // One instance of each tool — they manage their own state internally.
        MeshCombiner meshCombiner;
        SkinnedMeshCombiner skinnedMeshCombiner;
        MeshUvAtlasMapper uvAtlasMapper;
        MeshSimplifierLODGenerator meshSimplifierLODGenerator;
        SpriteSheetGenerator spriteSheetGenerator;
        TextureResizer textureResizer;
        CanvasAnalyzer canvasAnalyzer;
        SceneAnalyzer sceneAnalyzer;

        // Loads prefs and creates tool instances when the window opens
        void OnEnable()
        {
            LoadEditorPrefs();
            meshCombiner = CreateInstance<MeshCombiner>();
            skinnedMeshCombiner = CreateInstance<SkinnedMeshCombiner>();
            uvAtlasMapper = CreateInstance<MeshUvAtlasMapper>();
            meshSimplifierLODGenerator = CreateInstance<MeshSimplifierLODGenerator>();
            spriteSheetGenerator = CreateInstance<SpriteSheetGenerator>();
            textureResizer = CreateInstance<TextureResizer>();
            canvasAnalyzer = CreateInstance<CanvasAnalyzer>();
            sceneAnalyzer = CreateInstance<SceneAnalyzer>();
        }

        // Saves prefs and destroys tool instances when the window closes
        void OnDisable()
        {
            SaveEditorPrefs();
            DestroyImmediate(meshCombiner);
            DestroyImmediate(skinnedMeshCombiner);
            DestroyImmediate(uvAtlasMapper);
            DestroyImmediate(meshSimplifierLODGenerator);
            DestroyImmediate(spriteSheetGenerator);
            DestroyImmediate(textureResizer);
            DestroyImmediate(canvasAnalyzer);
            DestroyImmediate(sceneAnalyzer);
        }

        // Draws the tab bar, delegates rendering to the active tool, and shows the shared status message at the bottom
        void OnGUI()
        {
            // Tab bar
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            selectedTab = GUILayout.SelectionGrid(selectedTab, TAB_LABELS, 4, GUILayout.Height(80));
            // Clear things on new tab open
            if (selectedTab != lastTab)
            {
                lastTab = selectedTab;
                GUI.FocusControl(null);
                Textures.Clear();
                SetStatus(0, string.Empty);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("ℹ", GetStyle());

            // Draw the active tool's GUI
            switch (selectedTab)
            {
                case 0: meshCombiner.DrawGUI(); break;
                case 1: skinnedMeshCombiner.DrawGUI(); break;
                case 2: uvAtlasMapper.DrawGUI(); break;
                case 3: meshSimplifierLODGenerator.DrawGUI(); break;
                case 4: spriteSheetGenerator.DrawGUI(); break;
                case 5: textureResizer.DrawGUI(); break;
                case 6: canvasAnalyzer.DrawGUI(); break;
                case 7: sceneAnalyzer.DrawGUI(); break;
            }

            EditorGUILayout.Space(12);
            if (StatusCode != 0) EditorGUILayout.LabelField(StatusMessage, GetStyle(color: StatusCode == 1 ? Color.green : StatusCode == 2 ? Color.red : StatusCode == 3 ? Color.yellow : Color.white));
        }

        // Clears hierarchy selection — called at the end of Generate() to disable the button after use
        public static void Deselect() => Selection.activeGameObject = null;

        // Sets the shared status label shown at the bottom of the hub: 0 = none, 1 = success, 2 = error, 3 = warning, 4 = white
        public static void SetStatus(int code, string message)
        {
            StatusCode = code;
            StatusMessage = message;
        }

        // Draws a centered Reset button — used by all tools that have configurable settings
        public static void DrawResetButton() { if (CenteredButton("Reset", "Reset all settings to default values", 50, 25)) ResetToDefaults(); }

        // Draws a horizontally centered button — supports multiple buttons in one row using BeginHorizontal manually
        public static bool CenteredButton(string label, string tooltip = "", float width = 70, float height = 30)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool clicked = GUILayout.Button(new GUIContent(label, tooltip), GUILayout.Width(width), GUILayout.Height(height));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            return clicked;
        }

        // Draws the shared Save Path field with Browse button. Used in many tool in OnGUI()
        public static void DrawSavePathField()
        {
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
        }

        // Shared label style used across all tools. Icon-texts we can use:⚠   ℹ ✓ ✘   ● ◉ ■ ★   ☆ ⚙ ♦ ♣   ► ◄ ▲ ▼
        public static GUIStyle GetStyle(int fontSize = 14, TextAnchor alignment = TextAnchor.MiddleCenter, Color? color = null)
        {
            GUIStyle style = new GUIStyle(EditorStyles.wordWrappedLabel);
            style.fontSize = fontSize;
            style.alignment = alignment;
            style.normal.textColor = color ?? Color.white;
            return style;
        }

        // Returns a unique asset path by appending a number if the file already exists
        public static string GetUniquePath(string folder, string name, string ext)
        {
            string path = Path.Combine(folder, name + ext);
            int n = 1;
            while (File.Exists(path) || AssetDatabase.LoadAssetAtPath<Object>(path) != null) path = Path.Combine(folder, name + n++ + ext);
            return path;
        }

        // Normalizes a path to forward slashes with a trailing slash
        public static string NormalizePath(string raw)
        {
            string p = raw.Replace("\\", "/");
            if (!p.EndsWith("/")) p += "/";
            return p;
        }

        // Creates the directory if it doesn't exist and refreshes the asset database
        public static void EnsureDirectory(string normalizedPath)
        {
            if (!Directory.Exists(normalizedPath))
            {
                Directory.CreateDirectory(normalizedPath);
                AssetDatabase.Refresh();
            }
        }

        // Adds a free space to the atlas packing list, merging or discarding overlapping spaces
        public static void AddAndMergeSpace(List<AtlasSpace> spaces, AtlasSpace newSpace, List<AtlasPlacement> placements)
        {
            foreach (var p in placements) if (SpacesOverlap(newSpace, p)) return;
            foreach (var s in spaces) if (SpaceContainedIn(newSpace, s)) return;
            spaces.RemoveAll(s => SpaceContainedIn(s, newSpace));
            spaces.Add(newSpace);
        }

        // Returns true if the atlas space overlaps with an already placed texture
        public static bool SpacesOverlap(AtlasSpace s, AtlasPlacement p) => !(s.x >= p.x + p.texture.width || s.x + s.width <= p.x || s.y >= p.y + p.texture.height || s.y + s.height <= p.y);

        // Returns true if inner space is fully contained within outer space
        public static bool SpaceContainedIn(AtlasSpace inner, AtlasSpace outer) => inner.x >= outer.x && inner.y >= outer.y && inner.x + inner.width <= outer.x + outer.width && inner.y + inner.height <= outer.y + outer.height;

        // Duplicates a texture into a readable Texture2D via RenderTexture
        public static Texture2D DuplicateTexture(Texture2D source)
        {
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D readable = new Texture2D(source.width, source.height);
            readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }

        // Atlas space and placement types used by MeshCombiner and SkinnedMeshCombiner
        public class AtlasSpace
        {
            public int x, y, width, height;
            public AtlasSpace(int x, int y, int w, int h) { this.x = x; this.y = y; width = w; height = h; }
        }

        public struct AtlasPlacement
        {
            public Texture2D texture;
            public int originalIndex;
            public int x, y;
        }

        // Creates a new material from a source material and assigns the atlas texture. Supports Built-in, URP, and HDRP material properties
        public static Material CreateAtlasMaterial(Material sourceMat, Texture2D atlasTex, string atlasName)
        {
            Material mat = new Material(sourceMat);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", atlasTex);
            else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", atlasTex);
            else if (mat.HasProperty("_Albedo")) mat.SetTexture("_Albedo", atlasTex); // URP/HDRP common
            else if (mat.HasProperty("_ColorMap")) mat.SetTexture("_ColorMap", atlasTex);
            else mat.mainTexture = atlasTex;

            mat.name = atlasName;
            return mat;
        }

        // Atlas space and placement types used by MeshCombiner, SkinnedMeshCombiner and MeshUvAtlasMapper
        public static Texture2D GenerateTextureAtlas(List<Texture2D> textures, string atlasName, string saveFolder, out Rect[] uvRects)
        {
            uvRects = null;
            if (textures == null || textures.Count == 0) return null;

            // Make copies and sort by size (largest first)
            Texture2D[] sortedTextures = textures.Select(DuplicateTexture).ToArray();
            int[] sorted = Enumerable.Range(0, sortedTextures.Length).ToArray();
            System.Array.Sort(sorted, (a, b) =>
            {
                int areaA = sortedTextures[a].width * sortedTextures[a].height;
                int areaB = sortedTextures[b].width * sortedTextures[b].height;
                return areaA != areaB ? areaB.CompareTo(areaA) : sortedTextures[b].height.CompareTo(sortedTextures[a].height);
            });

            List<AtlasPlacement> placements = new List<AtlasPlacement>();
            List<AtlasSpace> freeSpaces = new List<AtlasSpace> { new AtlasSpace(0, 0, MAX_ATLAS_SIZE, MAX_ATLAS_SIZE) };

            int atlasW = 0, atlasH = 0;

            foreach (int i in sorted)
            {
                Texture2D tex = sortedTextures[i];
                AtlasSpace bestSpace = null;
                int bestWaste = int.MaxValue, bestScore = int.MaxValue;

                foreach (var space in freeSpaces)
                {
                    if (space.width < tex.width || space.height < tex.height) continue;

                    int waste = (space.width - tex.width) + (space.height - tex.height);
                    int newW = Mathf.Max(atlasW, space.x + tex.width);
                    int newH = Mathf.Max(atlasH, space.y + tex.height);
                    int dW = newW - atlasW, dH = newH - atlasH;

                    int score = (dW == 0 && dH == 0) ? 0 :
                                (atlasW < atlasH ? dH * 1000 + dW : (atlasH < atlasW ? dW * 1000 + dH : dW + dH));

                    if (bestSpace == null || score < bestScore || (score == bestScore && waste < bestWaste))
                    {
                        bestSpace = space;
                        bestWaste = waste;
                        bestScore = score;
                    }
                }

                if (bestSpace == null)
                {
                    Debug.LogError($"Failed to pack texture {tex.width}x{tex.height} into atlas!");
                    return null;
                }

                int px = bestSpace.x, py = bestSpace.y;
                placements.Add(new AtlasPlacement { texture = tex, originalIndex = i, x = px, y = py });

                atlasW = Mathf.Max(atlasW, px + tex.width);
                atlasH = Mathf.Max(atlasH, py + tex.height);
                freeSpaces.Remove(bestSpace);

                // Split remaining space
                List<AtlasSpace> newSpaces = new List<AtlasSpace>();
                if (bestSpace.width > tex.width) newSpaces.Add(new AtlasSpace(px + tex.width, py, bestSpace.width - tex.width, tex.height));
                if (bestSpace.height > tex.height) newSpaces.Add(new AtlasSpace(px, py + tex.height, tex.width, bestSpace.height - tex.height));
                if (bestSpace.width > tex.width && bestSpace.height > tex.height)
                    newSpaces.Add(new AtlasSpace(px + tex.width, py + tex.height, bestSpace.width - tex.width, bestSpace.height - tex.height));

                foreach (var ns in newSpaces)
                    if (ns.width > 0 && ns.height > 0)
                        AddAndMergeSpace(freeSpaces, ns, placements);
            }

            // Power of two + limit
            int potW = Mathf.NextPowerOfTwo(atlasW);
            int potH = Mathf.NextPowerOfTwo(atlasH);
            atlasW = ((potW - atlasW) / (float)atlasW > 0.25f) ? Mathf.Min(atlasW, MAX_ATLAS_SIZE) : Mathf.Min(potW, MAX_ATLAS_SIZE);
            atlasH = ((potH - atlasH) / (float)atlasH > 0.25f) ? Mathf.Min(atlasH, MAX_ATLAS_SIZE) : Mathf.Min(potH, MAX_ATLAS_SIZE);

            if (atlasW > MAX_ATLAS_SIZE || atlasH > MAX_ATLAS_SIZE)
            {
                Debug.LogError($"Atlas too large! Required: {atlasW}x{atlasH}");
                return null;
            }

            Texture2D atlas = new Texture2D(atlasW, atlasH, TextureFormat.RGBA32, true);
            atlas.SetPixels(Enumerable.Repeat(Color.clear, atlasW * atlasH).ToArray());

            uvRects = new Rect[textures.Count];
            foreach (var p in placements)
            {
                atlas.SetPixels(p.x, p.y, p.texture.width, p.texture.height, p.texture.GetPixels());
                uvRects[p.originalIndex] = new Rect((float)p.x / atlasW, (float)p.y / atlasH, (float)p.texture.width / atlasW, (float)p.texture.height / atlasH);
            }
            atlas.Apply(true);

            // Save
            string atlasPath = GetUniquePath(NormalizePath(saveFolder), atlasName, ".png");
            File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
            AssetDatabase.Refresh();

            TextureImporter importer = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.maxTextureSize = MAX_ATLAS_SIZE;
                importer.isReadable = false;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
        }
    }
}
#endif