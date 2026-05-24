#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Nikson
{
    public class MeshUvAtlasMapper : EditorWindow
    {
        const string ATLAS_NAME = "Atlas";
        const int MAX_ATLAS_SIZE = 4096;

        GameObject parentObject;
        string savePath = "Nikson/Generated/";
        bool resizeTextures = false;
        Vector2Int targetTextureSize = new Vector2Int(512, 512);
        bool applyToObjects = true;
        Dictionary<MeshTextureKey, Rect> uvMappings = new Dictionary<MeshTextureKey, Rect>();

        // Key to track unique mesh + texture combinations
        class MeshTextureKey
        {
            public Mesh mesh;
            public Texture2D texture;
            public int instanceId;

            public MeshTextureKey(Mesh m, Texture2D t, int id)
            {
                mesh = m;
                texture = t;
                instanceId = id;
            }

            public override bool Equals(object obj)
            {
                if (obj is MeshTextureKey other)
                    return mesh == other.mesh && texture == other.texture;
                return false;
            }

            public override int GetHashCode()
            {
                return (mesh?.GetHashCode() ?? 0) ^ (texture?.GetHashCode() ?? 0);
            }
        }

        [MenuItem("Nikson/Optimization/3. Mesh Uv Atlas Mapper")]
        public static void ShowWindow() => GetWindow<MeshUvAtlasMapper>("Mesh Uv Atlas Mapper");

        void OnGUI()
        {
            GUILayout.Label("Mesh Uv Atlas Mapper Settings", EditorStyles.boldLabel);

            parentObject = (GameObject)EditorGUILayout.ObjectField("Parent Object", parentObject, typeof(GameObject), true);
            savePath = EditorGUILayout.TextField("Save Path", savePath);

            resizeTextures = EditorGUILayout.Toggle("Resize Textures", resizeTextures);

            GUI.enabled = resizeTextures;
            targetTextureSize = EditorGUILayout.Vector2IntField("Target Size", targetTextureSize);
            if (!resizeTextures) EditorGUILayout.HelpBox("Enable 'Resize Textures' to change texture size during atlas generation", MessageType.Info);
            else EditorGUILayout.HelpBox($"All textures will be resized to {targetTextureSize.x}x{targetTextureSize.y} before packing into atlas", MessageType.Warning);
            GUI.enabled = true;

            EditorGUILayout.Space();

            applyToObjects = EditorGUILayout.Toggle("Apply to Objects", applyToObjects);
            EditorGUILayout.HelpBox(
                "When enabled: Automatically applies the generated atlas material and remapped meshes to child objects\n" +
                "When disabled: Only saves the atlas and remapped meshes as assets",
                MessageType.Info);

            EditorGUILayout.Space();

            GUI.enabled = parentObject != null;
            if (GUILayout.Button("Map Meshes to Atlas", GUILayout.Height(30))) Generate();
            GUI.enabled = true;
        }

        public void Generate()
        {
            MeshRenderer[] renderers = parentObject.GetComponentsInChildren<MeshRenderer>();
            MeshFilter[] filters = parentObject.GetComponentsInChildren<MeshFilter>();

            if (renderers.Length == 0)
            {
                Debug.LogError("No MeshRenderers found in children!");
                return;
            }
            if (renderers.Length == 1)
            {
                Debug.LogError("You are trying to generate atlas for one Mesh only!");
                return;
            }

            // Gather ALL mesh-texture combinations (not just unique meshes!)
            List<MeshTextureKey> meshTextureKeys = new List<MeshTextureKey>();
            Dictionary<MeshTextureKey, Texture2D> uniqueTextures = new Dictionary<MeshTextureKey, Texture2D>();
            Dictionary<MeshTextureKey, string> objectNames = new Dictionary<MeshTextureKey, string>();

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null) continue;

                Material mat = renderers[i].sharedMaterial;
                if (mat == null) continue;

                Texture2D mainTex = mat.mainTexture as Texture2D;
                if (mainTex == null)
                {
                    Debug.LogWarning($"No main texture found on {renderers[i].gameObject.name}, skipping...");
                    continue;
                }

                // Create key for this specific mesh+texture combination
                MeshTextureKey key = new MeshTextureKey(mesh, mainTex, i);
                meshTextureKeys.Add(key);

                // Only add unique mesh+texture combinations to the atlas
                if (!uniqueTextures.ContainsKey(key))
                {
                    uniqueTextures[key] = mainTex;
                    objectNames[key] = filters[i].gameObject.name;
                }
            }

            if (uniqueTextures.Count == 0)
            {
                Debug.LogError("No valid mesh-texture pairs found!");
                return;
            }

            Texture2D[] textures = uniqueTextures.Values.ToArray();
            MeshTextureKey[] keys = uniqueTextures.Keys.ToArray();

            List<TextureImportData> originalImportSettings = new List<TextureImportData>();
            MakeTexturesReadable(textures, originalImportSettings);

            // Resize textures if enabled
            if (resizeTextures)
            {
                for (int i = 0; i < textures.Length; i++)
                {
                    textures[i] = ResizeTexture(textures[i], targetTextureSize[0], targetTextureSize[1]);
                    // Update the texture reference in uniqueTextures
                    uniqueTextures[keys[i]] = textures[i];
                }
            }

            // Advanced space-tracking bin packing
            int[] sortedIndices = new int[textures.Length];
            for (int i = 0; i < sortedIndices.Length; i++)
                sortedIndices[i] = i;
            System.Array.Sort(sortedIndices, (a, b) =>
            {
                int areaA = textures[a].width * textures[a].height;
                int areaB = textures[b].width * textures[b].height;
                if (areaA != areaB) return areaB.CompareTo(areaA);
                return textures[b].height.CompareTo(textures[a].height);
            });

            List<TexturePlacement> placements = new List<TexturePlacement>();
            List<FreeSpace> freeSpaces = new List<FreeSpace>();
            freeSpaces.Add(new FreeSpace(0, 0, MAX_ATLAS_SIZE, MAX_ATLAS_SIZE));

            int atlasWidth = 0;
            int atlasHeight = 0;
            uvMappings.Clear();

            // Pack each texture
            for (int idx = 0; idx < sortedIndices.Length; idx++)
            {
                int i = sortedIndices[idx];
                Texture2D texture = textures[i];
                MeshTextureKey key = keys[i];

                FreeSpace bestSpace = null;
                int bestWaste = int.MaxValue;
                int bestScore = int.MaxValue;

                foreach (var space in freeSpaces)
                {
                    if (space.width >= texture.width && space.height >= texture.height)
                    {
                        int waste = (space.width - texture.width) + (space.height - texture.height);

                        int newWidth = Mathf.Max(atlasWidth, space.x + texture.width);
                        int newHeight = Mathf.Max(atlasHeight, space.y + texture.height);

                        int widthExpansion = newWidth - atlasWidth;
                        int heightExpansion = newHeight - atlasHeight;

                        int score;
                        if (widthExpansion == 0 && heightExpansion == 0) score = 0;
                        else if (atlasWidth < atlasHeight) score = heightExpansion * 1000 + widthExpansion;
                        else if (atlasHeight < atlasWidth) score = widthExpansion * 1000 + heightExpansion;
                        else score = widthExpansion + heightExpansion;

                        if (bestSpace == null || score < bestScore || (score == bestScore && waste < bestWaste))
                        {
                            bestSpace = space;
                            bestWaste = waste;
                            bestScore = score;
                        }
                    }
                }

                if (bestSpace == null)
                {
                    int requiredWidth = atlasWidth;
                    int requiredHeight = atlasHeight;
                    requiredWidth = Mathf.Max(requiredWidth, texture.width);
                    requiredHeight = Mathf.Max(requiredHeight, texture.height);

                    for (int j = idx; j < sortedIndices.Length; j++)
                    {
                        requiredWidth = Mathf.Max(requiredWidth, textures[sortedIndices[j]].width);
                        requiredHeight += textures[sortedIndices[j]].height;
                    }

                    Debug.LogError($"Failed to pack textures into atlas! Maximum recommended atlas size is {MAX_ATLAS_SIZE}x{MAX_ATLAS_SIZE}, but approximately {requiredWidth}x{requiredHeight} (estimated) would be needed");
                    RestoreTextureReadability(originalImportSettings);
                    return;
                }

                int placeX = bestSpace.x;
                int placeY = bestSpace.y;

                placements.Add(new TexturePlacement
                {
                    texture = texture,
                    key = key,
                    x = placeX,
                    y = placeY
                });

                atlasWidth = Mathf.Max(atlasWidth, placeX + texture.width);
                atlasHeight = Mathf.Max(atlasHeight, placeY + texture.height);

                freeSpaces.Remove(bestSpace);

                List<FreeSpace> newSpaces = new List<FreeSpace>();

                if (bestSpace.width > texture.width)
                {
                    newSpaces.Add(new FreeSpace(
                        placeX + texture.width,
                        placeY,
                        bestSpace.width - texture.width,
                        texture.height
                    ));
                }

                if (bestSpace.height > texture.height)
                {
                    newSpaces.Add(new FreeSpace(
                        placeX,
                        placeY + texture.height,
                        texture.width,
                        bestSpace.height - texture.height
                    ));
                }

                if (bestSpace.width > texture.width && bestSpace.height > texture.height)
                {
                    newSpaces.Add(new FreeSpace(
                        placeX + texture.width,
                        placeY + texture.height,
                        bestSpace.width - texture.width,
                        bestSpace.height - texture.height
                    ));
                }

                foreach (var newSpace in newSpaces)
                    if (newSpace.width > 0 && newSpace.height > 0)
                        AddAndMergeSpace(freeSpaces, newSpace, placements);
            }

            // Adjust atlas size
            int potWidth = Mathf.NextPowerOfTwo(atlasWidth);
            int potHeight = Mathf.NextPowerOfTwo(atlasHeight);

            float widthWaste = (potWidth - atlasWidth) / (float)atlasWidth;
            float heightWaste = (potHeight - atlasHeight) / (float)atlasHeight;

            if (widthWaste > 0.25f || heightWaste > 0.25f)
            {
                atlasWidth = Mathf.Min(atlasWidth, MAX_ATLAS_SIZE);
                atlasHeight = Mathf.Min(atlasHeight, MAX_ATLAS_SIZE);
            }
            else
            {
                atlasWidth = Mathf.Min(potWidth, MAX_ATLAS_SIZE);
                atlasHeight = Mathf.Min(potHeight, MAX_ATLAS_SIZE);
            }

            if (atlasWidth > MAX_ATLAS_SIZE || atlasHeight > MAX_ATLAS_SIZE)
            {
                Debug.LogError($"Atlas size exceeded maximum! Required: {atlasWidth}x{atlasHeight}, Maximum: {MAX_ATLAS_SIZE}x{MAX_ATLAS_SIZE}. " +
                               $"Enable 'Resize Textures' and use a smaller target size to fit within limits.");
                RestoreTextureReadability(originalImportSettings);
                return;
            }

            // Create atlas texture
            Texture2D atlasTexture = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, true);
            Color[] clearColors = new Color[atlasWidth * atlasHeight];
            for (int i = 0; i < clearColors.Length; i++)
                clearColors[i] = Color.clear;
            atlasTexture.SetPixels(clearColors);

            foreach (var placement in placements)
            {
                atlasTexture.SetPixels(placement.x, placement.y, placement.texture.width, placement.texture.height, placement.texture.GetPixels());
                uvMappings[placement.key] = new Rect(
                    (float)placement.x / atlasWidth,
                    (float)placement.y / atlasHeight,
                    (float)placement.texture.width / atlasWidth,
                    (float)placement.texture.height / atlasHeight
                );
            }

            atlasTexture.Apply();

            string normalizedPath = savePath.Replace("\\", "/");
            if (!normalizedPath.StartsWith("Assets/")) normalizedPath = "Assets/" + normalizedPath;
            if (!normalizedPath.EndsWith("/")) normalizedPath += "/";

            if (!Directory.Exists(normalizedPath))
            {
                Directory.CreateDirectory(normalizedPath);
                AssetDatabase.Refresh();
            }

            string atlasPath = GetUniquePath(normalizedPath, ATLAS_NAME, ".png");
            File.WriteAllBytes(atlasPath, atlasTexture.EncodeToPNG());
            AssetDatabase.Refresh();

            Texture2D savedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);
            Material atlasMaterial = CreateAtlasMaterial(normalizedPath, savedAtlas);

            SaveMappedMeshes(normalizedPath, objectNames);
            if (applyToObjects) ApplyMappedMeshesAndMaterial(filters, renderers, atlasMaterial, meshTextureKeys);
            RestoreTextureReadability(originalImportSettings);

            string resizeInfo = resizeTextures ? $" (textures resized to {targetTextureSize.x}x{targetTextureSize.y})" : "";
            Debug.Log($"Created Atlas: {atlasPath}   |   Size: {atlasWidth}x{atlasHeight}   |   From: {uniqueTextures.Count} unique textures and {renderers.Length} renderers{resizeInfo}");
            EditorUtility.SetDirty(parentObject);
        }

        Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            if (source.width == targetWidth && source.height == targetHeight)
                return source;

            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight);
            rt.filterMode = FilterMode.Bilinear;

            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, true);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return result;
        }

        void AddAndMergeSpace(List<FreeSpace> spaces, FreeSpace newSpace, List<TexturePlacement> placements)
        {
            foreach (var placement in placements)
                if (SpacesOverlap(newSpace, placement))
                    return;

            foreach (var existing in spaces)
                if (SpaceContainedIn(newSpace, existing))
                    return;

            spaces.RemoveAll(s => SpaceContainedIn(s, newSpace));
            spaces.Add(newSpace);
        }

        bool SpacesOverlap(FreeSpace space, TexturePlacement placement)
        {
            return !(space.x >= placement.x + placement.texture.width ||
                     space.x + space.width <= placement.x ||
                     space.y >= placement.y + placement.texture.height ||
                     space.y + space.height <= placement.y);
        }

        bool SpaceContainedIn(FreeSpace inner, FreeSpace outer)
        {
            return inner.x >= outer.x && inner.y >= outer.y &&
                   inner.x + inner.width <= outer.x + outer.width &&
                   inner.y + inner.height <= outer.y + outer.height;
        }

        class FreeSpace
        {
            public int x, y, width, height;

            public FreeSpace(int x, int y, int width, int height)
            {
                this.x = x;
                this.y = y;
                this.width = width;
                this.height = height;
            }
        }

        struct TexturePlacement
        {
            public Texture2D texture;
            public MeshTextureKey key;
            public int x;
            public int y;
        }

        void MakeTexturesReadable(Texture2D[] textures, List<TextureImportData> settings)
        {
            foreach (var texture in textures)
            {
                string texturePath = AssetDatabase.GetAssetPath(texture);
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;

                if (importer != null)
                {
                    settings.Add(new TextureImportData { importer = importer, wasReadable = importer.isReadable });
                    if (!importer.isReadable)
                    {
                        importer.isReadable = true;
                        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
                    }
                }
            }
        }

        void SaveMappedMeshes(string folder, Dictionary<MeshTextureKey, string> objectNames)
        {
            Dictionary<MeshTextureKey, string> savedMeshPaths = new Dictionary<MeshTextureKey, string>();

            foreach (var entry in uvMappings)
            {
                Mesh newMesh = Object.Instantiate(entry.Key.mesh);
                Vector2[] uvs = new Vector2[newMesh.vertexCount];

                for (int i = 0; i < uvs.Length; i++)
                    uvs[i] = new Vector2(
                        Mathf.Lerp(entry.Value.xMin, entry.Value.xMax, newMesh.uv[i].x),
                        Mathf.Lerp(entry.Value.yMin, entry.Value.yMax, newMesh.uv[i].y)
                    );

                newMesh.uv = uvs;

                string baseName = $"{objectNames[entry.Key]}";
                string meshPath = GetUniquePath(folder, baseName, ".asset");
                AssetDatabase.CreateAsset(newMesh, meshPath);
                savedMeshPaths[entry.Key] = meshPath;
            }
            AssetDatabase.SaveAssets();
        }

        Material CreateAtlasMaterial(string folder, Texture2D atlasTexture)
        {
            MeshRenderer firstRenderer = parentObject.GetComponentInChildren<MeshRenderer>();
            Material firstMaterial = firstRenderer.sharedMaterial;

            Material atlasMaterial = new Material(firstMaterial.shader);

            if (atlasMaterial.HasProperty("_BaseMap")) atlasMaterial.SetTexture("_BaseMap", atlasTexture);
            else if (atlasMaterial.HasProperty("_MainTex")) atlasMaterial.SetTexture("_MainTex", atlasTexture);
            else atlasMaterial.mainTexture = atlasTexture;

            string materialPath = GetUniquePath(folder, ATLAS_NAME, ".mat");
            AssetDatabase.CreateAsset(atlasMaterial, materialPath);
            AssetDatabase.SaveAssets();
            return atlasMaterial;
        }

        void ApplyMappedMeshesAndMaterial(MeshFilter[] filters, MeshRenderer[] renderers, Material atlasMaterial, List<MeshTextureKey> allKeys)
        {
            string normalizedPath = savePath.Replace("\\", "/");
            if (!normalizedPath.StartsWith("Assets/")) normalizedPath = "Assets/" + normalizedPath;
            if (!normalizedPath.EndsWith("/")) normalizedPath += "/";

            for (int i = 0; i < filters.Length; i++)
            {
                Mesh originalMesh = filters[i].sharedMesh;
                if (originalMesh == null) continue;

                Material mat = renderers[i].sharedMaterial;
                if (mat == null) continue;

                Texture2D mainTex = mat.mainTexture as Texture2D;
                if (mainTex == null) continue;

                // Find the matching key for this specific mesh+texture combo
                MeshTextureKey matchingKey = null;
                foreach (var key in uvMappings.Keys)
                {
                    if (key.mesh == originalMesh && key.texture == mainTex)
                    {
                        matchingKey = key;
                        break;
                    }
                }

                if (matchingKey == null) continue;

                // Find the saved mesh for this combination
                string searchPattern = $"{filters[i].gameObject.name}*.asset";
                string[] foundFiles = Directory.GetFiles(normalizedPath, searchPattern);

                if (foundFiles.Length > 0)
                {
                    Mesh mappedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(foundFiles[0]);
                    if (mappedMesh != null)
                    {
                        filters[i].sharedMesh = mappedMesh;
                        renderers[i].sharedMaterial = atlasMaterial;
                    }
                }
            }
        }

        void RestoreTextureReadability(List<TextureImportData> settings)
        {
            foreach (var data in settings)
            {
                if (!data.wasReadable && data.importer.isReadable)
                {
                    data.importer.isReadable = false;
                    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(data.importer), ImportAssetOptions.ForceUpdate);
                }
            }
        }

        string GetUniquePath(string folder, string name, string ext)
        {
            string path = Path.Combine(folder, name + ext);
            int index = 1;
            while (File.Exists(path) || AssetDatabase.LoadAssetAtPath<Object>(path) != null) path = Path.Combine(folder, name + index++ + ext);
            return path;
        }

        class TextureImportData
        {
            public TextureImporter importer;
            public bool wasReadable;
        }
    }
}
#endif