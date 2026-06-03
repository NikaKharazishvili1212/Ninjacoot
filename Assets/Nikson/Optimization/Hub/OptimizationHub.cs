#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

namespace Nikson
{
    public class OptimizationHub : EditorWindow
    {
        // Open
        [MenuItem("Tools/Nikson/Optimization Tools")]
        static void ShowWindow()
        {
            var hub = GetWindow<OptimizationHub>("Optimization Tools");
            hub.minSize = new Vector2(420, 500);
        }

        // Tab definitions
        static readonly string[] TAB_LABELS =
        {
            "Mesh Combiner",
            "Skinned Mesh Combiner",
            "Mesh Simplifier",
            "Texture Simplifier",
            "UV Atlas Mapper",
            "LOD Generator",
            "Canvas Analyzer",
            "Missing Scripts",
        };

        int selectedTab = 0;
        int lastTab = -1;

        // One instance of each tool — they manage their own state internally
        MeshCombiner meshCombiner;
        SkinnedMeshCombiner skinnedMeshCombiner;
        MeshSimplifier meshSimplifier;
        TextureSimplifier textureSimplifier;
        MeshUvAtlasMapper uvAtlasMapper;
        LodGenerator lodGenerator;
        CanvasAnalyzer canvasAnalyzer;
        MissingScriptsCleaner missingScriptsCleaner;

        // Lifecycle
        void OnEnable()
        {
            SavePath = EditorPrefs.GetString(PREF_SAVE_PATH, "Assets/Nikson/Optimization/Generated/");

            Lod0Percent = EditorPrefs.GetInt(PREF_LOD0, 80);
            Lod1Percent = EditorPrefs.GetInt(PREF_LOD1, 60);
            Lod2Percent = EditorPrefs.GetInt(PREF_LOD2, 40);

            SimplifiedMeshName = EditorPrefs.GetString(PREF_SIMPLIFIED_MESH_NAME, "SimplifiedMesh");
            MeshQuality = EditorPrefs.GetInt(PREF_QUALITY, 100);
            ApplyToMesh = EditorPrefs.GetBool(PREF_APPLY_TO_MESH, true);

            ResizeTextures = EditorPrefs.GetBool(PREF_RESIZE_TEXTURES, false);
            TargetTextureSize = new Vector2Int(EditorPrefs.GetInt(PREF_TARGET_WIDTH, 512), EditorPrefs.GetInt(PREF_TARGET_HEIGHT, 512));
            ApplyToObjects = EditorPrefs.GetBool(PREF_APPLY_TO_OBJECTS, true);

            SharedMeshName = EditorPrefs.GetString(PREF_MESH_NAME, "CombinedMesh");
            SharedAtlasName = EditorPrefs.GetString(PREF_ATLAS_NAME, "Atlas");
            SharedGenerateAtlas = EditorPrefs.GetBool(PREF_GENERATE_ATLAS, true);
            SharedMeshHandling = EditorPrefs.GetInt(PREF_MESH_HANDLING, (int)OriginalMeshHandling.Deactivate);

            OutputSize = new Vector2Int(EditorPrefs.GetInt(PREF_OUTPUT_WIDTH, 512), EditorPrefs.GetInt(PREF_OUTPUT_HEIGHT, 512));
            InputModeHandling = EditorPrefs.GetInt(PREF_INPUT_MODE, (int)InputMode.Folder);
            FolderPath = EditorPrefs.GetString(PREF_FOLDER_PATH, "Assets/");

            // CreateInstance keeps each tool alive without opening its own window. OnEnable/OnDisable on the sub-tools is called automatically
            meshCombiner = CreateInstance<MeshCombiner>();
            skinnedMeshCombiner = CreateInstance<SkinnedMeshCombiner>();
            meshSimplifier = CreateInstance<MeshSimplifier>();
            textureSimplifier = CreateInstance<TextureSimplifier>();
            uvAtlasMapper = CreateInstance<MeshUvAtlasMapper>();
            lodGenerator = CreateInstance<LodGenerator>();
            canvasAnalyzer = CreateInstance<CanvasAnalyzer>();
            missingScriptsCleaner = CreateInstance<MissingScriptsCleaner>();
        }

        void OnDisable()
        {
            EditorPrefs.SetString(PREF_SAVE_PATH, SavePath);

            EditorPrefs.SetInt(PREF_LOD0, Lod0Percent);
            EditorPrefs.SetInt(PREF_LOD1, Lod1Percent);
            EditorPrefs.SetInt(PREF_LOD2, Lod2Percent);

            EditorPrefs.SetString(PREF_SIMPLIFIED_MESH_NAME, SimplifiedMeshName);
            EditorPrefs.SetInt(PREF_QUALITY, MeshQuality);
            EditorPrefs.SetBool(PREF_APPLY_TO_MESH, ApplyToMesh);

            EditorPrefs.SetBool(PREF_RESIZE_TEXTURES, ResizeTextures);
            EditorPrefs.SetInt(PREF_TARGET_WIDTH, TargetTextureSize.x);
            EditorPrefs.SetInt(PREF_TARGET_HEIGHT, TargetTextureSize.y);
            EditorPrefs.SetBool(PREF_APPLY_TO_OBJECTS, ApplyToObjects);

            EditorPrefs.SetString(PREF_MESH_NAME, SharedMeshName);
            EditorPrefs.SetString(PREF_ATLAS_NAME, SharedAtlasName);
            EditorPrefs.SetBool(PREF_GENERATE_ATLAS, SharedGenerateAtlas);
            EditorPrefs.SetInt(PREF_MESH_HANDLING, SharedMeshHandling);

            EditorPrefs.SetInt(PREF_OUTPUT_WIDTH, OutputSize.x);
            EditorPrefs.SetInt(PREF_OUTPUT_HEIGHT, OutputSize.y);
            EditorPrefs.SetInt(PREF_INPUT_MODE, InputModeHandling);

            DestroyImmediate(meshCombiner);
            DestroyImmediate(skinnedMeshCombiner);
            DestroyImmediate(meshSimplifier);
            DestroyImmediate(textureSimplifier);
            DestroyImmediate(uvAtlasMapper);
            DestroyImmediate(lodGenerator);
            DestroyImmediate(canvasAnalyzer);
            DestroyImmediate(missingScriptsCleaner);
        }

        void OnGUI()
        {
            EditorGUILayout.Space();

            // Tab bar
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            selectedTab = GUILayout.SelectionGrid(selectedTab, TAB_LABELS, 4, GUILayout.Height(80));
            if (selectedTab != lastTab) { lastTab = selectedTab; GUI.FocusControl(null); ParentObject = null; } // Clear selection on new tab open
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("⚠", NiksonStyle);

            // Active tool
            switch (selectedTab)
            {
                case 0: meshCombiner.DrawGUI(); break;
                case 1: skinnedMeshCombiner.DrawGUI(); break;
                case 2: meshSimplifier.DrawGUI(); break;
                case 3: textureSimplifier.DrawGUI(); break;
                case 4: uvAtlasMapper.DrawGUI(); break;
                case 5: lodGenerator.DrawGUI(); break;
                case 6: canvasAnalyzer.DrawGUI(); break;
                case 7: missingScriptsCleaner.DrawGUI(); break;
            }
        }

        static GUIStyle gUIStyle;
        // Icon-texts we can use:   ⚠ � ℹ ✓ ✘   ● ◉ ■ ★   ☆ ⚙ ♦ ♣   ► ◄ ▲ ▼
        public static GUIStyle NiksonStyle
        {
            get
            {
                if (gUIStyle == null)
                {
                    gUIStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
                    gUIStyle.fontSize = 14;
                    gUIStyle.alignment = TextAnchor.MiddleCenter;
                    gUIStyle.normal.textColor = Color.white;
                }
                return gUIStyle;
            }
        }


        #region Shared By All
        public static GameObject ParentObject;

        public const string PREF_SAVE_PATH = "Nikson_Optimization_SavePath";
        public static string SavePath = "Assets/Nikson/Optimization/Generated/";
        public static string GetUniquePath(string folder, string name, string ext)
        {
            string path = Path.Combine(folder, name + ext);
            int n = 1;
            while (File.Exists(path) || AssetDatabase.LoadAssetAtPath<Object>(path) != null) path = Path.Combine(folder, name + n++ + ext);
            return path;
        }
        #endregion


        #region LodGenerator
        public const string PREF_LOD0 = "Nikson_LodGenerator_Lod0";
        public const string PREF_LOD1 = "Nikson_LodGenerator_Lod1";
        public const string PREF_LOD2 = "Nikson_LodGenerator_Lod2";
        public static int Lod0Percent = 80;
        public static int Lod1Percent = 60;
        public static int Lod2Percent = 40;
        #endregion


        #region MeshSimplifier
        public const string PREF_SIMPLIFIED_MESH_NAME = "Nikson_MeshSimplifier_MeshName";
        public const string PREF_QUALITY = "Nikson_MeshSimplifier_Quality";
        public const string PREF_APPLY_TO_MESH = "Nikson_MeshSimplifier_ApplyToMesh";
        public static string SimplifiedMeshName = "SimplifiedMesh";
        public static int MeshQuality = 100;
        public static bool ApplyToMesh = true;
        #endregion


        #region MeshUvAtlasMapper
        public const string PREF_RESIZE_TEXTURES = "Nikson_MeshUvAtlasMapper_Resize";
        public const string PREF_TARGET_WIDTH = "Nikson_MeshUvAtlasMapper_Width";
        public const string PREF_TARGET_HEIGHT = "Nikson_MeshUvAtlasMapper_Height";
        public const string PREF_APPLY_TO_OBJECTS = "Nikson_MeshUvAtlasMapper_Apply";
        public static bool ResizeTextures = false;
        public static Vector2Int TargetTextureSize = new Vector2Int(512, 512);
        public static bool ApplyToObjects = true;
        #endregion


        #region MeshCombiner & SkinnedMeshCombiner & MeshUvAtlasMapper
        public const int MAX_ATLAS_SIZE = 4096;
        public const string PREF_MESH_NAME = "Nikson_Shared_MeshName";
        public const string PREF_ATLAS_NAME = "Nikson_Shared_AtlasName";
        public const string PREF_GENERATE_ATLAS = "Nikson_Shared_GenerateAtlas";
        public const string PREF_MESH_HANDLING = "Nikson_Shared_MeshHandling";
        public static string SharedMeshName = "CombinedMesh";
        public static string SharedAtlasName = "Atlas";
        public static bool SharedGenerateAtlas = true;
        public static int SharedMeshHandling = (int)OriginalMeshHandling.Destroy;
        public enum OriginalMeshHandling { Destroy, Deactivate, KeepActive }

        //  Atlas helper types both share
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

        // Helper methods both share
        public static void AddAndMergeSpace(List<AtlasSpace> spaces, AtlasSpace newSpace, List<AtlasPlacement> placements)
        {
            foreach (var p in placements) if (SpacesOverlap(newSpace, p)) return;
            foreach (var s in spaces) if (SpaceContainedIn(newSpace, s)) return;
            spaces.RemoveAll(s => SpaceContainedIn(s, newSpace));
            spaces.Add(newSpace);
        }

        public static bool SpacesOverlap(AtlasSpace s, AtlasPlacement p) =>
            !(s.x >= p.x + p.texture.width || s.x + s.width <= p.x ||
              s.y >= p.y + p.texture.height || s.y + s.height <= p.y);

        public static bool SpaceContainedIn(AtlasSpace inner, AtlasSpace outer) =>
            inner.x >= outer.x && inner.y >= outer.y &&
            inner.x + inner.width <= outer.x + outer.width &&
            inner.y + inner.height <= outer.y + outer.height;

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

        public static string NormalizePath(string raw)
        {
            string p = raw.Replace("\\", "/");
            if (!p.EndsWith("/")) p += "/";
            return p;
        }

        public static void EnsureDirectory(string normalizedPath)
        {
            if (!Directory.Exists(normalizedPath))
            {
                Directory.CreateDirectory(normalizedPath);
                AssetDatabase.Refresh();
            }
        }
        #endregion


        #region TextureSimpifier
        public const string PREF_OUTPUT_WIDTH = "Nikson_TextureSimplifier_Width";
        public const string PREF_OUTPUT_HEIGHT = "Nikson_TextureSimplifier_Height";
        public const string PREF_INPUT_MODE = "Nikson_TextureSimplifier_InputMode";
        public const string PREF_FOLDER_PATH = "Nikson_TextureSimplifier_FolderPath";
        public static Vector2Int OutputSize = new Vector2Int(512, 512);
        public static int InputModeHandling = (int)InputMode.Folder;
        public enum InputMode { IndividualTextures, Folder }
        public static string FolderPath = "Assets/";
        #endregion
    }
}
#endif