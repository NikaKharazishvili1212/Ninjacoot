#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using static Nikson.OptimizationHub;

namespace Nikson
{
    public class MeshUvAtlasMapper : ScriptableObject
    {
        Dictionary<MeshTextureKey, Rect> uvMappings = new Dictionary<MeshTextureKey, Rect>();

        class MeshTextureKey
        {
            public Mesh mesh;
            public Texture2D texture;

            public MeshTextureKey(Mesh m, Texture2D t)
            {
                mesh = m;
                texture = t;
            }

            public override bool Equals(object obj)
            {
                if (obj is MeshTextureKey other) return mesh == other.mesh && texture == other.texture;
                return false;
            }

            public override int GetHashCode() => (mesh?.GetHashCode() ?? 0) ^ (texture?.GetHashCode() ?? 0);
        }

        public void OnGUI()
        {
            DrawIcon();
            EditorGUILayout.LabelField(
                "Remaps all meshes under the selected parent GameObject to a shared texture atlas. " +
                "Each mesh keeps its own identity — UVs are adjusted per-object rather than merging into one mesh.\n\n" +
                "Select a parent GameObject in the Hierarchy, select options and then click \"Generate\". " +
                "If a file already exists, a number will be appended automatically (e.g. Atlas1).",
                GetStyle());

            EditorGUILayout.Space();
            DrawResetButton();
            EditorGUILayout.Space();

            DrawSavePathField();

            AtlasName = EditorGUILayout.TextField("Atlas Name", AtlasName);
            ResizeTextures = EditorGUILayout.Toggle("Resize Textures", ResizeTextures);
            if (ResizeTextures) TargetTextureSize = EditorGUILayout.Vector2IntField("Target Size", TargetTextureSize);
            ApplyToObjects = EditorGUILayout.Toggle("Apply to Objects", ApplyToObjects);

            EditorGUILayout.Space();
            GUI.enabled = Selection.gameObjects.Length == 1;
            if (CenteredButton("Generate")) Generate();
            GUI.enabled = true;
            EditorGUILayout.Space();
        }

        public void Generate()
        {
            MeshRenderer[] renderers = Selected.GetComponentsInChildren<MeshRenderer>(true);
            MeshFilter[] filters = Selected.GetComponentsInChildren<MeshFilter>(true);

            if (renderers.Length == 0)
            {
                SetStatus(2, "No MeshRenderers found in children!");
                return;
            }
            if (renderers.Length == 1)
            {
                SetStatus(2, "You are trying to generate atlas for one Mesh only!");
                return;
            }

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
                if (mainTex == null) continue;

                MeshTextureKey key = new MeshTextureKey(mesh, mainTex);
                meshTextureKeys.Add(key);

                if (!uniqueTextures.ContainsKey(key))
                {
                    uniqueTextures[key] = mainTex;
                    objectNames[key] = filters[i].gameObject.name;
                }
            }

            if (uniqueTextures.Count == 0)
            {
                SetStatus(2, "No valid mesh-texture pairs found!");
                return;
            }

            List<TextureImportData> originalImportSettings = new List<TextureImportData>();
            MakeTexturesReadable(uniqueTextures.Values.ToArray(), originalImportSettings);

            List<Texture2D> textureList = new List<Texture2D>();
            List<MeshTextureKey> keys = new List<MeshTextureKey>();

            foreach (var kvp in uniqueTextures)
            {
                Texture2D tex = kvp.Value;
                if (ResizeTextures)
                    tex = ResizeTexture(tex, TargetTextureSize.x, TargetTextureSize.y);

                textureList.Add(DuplicateTexture(tex));
                keys.Add(kvp.Key);
            }

            Rect[] uvRects = null;
            Texture2D atlasTex = GenerateTextureAtlas(textureList, AtlasName, SavePath, out uvRects);
            if (atlasTex == null)
            {
                RestoreTextureReadability(originalImportSettings);
                return;
            }

            Material firstMat = renderers[0].sharedMaterial;
            Material atlasMaterial = new Material(firstMat);
            if (atlasMaterial.HasProperty("_BaseMap")) atlasMaterial.SetTexture("_BaseMap", atlasTex);
            else if (atlasMaterial.HasProperty("_MainTex")) atlasMaterial.SetTexture("_MainTex", atlasTex);
            else atlasMaterial.mainTexture = atlasTex;

            atlasMaterial.name = AtlasName;
            string normalizedPath = NormalizePath(SavePath);
            EnsureDirectory(normalizedPath);
            string matPath = GetUniquePath(normalizedPath, AtlasName, ".mat");
            AssetDatabase.CreateAsset(atlasMaterial, matPath);

            uvMappings.Clear();
            for (int i = 0; i < keys.Count && i < uvRects.Length; i++)
                uvMappings[keys[i]] = uvRects[i];

            Dictionary<MeshTextureKey, string> savedPaths;
            SaveMappedMeshes(normalizedPath, objectNames, out savedPaths);

            if (ApplyToObjects) ApplyMappedMeshesAndMaterial(filters, renderers, atlasMaterial, savedPaths);

            RestoreTextureReadability(originalImportSettings);

            string resizeInfo = ResizeTextures ? $" (resized to {TargetTextureSize.x}x{TargetTextureSize.y})" : string.Empty;
            SetStatus(1, $"Created atlas: {matPath}.\nSize: {atlasTex.width}x{atlasTex.height}{resizeInfo}.");
            EditorUtility.SetDirty(Selected);
            Deselect();
        }

        Texture2D ResizeTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            if (source.width == targetWidth && source.height == targetHeight) return source;

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

        void MakeTexturesReadable(Texture2D[] textures, List<TextureImportData> settings)
        {
            foreach (var texture in textures)
            {
                string path = AssetDatabase.GetAssetPath(texture);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    settings.Add(new TextureImportData { importer = importer, wasReadable = importer.isReadable });
                    if (!importer.isReadable)
                    {
                        importer.isReadable = true;
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
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

        void SaveMappedMeshes(string folder, Dictionary<MeshTextureKey, string> objectNames, out Dictionary<MeshTextureKey, string> savedPaths)
        {
            savedPaths = new Dictionary<MeshTextureKey, string>();
            foreach (var entry in uvMappings)
            {
                Mesh newMesh = Instantiate(entry.Key.mesh);
                Vector2[] uvs = new Vector2[newMesh.vertexCount];

                for (int i = 0; i < uvs.Length; i++)
                    uvs[i] = new Vector2(
                        Mathf.Lerp(entry.Value.xMin, entry.Value.xMax, newMesh.uv[i].x),
                        Mathf.Lerp(entry.Value.yMin, entry.Value.yMax, newMesh.uv[i].y)
                    );

                newMesh.uv = uvs;

                string baseName = objectNames[entry.Key];
                string meshPath = GetUniquePath(folder, baseName, ".asset");
                AssetDatabase.CreateAsset(newMesh, meshPath);
                savedPaths[entry.Key] = meshPath;
            }
            AssetDatabase.SaveAssets();
        }

        void ApplyMappedMeshesAndMaterial(MeshFilter[] filters, MeshRenderer[] renderers, Material atlasMaterial, Dictionary<MeshTextureKey, string> savedPaths)
        {
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh originalMesh = filters[i].sharedMesh;
                if (originalMesh == null) continue;
                Material mat = renderers[i].sharedMaterial;
                if (mat == null) continue;
                Texture2D mainTex = mat.mainTexture as Texture2D;
                if (mainTex == null) continue;

                foreach (var kvp in savedPaths)
                {
                    if (kvp.Key.mesh == originalMesh && kvp.Key.texture == mainTex)
                    {
                        Mesh mappedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(kvp.Value);
                        if (mappedMesh != null)
                        {
                            filters[i].sharedMesh = mappedMesh;
                            renderers[i].sharedMaterial = atlasMaterial;
                        }
                        break;
                    }
                }
            }
        }
    }
}
#endif