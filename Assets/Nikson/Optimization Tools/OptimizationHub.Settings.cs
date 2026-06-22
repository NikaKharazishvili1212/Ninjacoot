#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Nikson
{
    // Settings partial — all shared static fields, pref keys, defaults, and pref load/save logic
    public partial class OptimizationHub : EditorWindow
    {
        // Shared By All
        public const string PREF_SAVE_PATH = "Nikson_Optimization Tools_SavePath";
        public const string DEFAULT_SAVE_PATH = "Assets/Nikson/Optimization Tools/Generated/";
        public static string SavePath;

        public static GameObject Selected => Selection.activeGameObject;
        public static int StatusCode = 0; // 0 = none, 1 = success, 2 = error, 3 = warning
        public static string StatusMessage = string.Empty;


        // Mesh Combiner
        public const string PREF_MESH_COMBINER_MODE = "Nikson_MeshCombiner_Mode";
        public static readonly string[] MESH_COMBINER_MODE_LABELS = { "Mesh Combiner", "Sk.Mesh Combiner" };
        public static int SelectedMeshCombinerMode;

        public const string PREF_COMBINED_MESH_NAME = "Nikson_MeshCombiner_MeshName";
        public const string DEFAULT_COMBINED_MESH_NAME = "CombinedMesh";
        public static string CombinedMeshName;

        public const string PREF_ATLAS_NAME = "Nikson_MeshCombiner_AtlasName";
        public const string DEFAULT_ATLAS_NAME = "Atlas";
        public static string AtlasName;

        public const string PREF_GENERATE_ATLAS = "Nikson_MeshCombiner_GenerateAtlas";
        public const bool DEFAULT_GENERATE_ATLAS = false;
        public static bool GenerateAtlas;

        public const string PREF_MESH_HANDLING = "Nikson_MeshCombiner_MeshHandling";
        public const int DEFAULT_MESH_HANDLING = 0;
        public enum OriginalMeshHandling { Destroy, Deactivate, KeepActive }
        public static int CurrentOriginalMeshHandling;

        public const int MAX_ATLAS_SIZE = 4096;


        // MeshUvAtlasMapper
        public const string PREF_RESIZE_TEXTURES = "Nikson_MeshUvAtlasMapper_Resize";
        public const bool DEFAULT_RESIZE_TEXTURES = false;
        public static bool ResizeTextures;

        public const string PREF_TARGET_WIDTH = "Nikson_MeshUvAtlasMapper_Width";
        public const string PREF_TARGET_HEIGHT = "Nikson_MeshUvAtlasMapper_Height";
        public const int DEFAULT_TEXTURE_SIZE_X = 512;
        public const int DEFAULT_TEXTURE_SIZE_Y = 512;
        public static Vector2Int TargetTextureSize;

        public const string PREF_APPLY_TO_OBJECTS = "Nikson_MeshUvAtlasMapper_Apply";
        public const bool DEFAULT_APPLY_TO_OBJECTS = true;
        public static bool ApplyToObjects;


        // Texture Tools
        public const string PREF_TEXTURE_TOOLS_MODE = "Nikson_TextureTools_Mode";
        public static readonly string[] TEXTURE_TOOLS_MODE_LABELS = { "Texture Resizer", "Sprite Sheet" };
        public static int SelectedTextureToolsMode;

        public const string PREF_OUTPUT_WIDTH = "Nikson_TextureResizer_Width";
        public const string PREF_OUTPUT_HEIGHT = "Nikson_TextureResizer_Height";
        public const int DEFAULT_OUTPUT_SIZE_X = 512;
        public const int DEFAULT_OUTPUT_SIZE_Y = 512;
        public static Vector2 ScrollPosition;
        public static Vector2Int OutputSize;

        public const string PREF_INPUT_MODE = "Nikson_TextureResizer_InputMode";
        public const int DEFAULT_INPUT_MODE_HANDLING = 0;
        public enum InputMode { IndividualTextures, Folder }
        public static int CurrentInputMode;

        public const string PREF_TEXTURES_FOLDER_PATH = "Nikson_TextureResizer_TexturesFolderPath";
        public const string DEFAULT_TEXTURES_FOLDER_PATH = "Assets/";
        public static string TexturesFolderPath;

        public const string PREF_SPRITE_SHEET_NAME = "Nikson_SpriteSheet_Name";
        public const string DEFAULT_SPRITE_SHEET_NAME = "SpriteSheet";
        public static string SpriteSheetName;

        public const string PREF_SPRITE_PADDING = "Nikson_SpriteSheet_Padding";
        public const int DEFAULT_SPRITE_PADDING = 0;
        public static int SpritePadding;

        public const string PREF_USE_CUSTOM_SPRITE_NAMES = "Nikson_SpriteSheet_UseCustomNames";
        public const bool DEFAULT_USE_CUSTOM_SPRITE_NAMES = true;
        public static bool UseCustomSpriteNames;

        public const string PREF_CUSTOM_SPRITE_NAME = "Nikson_SpriteSheet_CustomName";
        public const string DEFAULT_CUSTOM_SPRITE_NAME = "Icon";
        public static string CustomSpriteName;

        // Analyzer
        public const string PREF_ANALYZER_MODE = "Nikson_Analyzer_Mode";
        public static readonly string[] ANALYZER_MODE_LABELS = { "Canvas", "Scene", "Project", "Duplicates" };
        public static int SelectedAnalyzerMode;


        // Called by 'Reset' button to reset all settings to their default values
        static void ResetToDefaults()
        {
            GUI.FocusControl(null); // Deselect selected field
            SavePath = DEFAULT_SAVE_PATH;

            // Mesh Combiner
            CombinedMeshName = DEFAULT_COMBINED_MESH_NAME;
            AtlasName = DEFAULT_ATLAS_NAME;
            GenerateAtlas = DEFAULT_GENERATE_ATLAS;
            CurrentOriginalMeshHandling = DEFAULT_MESH_HANDLING;

            // MeshUvAtlasMapper
            ResizeTextures = DEFAULT_RESIZE_TEXTURES;
            TargetTextureSize = new Vector2Int(DEFAULT_TEXTURE_SIZE_X, DEFAULT_TEXTURE_SIZE_Y);
            ApplyToObjects = DEFAULT_APPLY_TO_OBJECTS;

            // Texture Tools
            OutputSize = new Vector2Int(DEFAULT_OUTPUT_SIZE_X, DEFAULT_OUTPUT_SIZE_Y);
            TexturesFolderPath = DEFAULT_TEXTURES_FOLDER_PATH;
            SpriteSheetName = DEFAULT_SPRITE_SHEET_NAME;
            SpritePadding = DEFAULT_SPRITE_PADDING;
            UseCustomSpriteNames = DEFAULT_USE_CUSTOM_SPRITE_NAMES;
            CustomSpriteName = DEFAULT_CUSTOM_SPRITE_NAME;
        }

        // Saves all settings to EditorPrefs — called on OnDisable
        void SaveEditorPrefs()
        {
            EditorPrefs.SetString(PREF_SAVE_PATH, SavePath);

            // Mesh Combiner & Skinned Mesh Combiner
            EditorPrefs.SetInt(PREF_MESH_COMBINER_MODE, SelectedMeshCombinerMode);
            EditorPrefs.SetString(PREF_COMBINED_MESH_NAME, CombinedMeshName);
            EditorPrefs.SetString(PREF_ATLAS_NAME, AtlasName);
            EditorPrefs.SetBool(PREF_GENERATE_ATLAS, GenerateAtlas);
            EditorPrefs.SetInt(PREF_MESH_HANDLING, CurrentOriginalMeshHandling);

            // MeshUvAtlasMapper
            EditorPrefs.SetBool(PREF_RESIZE_TEXTURES, ResizeTextures);
            EditorPrefs.SetInt(PREF_TARGET_WIDTH, TargetTextureSize.x);
            EditorPrefs.SetInt(PREF_TARGET_HEIGHT, TargetTextureSize.y);
            EditorPrefs.SetBool(PREF_APPLY_TO_OBJECTS, ApplyToObjects);

            // Texture Tools
            EditorPrefs.SetInt(PREF_TEXTURE_TOOLS_MODE, SelectedTextureToolsMode);
            EditorPrefs.SetInt(PREF_OUTPUT_WIDTH, OutputSize.x);
            EditorPrefs.SetInt(PREF_OUTPUT_HEIGHT, OutputSize.y);
            EditorPrefs.SetInt(PREF_INPUT_MODE, CurrentInputMode);
            EditorPrefs.SetString(PREF_TEXTURES_FOLDER_PATH, TexturesFolderPath);
            EditorPrefs.SetString(PREF_SPRITE_SHEET_NAME, SpriteSheetName);
            EditorPrefs.SetInt(PREF_SPRITE_PADDING, SpritePadding);
            EditorPrefs.SetBool(PREF_USE_CUSTOM_SPRITE_NAMES, UseCustomSpriteNames);
            EditorPrefs.SetString(PREF_CUSTOM_SPRITE_NAME, CustomSpriteName);

            // Analyzer
            EditorPrefs.SetInt(PREF_ANALYZER_MODE, SelectedAnalyzerMode);
        }

        // Loads all settings from EditorPrefs — called on OnEnable
        void LoadEditorPrefs()
        {
            SavePath = EditorPrefs.GetString(PREF_SAVE_PATH, DEFAULT_SAVE_PATH);

            // Mesh Combiner
            SelectedMeshCombinerMode = EditorPrefs.GetInt(PREF_MESH_COMBINER_MODE, 0);
            CombinedMeshName = EditorPrefs.GetString(PREF_COMBINED_MESH_NAME, DEFAULT_COMBINED_MESH_NAME);
            AtlasName = EditorPrefs.GetString(PREF_ATLAS_NAME, DEFAULT_ATLAS_NAME);
            GenerateAtlas = EditorPrefs.GetBool(PREF_GENERATE_ATLAS, DEFAULT_GENERATE_ATLAS);
            CurrentOriginalMeshHandling = EditorPrefs.GetInt(PREF_MESH_HANDLING, DEFAULT_MESH_HANDLING);

            // MeshUvAtlasMapper
            ResizeTextures = EditorPrefs.GetBool(PREF_RESIZE_TEXTURES, DEFAULT_RESIZE_TEXTURES);
            TargetTextureSize = new Vector2Int(EditorPrefs.GetInt(PREF_TARGET_WIDTH, DEFAULT_TEXTURE_SIZE_X), EditorPrefs.GetInt(PREF_TARGET_HEIGHT, DEFAULT_TEXTURE_SIZE_Y));
            ApplyToObjects = EditorPrefs.GetBool(PREF_APPLY_TO_OBJECTS, DEFAULT_APPLY_TO_OBJECTS);

            // Texture Tools
            SelectedTextureToolsMode = EditorPrefs.GetInt(PREF_TEXTURE_TOOLS_MODE, 0);
            OutputSize = new Vector2Int(EditorPrefs.GetInt(PREF_OUTPUT_WIDTH, DEFAULT_OUTPUT_SIZE_X), EditorPrefs.GetInt(PREF_OUTPUT_HEIGHT, DEFAULT_OUTPUT_SIZE_Y));
            CurrentInputMode = EditorPrefs.GetInt(PREF_INPUT_MODE, DEFAULT_INPUT_MODE_HANDLING);
            TexturesFolderPath = EditorPrefs.GetString(PREF_TEXTURES_FOLDER_PATH, DEFAULT_TEXTURES_FOLDER_PATH);
            SpriteSheetName = EditorPrefs.GetString(PREF_SPRITE_SHEET_NAME, DEFAULT_SPRITE_SHEET_NAME);
            SpritePadding = EditorPrefs.GetInt(PREF_SPRITE_PADDING, DEFAULT_SPRITE_PADDING);
            UseCustomSpriteNames = EditorPrefs.GetBool(PREF_USE_CUSTOM_SPRITE_NAMES, DEFAULT_USE_CUSTOM_SPRITE_NAMES);
            CustomSpriteName = EditorPrefs.GetString(PREF_CUSTOM_SPRITE_NAME, DEFAULT_CUSTOM_SPRITE_NAME);

            // Analyzer
            SelectedAnalyzerMode = EditorPrefs.GetInt(PREF_ANALYZER_MODE, 0);
        }
    }
}
#endif