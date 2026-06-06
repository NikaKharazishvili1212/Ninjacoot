#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace Nikson
{
    // Settings partial — all shared static fields, pref keys, defaults, and pref load/save logic
    public partial class OptimizationHub : EditorWindow
    {
        // Shared By All
        public static string SavePath;
        public static GameObject Selected => Selection.activeGameObject;
        public static int StatusCode = 0; // 0 = none, 1 = success, 2 = error, 3 = warning
        public static string StatusMessage = string.Empty;

        // Texture Simplifier & Sprite Sheet Generator
        public static List<Texture2D> Textures = new List<Texture2D>();
        public static Vector2 ScrollPosition;
        public static Vector2Int OutputSize;
        public static int InputModeHandling;
        public static string FolderPath;
        public static string SpriteSheetName;
        public static int SpritePadding;

        // MeshUvAtlasMapper
        public static bool ResizeTextures;
        public static Vector2Int TargetTextureSize;
        public static bool ApplyToObjects;

        // Mesh Combiner & Skinned Mesh Combiner
        public static string CombinedMeshName;
        public static string AtlasName;
        public static bool GenerateAtlas;
        public static int MeshHandling;

        // Mesh Simplifier
        public static int SimplifierMode;
        public static int SimplifyQuality;
        public static string SimplifiedMeshName;
        public static int LodLevelCount;
        public static int[] LodQualities;
        public static int[] LodScreenHeights;


        // Save Path For Every Tool
        public const string PREF_SAVE_PATH = "Nikson_Optimization Tools_SavePath";
        public const string DEFAULT_SAVE_PATH = "Assets/Nikson/Optimization Tools/Generated/";

        // MeshUvAtlasMapper
        public const string PREF_RESIZE_TEXTURES = "Nikson_MeshUvAtlasMapper_Resize";
        public const string PREF_TARGET_WIDTH = "Nikson_MeshUvAtlasMapper_Width";
        public const string PREF_TARGET_HEIGHT = "Nikson_MeshUvAtlasMapper_Height";
        public const string PREF_APPLY_TO_OBJECTS = "Nikson_MeshUvAtlasMapper_Apply";
        public const bool DEFAULT_RESIZE_TEXTURES = false;
        public const int DEFAULT_TEXTURE_SIZE_X = 512;
        public const int DEFAULT_TEXTURE_SIZE_Y = 512;
        public const bool DEFAULT_APPLY_TO_OBJECTS = true;

        // Mesh Combiner & Skinned Mesh Combiner
        public const string PREF_COMBINED_MESH_NAME = "Nikson_Shared_MeshName";
        public const string PREF_ATLAS_NAME = "Nikson_Shared_AtlasName";
        public const string PREF_GENERATE_ATLAS = "Nikson_Shared_GenerateAtlas";
        public const string PREF_MESH_HANDLING = "Nikson_Shared_MeshHandling";
        public const int MAX_ATLAS_SIZE = 4096;
        public const string DEFAULT_COMBINED_MESH_NAME = "CombinedMesh";
        public const string DEFAULT_ATLAS_NAME = "Atlas";
        public const bool DEFAULT_GENERATE_ATLAS = false;
        public const int DEFAULT_MESH_HANDLING = 0;
        public enum OriginalMeshHandling { Destroy, Deactivate, KeepActive }

        // Texture Simplifier
        public const string PREF_OUTPUT_WIDTH = "Nikson_TextureSimplifier_Width";
        public const string PREF_OUTPUT_HEIGHT = "Nikson_TextureSimplifier_Height";
        public const string PREF_INPUT_MODE = "Nikson_TextureSimplifier_InputMode";
        public const string PREF_FOLDER_PATH = "Nikson_TextureSimplifier_FolderPath";
        public const int DEFAULT_OUTPUT_SIZE_X = 512;
        public const int DEFAULT_OUTPUT_SIZE_Y = 512;
        public const int DEFAULT_INPUT_MODE_HANDLING = 0;
        public const string DEFAULT_FOLDER_PATH = "Assets/";
        public enum InputMode { IndividualTextures, Folder }

        // Sprite Sheet Generator
        public const string PREF_SPRITE_SHEET_NAME = "Nikson_SpriteSheet_Name";
        public const string PREF_SPRITE_PADDING = "Nikson_SpriteSheet_Padding";
        public const string DEFAULT_SPRITE_SHEET_NAME = "SpriteSheet";
        public const int DEFAULT_SPRITE_PADDING = 0;


        // Mesh Simplifier
        public const string PREF_SIMPLIFIER_MODE = "Nikson_MeshSimplifier_Mode";
        public const string PREF_SIMPLIFY_QUALITY = "Nikson_MeshSimplifier_Quality";
        public const string PREF_SIMPLIFIED_MESH_NAME = "Nikson_MeshSimplifier_MeshName";
        public const string PREF_LOD_LEVEL_COUNT = "Nikson_MeshSimplifier_LodCount";
        public const int DEFAULT_SIMPLIFIER_MODE = 0;
        public const int DEFAULT_SIMPLIFY_QUALITY = 100;
        public const string DEFAULT_SIMPLIFIED_MESH_NAME = "SimplifiedMesh";
        public const int DEFAULT_LOD_LEVEL_COUNT = 3;
        public static readonly int[] DEFAULT_LOD_QUALITIES = new int[] { 70, 50, 30 };
        public static readonly int[] DEFAULT_LOD_SCREEN_HEIGHTS = new int[] { 60, 20, 5 };
        public enum Mode { SimplifyMesh, GenerateLOD }


        // Called by 'Reset' button to reset all settings to their default values
        static void ResetToDefaults()
        {
            GUI.FocusControl(null);

            SavePath = DEFAULT_SAVE_PATH;

            CombinedMeshName = DEFAULT_COMBINED_MESH_NAME;
            AtlasName = DEFAULT_ATLAS_NAME;
            GenerateAtlas = DEFAULT_GENERATE_ATLAS;
            MeshHandling = DEFAULT_MESH_HANDLING;

            ResizeTextures = DEFAULT_RESIZE_TEXTURES;
            TargetTextureSize = new Vector2Int(DEFAULT_TEXTURE_SIZE_X, DEFAULT_TEXTURE_SIZE_Y);
            ApplyToObjects = DEFAULT_APPLY_TO_OBJECTS;

            OutputSize = new Vector2Int(DEFAULT_OUTPUT_SIZE_X, DEFAULT_OUTPUT_SIZE_Y);
            InputModeHandling = DEFAULT_INPUT_MODE_HANDLING;
            FolderPath = DEFAULT_FOLDER_PATH;

            SpriteSheetName = DEFAULT_SPRITE_SHEET_NAME;
            SpritePadding = DEFAULT_SPRITE_PADDING;

            SimplifierMode = DEFAULT_SIMPLIFIER_MODE;
            SimplifyQuality = DEFAULT_SIMPLIFY_QUALITY;
            SimplifiedMeshName = DEFAULT_SIMPLIFIED_MESH_NAME;
            LodLevelCount = DEFAULT_LOD_LEVEL_COUNT;
            LodQualities = (int[])DEFAULT_LOD_QUALITIES.Clone();
            LodScreenHeights = (int[])DEFAULT_LOD_SCREEN_HEIGHTS.Clone();
        }

        // Saves all settings to EditorPrefs — called on OnDisable
        void SaveEditorPrefs()
        {
            EditorPrefs.SetString(PREF_SAVE_PATH, SavePath);

            // MeshUvAtlasMapper
            EditorPrefs.SetBool(PREF_RESIZE_TEXTURES, ResizeTextures);
            EditorPrefs.SetInt(PREF_TARGET_WIDTH, TargetTextureSize.x);
            EditorPrefs.SetInt(PREF_TARGET_HEIGHT, TargetTextureSize.y);
            EditorPrefs.SetBool(PREF_APPLY_TO_OBJECTS, ApplyToObjects);

            // Mesh Combiner & Skinned Mesh Combiner
            EditorPrefs.SetString(PREF_COMBINED_MESH_NAME, CombinedMeshName);
            EditorPrefs.SetString(PREF_ATLAS_NAME, AtlasName);
            EditorPrefs.SetBool(PREF_GENERATE_ATLAS, GenerateAtlas);
            EditorPrefs.SetInt(PREF_MESH_HANDLING, MeshHandling);

            // Texture Simplifier
            EditorPrefs.SetInt(PREF_OUTPUT_WIDTH, OutputSize.x);
            EditorPrefs.SetInt(PREF_OUTPUT_HEIGHT, OutputSize.y);
            EditorPrefs.SetInt(PREF_INPUT_MODE, InputModeHandling);

            // Sprite Sheet Generator
            EditorPrefs.SetString(PREF_SPRITE_SHEET_NAME, SpriteSheetName);
            EditorPrefs.SetInt(PREF_SPRITE_PADDING, SpritePadding);

            // Mesh Simplifier / LodGenerator
            EditorPrefs.SetInt(PREF_SIMPLIFIER_MODE, SimplifierMode);
            EditorPrefs.SetInt(PREF_SIMPLIFY_QUALITY, SimplifyQuality);
            EditorPrefs.SetString(PREF_SIMPLIFIED_MESH_NAME, SimplifiedMeshName);
            EditorPrefs.SetInt(PREF_LOD_LEVEL_COUNT, LodLevelCount);
        }

        // Loads all settings from EditorPrefs — called on OnEnable
        void LoadEditorPrefs()
        {
            SavePath = EditorPrefs.GetString(PREF_SAVE_PATH, DEFAULT_SAVE_PATH);

            // MeshUvAtlasMapper
            ResizeTextures = EditorPrefs.GetBool(PREF_RESIZE_TEXTURES, DEFAULT_RESIZE_TEXTURES);
            TargetTextureSize = new Vector2Int(EditorPrefs.GetInt(PREF_TARGET_WIDTH, DEFAULT_TEXTURE_SIZE_X), EditorPrefs.GetInt(PREF_TARGET_HEIGHT, DEFAULT_TEXTURE_SIZE_Y));
            ApplyToObjects = EditorPrefs.GetBool(PREF_APPLY_TO_OBJECTS, DEFAULT_APPLY_TO_OBJECTS);

            // Mesh Combiner & Skinned Mesh Combiner
            CombinedMeshName = EditorPrefs.GetString(PREF_COMBINED_MESH_NAME, DEFAULT_COMBINED_MESH_NAME);
            AtlasName = EditorPrefs.GetString(PREF_ATLAS_NAME, DEFAULT_ATLAS_NAME);
            GenerateAtlas = EditorPrefs.GetBool(PREF_GENERATE_ATLAS, DEFAULT_GENERATE_ATLAS);
            MeshHandling = EditorPrefs.GetInt(PREF_MESH_HANDLING, DEFAULT_MESH_HANDLING);

            // Texture Simplifier
            OutputSize = new Vector2Int(EditorPrefs.GetInt(PREF_OUTPUT_WIDTH, DEFAULT_OUTPUT_SIZE_X), EditorPrefs.GetInt(PREF_OUTPUT_HEIGHT, DEFAULT_OUTPUT_SIZE_Y));
            InputModeHandling = EditorPrefs.GetInt(PREF_INPUT_MODE, DEFAULT_INPUT_MODE_HANDLING);
            FolderPath = EditorPrefs.GetString(PREF_FOLDER_PATH, DEFAULT_FOLDER_PATH);

            // Sprite Sheet Generator
            SpriteSheetName = EditorPrefs.GetString(PREF_SPRITE_SHEET_NAME, DEFAULT_SPRITE_SHEET_NAME);
            SpritePadding = EditorPrefs.GetInt(PREF_SPRITE_PADDING, DEFAULT_SPRITE_PADDING);

            // Mesh Simplifier / LodGenerator
            SimplifierMode = EditorPrefs.GetInt(PREF_SIMPLIFIER_MODE, DEFAULT_SIMPLIFIER_MODE);
            SimplifyQuality = EditorPrefs.GetInt(PREF_SIMPLIFY_QUALITY, DEFAULT_SIMPLIFY_QUALITY);
            SimplifiedMeshName = EditorPrefs.GetString(PREF_SIMPLIFIED_MESH_NAME, DEFAULT_SIMPLIFIED_MESH_NAME);
            LodLevelCount = EditorPrefs.GetInt(PREF_LOD_LEVEL_COUNT, DEFAULT_LOD_LEVEL_COUNT);
            LodQualities = (int[])DEFAULT_LOD_QUALITIES.Clone();
            LodScreenHeights = (int[])DEFAULT_LOD_SCREEN_HEIGHTS.Clone();
        }
    }
}
#endif