#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Nikson.OptimizationHub;

namespace Nikson
{
    public class MeshCombiner : EditorWindow
    {
        public void DrawGUI() => OnGUI();

        void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Select the parent GameObject of the meshes you want to combine, " +
                "choose the desired settings, and click \"Generate\".\n\n" +
                "If a file with the chosen name already exists, a number will be appended automatically (e.g. Atlas1, Atlas2).",
                NiksonStyle);

            EditorGUILayout.Space();

            ParentObject = (GameObject)EditorGUILayout.ObjectField("Parent Object", ParentObject, typeof(GameObject), true);

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

            SharedMeshName = EditorGUILayout.TextField("Mesh Name", SharedMeshName);
            if (SharedGenerateAtlas) SharedAtlasName = EditorGUILayout.TextField("Atlas Name", SharedAtlasName);
            SharedGenerateAtlas = EditorGUILayout.Toggle("Generate Atlas", SharedGenerateAtlas);
            SharedMeshHandling = (int)(OriginalMeshHandling)EditorGUILayout.EnumPopup("Original Meshes", (OriginalMeshHandling)SharedMeshHandling);

            EditorGUILayout.Space();

            GUI.enabled = ParentObject != null;
            if (GUILayout.Button("Generate", GUILayout.Height(30))) Generate();
            GUI.enabled = true;
        }

        void Generate()
        {
            MeshFilter[] meshFilters = ParentObject.GetComponentsInChildren<MeshFilter>();

            if (meshFilters.Length == 0)
            {
                Debug.LogError("No MeshFilters found under the selected object!");
                return;
            }

            if (meshFilters.Length == 1)
            {
                Debug.LogError("Only one mesh found — nothing to combine!");
                return;
            }

            MeshRenderer[] renderers = ParentObject.GetComponentsInChildren<MeshRenderer>();
            var materialGroups = new Dictionary<Material, List<CombineData>>();

            for (int i = 0; i < meshFilters.Length; i++)
            {
                Mesh mesh = meshFilters[i].sharedMesh;
                if (mesh == null) continue;

                Material[] mats = renderers[i].sharedMaterials;
                for (int j = 0; j < mesh.subMeshCount; j++)
                {
                    Material mat = j < mats.Length ? mats[j] : null;
                    if (mat == null) continue;

                    if (!materialGroups.ContainsKey(mat)) materialGroups[mat] = new List<CombineData>();

                    materialGroups[mat].Add(new CombineData
                    {
                        mesh = mesh,
                        subMeshIndex = j,
                        transform = meshFilters[i].transform
                    });
                }
            }

            string normalizedPath = NormalizePath(SavePath);
            EnsureDirectory(normalizedPath);

            string meshPath = GetUniquePath(normalizedPath, SharedMeshName, ".asset");
            Mesh combinedMesh = new Mesh { name = Path.GetFileNameWithoutExtension(meshPath) };
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();

            List<Material> finalMaterials = new List<Material>();
            Rect[] uvRects = null;

            if (SharedGenerateAtlas)
            {
                Material atlasMaterial = GenerateTextureAtlas(materialGroups, normalizedPath, out uvRects);
                if (atlasMaterial == null) return;
                finalMaterials.Add(atlasMaterial);
            }

            int matIndex = 0;
            foreach (var matGroup in materialGroups)
            {
                if (!SharedGenerateAtlas) finalMaterials.Add(matGroup.Key);

                Rect uvRect = SharedGenerateAtlas ? uvRects[matIndex] : new Rect(0, 0, 1, 1);
                Matrix4x4 worldToParent = ParentObject.transform.worldToLocalMatrix;

                foreach (var data in matGroup.Value)
                {
                    Matrix4x4 transform = worldToParent * data.transform.localToWorldMatrix;
                    Vector3[] meshVerts = data.mesh.vertices;
                    Vector3[] meshNormals = data.mesh.normals;
                    Vector2[] meshUVs = data.mesh.uv;

                    for (int i = 0; i < meshVerts.Length; i++)
                    {
                        vertices.Add(transform.MultiplyPoint3x4(meshVerts[i]));
                        normals.Add(transform.MultiplyVector(meshNormals[i]).normalized);

                        Vector2 uv = (meshUVs != null && i < meshUVs.Length) ? meshUVs[i] : Vector2.zero;
                        if (SharedGenerateAtlas)
                        {
                            uv.x = uvRect.x + uv.x * uvRect.width;
                            uv.y = uvRect.y + uv.y * uvRect.height;
                        }
                        uvs.Add(uv);
                    }
                }
                matIndex++;
            }

            var vertexMap = new Dictionary<VertexKey, int>();
            var optVerts = new List<Vector3>();
            var optNormals = new List<Vector3>();
            var optUVs = new List<Vector2>();
            int[] vertexRemap = new int[vertices.Count];

            for (int i = 0; i < vertices.Count; i++)
            {
                var key = new VertexKey(vertices[i], normals[i], uvs[i]);
                if (vertexMap.TryGetValue(key, out int existing)) vertexRemap[i] = existing;
                else
                {
                    int newIdx = optVerts.Count;
                    vertexMap[key] = newIdx;
                    vertexRemap[i] = newIdx;
                    optVerts.Add(vertices[i]);
                    optNormals.Add(normals[i]);
                    optUVs.Add(uvs[i]);
                }
            }

            combinedMesh.vertices = optVerts.ToArray();
            combinedMesh.normals = optNormals.ToArray();
            combinedMesh.uv = optUVs.ToArray();

            int vertOffset = 0;

            if (SharedGenerateAtlas)
            {
                combinedMesh.subMeshCount = 1;
                var allIndices = new List<int>();

                foreach (var matGroup in materialGroups)
                    foreach (var data in matGroup.Value)
                    {
                        int[] raw = data.mesh.GetIndices(data.subMeshIndex);
                        for (int i = 0; i < raw.Length; i++)
                            allIndices.Add(vertexRemap[raw[i] + vertOffset]);
                        vertOffset += data.mesh.vertices.Length;
                    }

                combinedMesh.SetIndices(allIndices.ToArray(), MeshTopology.Triangles, 0);
            }
            else
            {
                combinedMesh.subMeshCount = materialGroups.Count;
                int sub = 0;

                foreach (var matGroup in materialGroups)
                {
                    var indices = new List<int>();
                    foreach (var data in matGroup.Value)
                    {
                        int[] raw = data.mesh.GetIndices(data.subMeshIndex);
                        for (int i = 0; i < raw.Length; i++)
                            indices.Add(vertexRemap[raw[i] + vertOffset]);
                        vertOffset += data.mesh.vertices.Length;
                    }
                    combinedMesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, sub++);
                }
            }

            combinedMesh.RecalculateBounds();
            combinedMesh.RecalculateNormals();
            combinedMesh.RecalculateTangents();

            if (optVerts.Count > 65535)
                Debug.LogWarning($"Combined mesh has {optVerts.Count} vertices (> 65535). Unity will automatically use 32-bit index format, which is not supported on some older platforms.");

            AssetDatabase.CreateAsset(combinedMesh, meshPath);
            AssetDatabase.SaveAssets();

            GameObject combined = new GameObject(SharedMeshName);
            Undo.RegisterCreatedObjectUndo(combined, "Combine Meshes");
            combined.transform.SetParent(ParentObject.transform);
            combined.transform.localPosition = Vector3.zero;
            combined.transform.localRotation = Quaternion.identity;
            combined.transform.localScale = Vector3.one;

            combined.AddComponent<MeshFilter>().sharedMesh = combinedMesh;
            combined.AddComponent<MeshRenderer>().sharedMaterials = finalMaterials.ToArray();

            foreach (var filter in meshFilters)
            {
                if (SharedMeshHandling == (int)OriginalMeshHandling.Destroy) Undo.DestroyObjectImmediate(filter.gameObject);
                else if (SharedMeshHandling == (int)OriginalMeshHandling.Deactivate) filter.gameObject.SetActive(false);
            }

            string atlasInfo = SharedGenerateAtlas
                ? "with texture atlas (1 material)"
                : $"({finalMaterials.Count} material{(finalMaterials.Count != 1 ? "s" : "")})";

            string dupeInfo = vertices.Count > optVerts.Count
                ? $"   |   Removed {vertices.Count - optVerts.Count} duplicate vertices ({vertices.Count} → {optVerts.Count})"
                : string.Empty;

            Debug.Log($"Created Mesh: {meshPath}   |   Combined {meshFilters.Length} meshes {atlasInfo}{dupeInfo}");

            EditorUtility.SetDirty(ParentObject);
            ParentObject = null;
        }

        //  Local data types
        class CombineData
        {
            public Mesh mesh;
            public int subMeshIndex;
            public Transform transform;
        }

        struct VertexKey
        {
            Vector3 position;
            Vector3 normal;
            Vector2 uv;

            public VertexKey(Vector3 pos, Vector3 norm, Vector2 uv)
            {
                position = Round(pos);
                normal = Round(norm);
                this.uv = Round(uv);
            }

            static Vector3 Round(Vector3 v) => new Vector3(
                Mathf.Round(v.x * 10000f) / 10000f,
                Mathf.Round(v.y * 10000f) / 10000f,
                Mathf.Round(v.z * 10000f) / 10000f);

            static Vector2 Round(Vector2 v) => new Vector2(
                Mathf.Round(v.x * 10000f) / 10000f,
                Mathf.Round(v.y * 10000f) / 10000f);

            public override bool Equals(object obj)
            {
                if (!(obj is VertexKey)) return false;
                VertexKey o = (VertexKey)obj;
                return position == o.position && normal == o.normal && uv == o.uv;
            }

            public override int GetHashCode() =>
                position.GetHashCode() ^ (normal.GetHashCode() << 2) ^ (uv.GetHashCode() >> 2);
        }

        // Atlas generation
        Material GenerateTextureAtlas(Dictionary<Material, List<CombineData>> materialGroups, string normalizedPath, out Rect[] uvRects)
        {
            List<Texture2D> textureList = new List<Texture2D>();

            foreach (var matGroup in materialGroups)
            {
                Texture2D mainTex = matGroup.Key.mainTexture as Texture2D;
                if (mainTex == null)
                {
                    mainTex = new Texture2D(256, 256);
                    Color[] pixels = Enumerable.Repeat(Color.white, 256 * 256).ToArray();
                    mainTex.SetPixels(pixels);
                    mainTex.Apply();
                }
                else mainTex = DuplicateTexture(mainTex);

                textureList.Add(mainTex);
            }

            Texture2D[] textures = textureList.ToArray();

            int[] sorted = Enumerable.Range(0, textures.Length).ToArray();
            System.Array.Sort(sorted, (a, b) =>
            {
                int da = textures[a].width * textures[a].height;
                int db = textures[b].width * textures[b].height;
                return da != db ? db.CompareTo(da) : textures[b].height.CompareTo(textures[a].height);
            });

            List<AtlasPlacement> placements = new List<AtlasPlacement>();
            List<AtlasSpace> freeSpaces = new List<AtlasSpace>();
            uvRects = new Rect[textures.Length];
            freeSpaces.Add(new AtlasSpace(0, 0, MAX_ATLAS_SIZE, MAX_ATLAS_SIZE));

            int atlasW = 0, atlasH = 0;

            foreach (int i in sorted)
            {
                Texture2D tex = textures[i];
                AtlasSpace bestSpace = null;
                int bestWaste = int.MaxValue, bestScore = int.MaxValue;

                foreach (var space in freeSpaces)
                {
                    if (space.width < tex.width || space.height < tex.height) continue;

                    int waste = (space.width - tex.width) + (space.height - tex.height);
                    int newW = Mathf.Max(atlasW, space.x + tex.width);
                    int newH = Mathf.Max(atlasH, space.y + tex.height);
                    int dW = newW - atlasW, dH = newH - atlasH;

                    int score;
                    if (dW == 0 && dH == 0) score = 0;
                    else if (atlasW < atlasH) score = dH * 1000 + dW;
                    else if (atlasH < atlasW) score = dW * 1000 + dH;
                    else score = dW + dH;

                    if (bestSpace == null || score < bestScore || (score == bestScore && waste < bestWaste))
                    { bestSpace = space; bestWaste = waste; bestScore = score; }
                }

                if (bestSpace == null)
                {
                    Debug.LogError($"Failed to pack texture {tex.width}x{tex.height} into atlas!");
                    uvRects = null;
                    return null;
                }

                int px = bestSpace.x, py = bestSpace.y;
                placements.Add(new AtlasPlacement { texture = tex, originalIndex = i, x = px, y = py });

                atlasW = Mathf.Max(atlasW, px + tex.width);
                atlasH = Mathf.Max(atlasH, py + tex.height);
                freeSpaces.Remove(bestSpace);

                List<AtlasSpace> newSpaces = new List<AtlasSpace>();
                if (bestSpace.width > tex.width) newSpaces.Add(new AtlasSpace(px + tex.width, py, bestSpace.width - tex.width, tex.height));
                if (bestSpace.height > tex.height) newSpaces.Add(new AtlasSpace(px, py + tex.height, tex.width, bestSpace.height - tex.height));
                if (bestSpace.width > tex.width && bestSpace.height > tex.height)
                    newSpaces.Add(new AtlasSpace(px + tex.width, py + tex.height, bestSpace.width - tex.width, bestSpace.height - tex.height));

                foreach (var ns in newSpaces)
                    if (ns.width > 0 && ns.height > 0)
                        AddAndMergeSpace(freeSpaces, ns, placements);
            }

            int potW = Mathf.NextPowerOfTwo(atlasW);
            int potH = Mathf.NextPowerOfTwo(atlasH);
            atlasW = ((potW - atlasW) / (float)atlasW > 0.25f) ? Mathf.Min(atlasW, MAX_ATLAS_SIZE) : Mathf.Min(potW, MAX_ATLAS_SIZE);
            atlasH = ((potH - atlasH) / (float)atlasH > 0.25f) ? Mathf.Min(atlasH, MAX_ATLAS_SIZE) : Mathf.Min(potH, MAX_ATLAS_SIZE);

            if (atlasW > MAX_ATLAS_SIZE || atlasH > MAX_ATLAS_SIZE)
            {
                Debug.LogError($"Atlas too large! Required: {atlasW}x{atlasH}, maximum: {MAX_ATLAS_SIZE}x{MAX_ATLAS_SIZE}");
                uvRects = null;
                return null;
            }

            Texture2D atlas = new Texture2D(atlasW, atlasH, TextureFormat.RGBA32, true);
            atlas.SetPixels(Enumerable.Repeat(Color.clear, atlasW * atlasH).ToArray());

            foreach (var p in placements)
            {
                atlas.SetPixels(p.x, p.y, p.texture.width, p.texture.height, p.texture.GetPixels());
                uvRects[p.originalIndex] = new Rect((float)p.x / atlasW, (float)p.y / atlasH, (float)p.texture.width / atlasW, (float)p.texture.height / atlasH);
            }
            atlas.Apply(true);

            string atlasPath = GetUniquePath(normalizedPath, SharedAtlasName, ".png");
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

            Texture2D savedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

            Material firstMat = materialGroups.Keys.First();
            Material atlasMat = new Material(firstMat.shader);
            if (atlasMat.HasProperty("_BaseMap")) atlasMat.SetTexture("_BaseMap", savedAtlas);
            else if (atlasMat.HasProperty("_MainTex")) atlasMat.SetTexture("_MainTex", savedAtlas);
            else atlasMat.mainTexture = savedAtlas;

            atlasMat.name = SharedAtlasName;
            string matPath = GetUniquePath(normalizedPath, SharedAtlasName, ".mat");
            AssetDatabase.CreateAsset(atlasMat, matPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created Atlas: {atlasPath}   |   Size: {atlasW}x{atlasH}, Packed: {textures.Length} textures");
            return atlasMat;
        }
    }
}
#endif