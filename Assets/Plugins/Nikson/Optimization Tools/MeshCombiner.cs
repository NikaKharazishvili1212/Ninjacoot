#if UNITY_EDITOR
using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.IO;
using System.Linq;

namespace Nikson
{
    class UnifiedCombineData
    {
        public Mesh mesh;
        public int subMeshIndex;
        public Transform transform;
        // Skinned mesh specific (null for regular meshes)
        public Transform[] bones;
        public BoneWeight[] boneWeights;
        public Matrix4x4[] bindPoses;
    }

    public class MeshCombiner : EditorWindow
    {
        const string MESH_NAME = "CombinedMesh";
        const string SKINNED_MESH_NAME = "CombinedSkinnedMesh";
        const string ATLAS_NAME = "Atlas";
        const int MAX_ATLAS_SIZE = 4096;

        GameObject parentObject;
        string savePath = "Nikson/Generated/";
        enum OriginalMeshHandling { Destroy, Deactivate, KeepActive }
        OriginalMeshHandling originalMeshHandling = OriginalMeshHandling.Destroy;
        bool generateAtlas = true;

        [MenuItem("Nikson/Optimization/1. Mesh Combiner")]
        public static void ShowWindow() => GetWindow<MeshCombiner>("Mesh Combiner");

        void OnGUI()
        {
            GUILayout.Label("Mesh Combiner Settings", EditorStyles.boldLabel);

            parentObject = (GameObject)EditorGUILayout.ObjectField("Parent Object", parentObject, typeof(GameObject), true);
            savePath = EditorGUILayout.TextField("Save Path", savePath);
            originalMeshHandling = (OriginalMeshHandling)EditorGUILayout.EnumPopup("Original Meshes", originalMeshHandling);
            generateAtlas = EditorGUILayout.Toggle("Generate Atlas", generateAtlas);
            EditorGUILayout.HelpBox(
                "When enabled: Creates texture atlas with 1 material (fewer draw calls, more memory)\n" +
                "When disabled: Uses existing textures (less memory, multiple materials)",
                MessageType.Info);

            EditorGUILayout.Space();

            GUI.enabled = parentObject != null;
            if (GUILayout.Button("Generate Combined Mesh", GUILayout.Height(30))) Generate();
            GUI.enabled = true;
        }

        void Generate()
        {
            // Detect mesh type
            SkinnedMeshRenderer[] skinnedRenderers = parentObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            MeshFilter[] meshFilters = parentObject.GetComponentsInChildren<MeshFilter>();

            bool isSkinnedMesh = skinnedRenderers.Length > 0;

            if (!isSkinnedMesh && meshFilters.Length == 0)
            {
                Debug.LogError("No renderers found!");
                return;
            }

            if ((isSkinnedMesh && skinnedRenderers.Length == 1) || (!isSkinnedMesh && meshFilters.Length == 1))
            {
                Debug.LogError("You are trying to combine one mesh!");
                return;
            }

            // Collect bone data if skinned (before the loop)
            Transform rootBone = null;
            List<Transform> allBones = new List<Transform>();

            if (isSkinnedMesh)
            {
                rootBone = skinnedRenderers[0].rootBone;
                foreach (var renderer in skinnedRenderers)
                {
                    if (renderer.rootBone != null && rootBone == null) rootBone = renderer.rootBone;
                    foreach (var bone in renderer.bones)
                        if (bone != null && !allBones.Contains(bone))
                            allBones.Add(bone);
                }
            }

            // Group meshes by material (unified for both types)
            Dictionary<Material, List<UnifiedCombineData>> materialGroups = new Dictionary<Material, List<UnifiedCombineData>>();

            if (isSkinnedMesh)
            {
                // Collect from skinned renderers
                foreach (var renderer in skinnedRenderers)
                {
                    Mesh mesh = renderer.sharedMesh;
                    if (mesh == null) continue;

                    for (int i = 0; i < mesh.subMeshCount; i++)
                    {
                        Material mat = renderer.sharedMaterials[i];
                        if (!materialGroups.ContainsKey(mat)) materialGroups[mat] = new List<UnifiedCombineData>();

                        materialGroups[mat].Add(new UnifiedCombineData
                        {
                            mesh = mesh,
                            subMeshIndex = i,
                            transform = renderer.transform,
                            bones = renderer.bones,
                            boneWeights = mesh.boneWeights,
                            bindPoses = mesh.bindposes
                        });
                    }
                }
            }
            else
            {
                // Collect from mesh filters
                MeshRenderer[] renderers = parentObject.GetComponentsInChildren<MeshRenderer>();
                for (int i = 0; i < meshFilters.Length; i++)
                {
                    Mesh mesh = meshFilters[i].sharedMesh;
                    if (mesh == null) continue;

                    Material[] mats = renderers[i].sharedMaterials;
                    for (int j = 0; j < mesh.subMeshCount; j++)
                    {
                        Material mat = j < mats.Length ? mats[j] : null;
                        if (mat == null) continue;

                        if (!materialGroups.ContainsKey(mat)) materialGroups[mat] = new List<UnifiedCombineData>();

                        materialGroups[mat].Add(new UnifiedCombineData
                        {
                            mesh = mesh,
                            subMeshIndex = j,
                            transform = meshFilters[i].transform
                        });
                    }
                }
            }

            // Setup paths and create mesh
            Mesh combinedMesh = new Mesh();

            string normalizedPath = savePath.Replace("\\", "/");
            if (!normalizedPath.StartsWith("Assets/")) normalizedPath = "Assets/" + normalizedPath;
            if (!normalizedPath.EndsWith("/")) normalizedPath += "/";

            if (!Directory.Exists(normalizedPath))
            {
                Directory.CreateDirectory(normalizedPath);
                AssetDatabase.Refresh();
            }

            string meshPath = GetUniquePath(normalizedPath, isSkinnedMesh ? SKINNED_MESH_NAME : MESH_NAME, ".asset");
            combinedMesh.name = Path.GetFileNameWithoutExtension(meshPath);

            // Initialize vertex data lists
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<BoneWeight> boneWeights = new List<BoneWeight>(); // Only used if isSkinnedMesh
            List<Material> finalMaterials = new List<Material>();

            Material atlasMaterial = null;
            Rect[] uvRects = null;

            // Generate texture atlas if enabled
            if (generateAtlas)
            {
                atlasMaterial = GenerateTextureAtlas(materialGroups, normalizedPath, out uvRects);
                finalMaterials.Add(atlasMaterial);
            }

            // Vertex combining loop - process all material groups
            int vertexOffset = 0;
            int matIndex = 0;

            foreach (var matGroup in materialGroups)
            {
                if (!generateAtlas) finalMaterials.Add(matGroup.Key);

                Rect uvRect = generateAtlas ? uvRects[matIndex] : new Rect(0, 0, 1, 1);

                foreach (var data in matGroup.Value)
                {
                    Vector3[] meshVerts = data.mesh.vertices;
                    Vector3[] meshNormals = data.mesh.normals;
                    Vector2[] meshUVs = data.mesh.uv;

                    if (isSkinnedMesh)
                    {
                        // Skinned mesh vertex transformation
                        Matrix4x4 worldToRoot = rootBone.worldToLocalMatrix;

                        for (int i = 0; i < meshVerts.Length; i++)
                        {
                            Vector3 vertex = meshVerts[i];
                            Vector3 normal = meshNormals[i];
                            BoneWeight weight = data.boneWeights[i];

                            Matrix4x4 bm0 = data.bones[weight.boneIndex0].localToWorldMatrix * data.bindPoses[weight.boneIndex0];
                            Matrix4x4 bm1 = data.bones[weight.boneIndex1].localToWorldMatrix * data.bindPoses[weight.boneIndex1];
                            Matrix4x4 bm2 = data.bones[weight.boneIndex2].localToWorldMatrix * data.bindPoses[weight.boneIndex2];
                            Matrix4x4 bm3 = data.bones[weight.boneIndex3].localToWorldMatrix * data.bindPoses[weight.boneIndex3];

                            Vector3 worldVertex = bm0.MultiplyPoint3x4(vertex) * weight.weight0 +
                                                 bm1.MultiplyPoint3x4(vertex) * weight.weight1 +
                                                 bm2.MultiplyPoint3x4(vertex) * weight.weight2 +
                                                 bm3.MultiplyPoint3x4(vertex) * weight.weight3;

                            Vector3 worldNormal = bm0.MultiplyVector(normal) * weight.weight0 +
                                                 bm1.MultiplyVector(normal) * weight.weight1 +
                                                 bm2.MultiplyVector(normal) * weight.weight2 +
                                                 bm3.MultiplyVector(normal) * weight.weight3;

                            vertices.Add(worldToRoot.MultiplyPoint3x4(worldVertex));
                            normals.Add(worldToRoot.MultiplyVector(worldNormal).normalized);

                            Vector2 uv = meshUVs[i];
                            if (generateAtlas)
                            {
                                uv.x = uvRect.x + uv.x * uvRect.width;
                                uv.y = uvRect.y + uv.y * uvRect.height;
                            }
                            uvs.Add(uv);

                            BoneWeight newWeight = new BoneWeight();
                            newWeight.boneIndex0 = allBones.IndexOf(data.bones[weight.boneIndex0]);
                            newWeight.boneIndex1 = allBones.IndexOf(data.bones[weight.boneIndex1]);
                            newWeight.boneIndex2 = allBones.IndexOf(data.bones[weight.boneIndex2]);
                            newWeight.boneIndex3 = allBones.IndexOf(data.bones[weight.boneIndex3]);
                            newWeight.weight0 = weight.weight0;
                            newWeight.weight1 = weight.weight1;
                            newWeight.weight2 = weight.weight2;
                            newWeight.weight3 = weight.weight3;
                            boneWeights.Add(newWeight);
                        }
                    }
                    else
                    {
                        // Regular mesh vertex transformation
                        Matrix4x4 localToWorld = data.transform.localToWorldMatrix;
                        Matrix4x4 worldToLocal = parentObject.transform.worldToLocalMatrix;
                        Matrix4x4 transformMatrix = worldToLocal * localToWorld;

                        for (int i = 0; i < meshVerts.Length; i++)
                        {
                            vertices.Add(transformMatrix.MultiplyPoint3x4(meshVerts[i]));
                            normals.Add(transformMatrix.MultiplyVector(meshNormals[i]).normalized);

                            Vector2 uv = meshUVs[i];
                            if (generateAtlas)
                            {
                                uv.x = uvRect.x + uv.x * uvRect.width;
                                uv.y = uvRect.y + uv.y * uvRect.height;
                            }
                            uvs.Add(uv);
                        }
                    }

                    vertexOffset += meshVerts.Length;
                }
                matIndex++;
            }

            // Vertex welding (optimization) - different for skinned vs regular
            int[] vertexRemap;
            List<Vector3> optimizedVerts;
            List<Vector3> optimizedNormals;
            List<Vector2> optimizedUVs;
            List<BoneWeight> optimizedWeights = new List<BoneWeight>();

            if (isSkinnedMesh)
            {
                Dictionary<SkinnedVertexKey, int> vertexMap = new Dictionary<SkinnedVertexKey, int>();
                optimizedVerts = new List<Vector3>();
                optimizedNormals = new List<Vector3>();
                optimizedUVs = new List<Vector2>();
                vertexRemap = new int[vertices.Count];

                for (int i = 0; i < vertices.Count; i++)
                {
                    SkinnedVertexKey key = new SkinnedVertexKey(vertices[i], normals[i], uvs[i], boneWeights[i]);

                    if (vertexMap.TryGetValue(key, out int existingIndex))
                        vertexRemap[i] = existingIndex;
                    else
                    {
                        int newIndex = optimizedVerts.Count;
                        vertexMap[key] = newIndex;
                        vertexRemap[i] = newIndex;

                        optimizedVerts.Add(vertices[i]);
                        optimizedNormals.Add(normals[i]);
                        optimizedUVs.Add(uvs[i]);
                        optimizedWeights.Add(boneWeights[i]);
                    }
                }
            }
            else
            {
                Dictionary<MeshVertexKey, int> vertexMap = new Dictionary<MeshVertexKey, int>();
                optimizedVerts = new List<Vector3>();
                optimizedNormals = new List<Vector3>();
                optimizedUVs = new List<Vector2>();
                vertexRemap = new int[vertices.Count];

                for (int i = 0; i < vertices.Count; i++)
                {
                    MeshVertexKey key = new MeshVertexKey(vertices[i], normals[i], uvs[i]);

                    if (vertexMap.TryGetValue(key, out int existingIndex))
                        vertexRemap[i] = existingIndex;
                    else
                    {
                        int newIndex = optimizedVerts.Count;
                        vertexMap[key] = newIndex;
                        vertexRemap[i] = newIndex;

                        optimizedVerts.Add(vertices[i]);
                        optimizedNormals.Add(normals[i]);
                        optimizedUVs.Add(uvs[i]);
                    }
                }
            }

            int originalVertCount = vertices.Count;
            int optimizedVertCount = optimizedVerts.Count;

            // Assign vertex data to mesh
            combinedMesh.vertices = optimizedVerts.ToArray();
            combinedMesh.normals = optimizedNormals.ToArray();
            combinedMesh.uv = optimizedUVs.ToArray();

            if (isSkinnedMesh)
                combinedMesh.boneWeights = optimizedWeights.ToArray();

            // Set up submeshes and indices
            if (generateAtlas)
            {
                combinedMesh.subMeshCount = 1;
                List<int> allIndices = new List<int>();
                vertexOffset = 0;

                foreach (var matGroup in materialGroups)
                {
                    foreach (var data in matGroup.Value)
                    {
                        int[] meshIndices = data.mesh.GetIndices(data.subMeshIndex);
                        for (int i = 0; i < meshIndices.Length; i++)
                            allIndices.Add(vertexRemap[meshIndices[i] + vertexOffset]);
                        vertexOffset += data.mesh.vertices.Length;
                    }
                }

                combinedMesh.SetIndices(allIndices.ToArray(), MeshTopology.Triangles, 0);
            }
            else
            {
                combinedMesh.subMeshCount = materialGroups.Count;
                int subMeshIndex = 0;
                vertexOffset = 0;

                foreach (var matGroup in materialGroups)
                {
                    List<int> indices = new List<int>();

                    foreach (var data in matGroup.Value)
                    {
                        int[] meshIndices = data.mesh.GetIndices(data.subMeshIndex);
                        for (int i = 0; i < meshIndices.Length; i++)
                            indices.Add(vertexRemap[meshIndices[i] + vertexOffset]);
                        vertexOffset += data.mesh.vertices.Length;
                    }

                    combinedMesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, subMeshIndex);
                    subMeshIndex++;
                }
            }

            // Bone optimization for skinned meshes
            Transform[] usedBones = null;
            if (isSkinnedMesh)
            {
                bool[] boneUsed = new bool[allBones.Count];
                foreach (var weight in optimizedWeights)
                {
                    if (weight.weight0 > 0) boneUsed[weight.boneIndex0] = true;
                    if (weight.weight1 > 0) boneUsed[weight.boneIndex1] = true;
                    if (weight.weight2 > 0) boneUsed[weight.boneIndex2] = true;
                    if (weight.weight3 > 0) boneUsed[weight.boneIndex3] = true;
                }

                List<Transform> usedBonesList = new List<Transform>();
                int[] boneRemap = new int[allBones.Count];

                for (int i = 0; i < allBones.Count; i++)
                {
                    if (boneUsed[i])
                    {
                        boneRemap[i] = usedBonesList.Count;
                        usedBonesList.Add(allBones[i]);
                    }
                }

                BoneWeight[] remappedWeights = new BoneWeight[optimizedWeights.Count];
                for (int i = 0; i < optimizedWeights.Count; i++)
                {
                    BoneWeight w = optimizedWeights[i];
                    w.boneIndex0 = boneRemap[w.boneIndex0];
                    w.boneIndex1 = boneRemap[w.boneIndex1];
                    w.boneIndex2 = boneRemap[w.boneIndex2];
                    w.boneIndex3 = boneRemap[w.boneIndex3];
                    remappedWeights[i] = w;
                }

                combinedMesh.boneWeights = remappedWeights;

                Matrix4x4[] newBindPoses = new Matrix4x4[usedBonesList.Count];
                for (int i = 0; i < usedBonesList.Count; i++)
                {
                    newBindPoses[i] = rootBone.worldToLocalMatrix * usedBonesList[i].localToWorldMatrix;
                    newBindPoses[i] = newBindPoses[i].inverse;
                }
                combinedMesh.bindposes = newBindPoses;

                usedBones = usedBonesList.ToArray();
            }

            // Finalize mesh
            combinedMesh.RecalculateBounds();
            if (!isSkinnedMesh)
            {
                combinedMesh.RecalculateNormals();
                combinedMesh.RecalculateTangents();
            }

            AssetDatabase.CreateAsset(combinedMesh, meshPath);
            AssetDatabase.SaveAssets();

            // Create combined GameObject
            GameObject combined = new GameObject(isSkinnedMesh ? SKINNED_MESH_NAME : MESH_NAME);
            combined.transform.SetParent(parentObject.transform);
            combined.transform.localPosition = Vector3.zero;
            combined.transform.localRotation = Quaternion.identity;
            combined.transform.localScale = Vector3.one;

            if (isSkinnedMesh)
            {
                SkinnedMeshRenderer newRenderer = combined.AddComponent<SkinnedMeshRenderer>();
                newRenderer.sharedMesh = combinedMesh;
                newRenderer.bones = usedBones;
                newRenderer.rootBone = rootBone;
                newRenderer.sharedMaterials = finalMaterials.ToArray();

                // Handle original meshes
                foreach (var renderer in skinnedRenderers)
                {
                    if (originalMeshHandling == OriginalMeshHandling.Destroy) DestroyImmediate(renderer.gameObject);
                    else if (originalMeshHandling == OriginalMeshHandling.Deactivate) renderer.gameObject.SetActive(false);
                }

                string atlasInfo = generateAtlas ? "with texture atlas (1 material)" : $"({finalMaterials.Count} materials)";
                string removedDuplicatevertices = originalVertCount > optimizedVertCount ? $"   |   Removed duplicate vertices ({originalVertCount}→{optimizedVertCount}) and {allBones.Count - usedBones.Length} unused bones" : string.Empty;
                Debug.Log($"Created Mesh: {meshPath}   |   Combined {skinnedRenderers.Length} meshes {atlasInfo}{removedDuplicatevertices}");
            }
            else
            {
                MeshFilter newFilter = combined.AddComponent<MeshFilter>();
                MeshRenderer newRenderer = combined.AddComponent<MeshRenderer>();
                newFilter.sharedMesh = combinedMesh;
                newRenderer.sharedMaterials = finalMaterials.ToArray();

                // Handle original meshes
                foreach (var filter in meshFilters)
                {
                    if (originalMeshHandling == OriginalMeshHandling.Destroy) DestroyImmediate(filter.gameObject);
                    else if (originalMeshHandling == OriginalMeshHandling.Deactivate) filter.gameObject.SetActive(false);
                }

                string atlasInfo = generateAtlas ? "with texture atlas (1 material)" : $"({finalMaterials.Count} materials)";
                string removedDuplicatevertices = originalVertCount > optimizedVertCount ? $"   |   Removed duplicate vertices ({originalVertCount}→{optimizedVertCount})" : string.Empty;
                Debug.Log($"Created Mesh: {meshPath}   |   Combined {meshFilters.Length} meshes {atlasInfo}{removedDuplicatevertices}");
            }

            EditorUtility.SetDirty(parentObject);
            parentObject = null; // Reset UI field to prevent accidental regeneration
        }

        Material GenerateTextureAtlas(Dictionary<Material, List<UnifiedCombineData>> materialGroups, string normalizedPath, out Rect[] uvRects)
        {
            // Collect textures and create a mapping to original materials
            List<Texture2D> textureList = new List<Texture2D>();
            Dictionary<Material, int> materialToTextureIndex = new Dictionary<Material, int>();
            int textureIndex = 0;

            foreach (var matGroup in materialGroups)
            {
                Texture2D mainTex = matGroup.Key.mainTexture as Texture2D;
                if (mainTex == null)
                {
                    // Create white texture if material has no texture
                    mainTex = new Texture2D(256, 256);
                    Color[] pixels = new Color[256 * 256];
                    for (int i = 0; i < pixels.Length; i++)
                        pixels[i] = Color.white;
                    mainTex.SetPixels(pixels);
                    mainTex.Apply();
                }
                else mainTex = DuplicateTexture(mainTex); // Make texture readable for atlas packing

                materialToTextureIndex[matGroup.Key] = textureIndex;
                textureList.Add(mainTex);
                textureIndex++;
            }

            Texture2D[] texturesToPack = textureList.ToArray();

            // Sort textures by area (largest first)
            int[] sortedIndices = new int[texturesToPack.Length];
            for (int i = 0; i < sortedIndices.Length; i++)
                sortedIndices[i] = i;
            System.Array.Sort(sortedIndices, (a, b) =>
            {
                int areaA = texturesToPack[a].width * texturesToPack[a].height;
                int areaB = texturesToPack[b].width * texturesToPack[b].height;
                if (areaA != areaB) return areaB.CompareTo(areaA);
                return texturesToPack[b].height.CompareTo(texturesToPack[a].height);
            });

            // Advanced space-tracking bin packing
            List<TexturePlacement> placements = new List<TexturePlacement>();
            List<FreeSpace> freeSpaces = new List<FreeSpace>();
            uvRects = new Rect[texturesToPack.Length];

            // Start with one large free space
            freeSpaces.Add(new FreeSpace(0, 0, MAX_ATLAS_SIZE, MAX_ATLAS_SIZE));

            int atlasWidth = 0;
            int atlasHeight = 0;

            // Pack each texture
            for (int idx = 0; idx < sortedIndices.Length; idx++)
            {
                int i = sortedIndices[idx];
                Texture2D texture = texturesToPack[i];

                // Find the best free space
                FreeSpace bestSpace = null;
                int bestWaste = int.MaxValue;
                int bestScore = int.MaxValue;

                foreach (var space in freeSpaces)
                {
                    if (space.width >= texture.width && space.height >= texture.height)
                    {
                        int waste = (space.width - texture.width) + (space.height - texture.height);

                        // Calculate what the new atlas dimensions would be
                        int newWidth = Mathf.Max(atlasWidth, space.x + texture.width);
                        int newHeight = Mathf.Max(atlasHeight, space.y + texture.height);

                        // Calculate expansion in each dimension
                        int widthExpansion = newWidth - atlasWidth;
                        int heightExpansion = newHeight - atlasHeight;

                        // Score based on balanced growth
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
                    Debug.LogError($"Failed to pack texture {texture.width}x{texture.height} into atlas!");
                    uvRects = null;
                    return null;
                }

                // Place the texture
                int placeX = bestSpace.x;
                int placeY = bestSpace.y;

                placements.Add(new TexturePlacement
                {
                    texture = texture,
                    originalIndex = i,
                    x = placeX,
                    y = placeY
                });

                // Update atlas dimensions
                atlasWidth = Mathf.Max(atlasWidth, placeX + texture.width);
                atlasHeight = Mathf.Max(atlasHeight, placeY + texture.height);

                // Remove the used space
                freeSpaces.Remove(bestSpace);

                // Create new free spaces from the leftover areas
                List<FreeSpace> newSpaces = new List<FreeSpace>();

                // Space to the right
                if (bestSpace.width > texture.width)
                {
                    newSpaces.Add(new FreeSpace(
                        placeX + texture.width,
                        placeY,
                        bestSpace.width - texture.width,
                        texture.height
                    ));
                }

                // Space above
                if (bestSpace.height > texture.height)
                {
                    newSpaces.Add(new FreeSpace(
                        placeX,
                        placeY + texture.height,
                        texture.width,
                        bestSpace.height - texture.height
                    ));
                }

                // Corner space (top-right)
                if (bestSpace.width > texture.width && bestSpace.height > texture.height)
                {
                    newSpaces.Add(new FreeSpace(
                        placeX + texture.width,
                        placeY + texture.height,
                        bestSpace.width - texture.width,
                        bestSpace.height - texture.height
                    ));
                }

                // Add new spaces and merge overlapping ones
                foreach (var newSpace in newSpaces)
                    if (newSpace.width > 0 && newSpace.height > 0)
                        AddAndMergeSpace(freeSpaces, newSpace, placements);
            }

            // Only use power-of-two if it doesn't waste too much space
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
                Debug.LogError($"Not enough space in atlas! Required size: {atlasWidth}x{atlasHeight}. Maximum allowed: {MAX_ATLAS_SIZE}x{MAX_ATLAS_SIZE}");
                uvRects = null;
                return null;
            }

            // Create final atlas texture
            Texture2D atlasTexture = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, true);
            Color[] clearColors = new Color[atlasWidth * atlasHeight];
            for (int i = 0; i < clearColors.Length; i++)
                clearColors[i] = Color.clear;
            atlasTexture.SetPixels(clearColors);

            // Pack all textures into the atlas
            foreach (var placement in placements)
            {
                atlasTexture.SetPixels(placement.x, placement.y, placement.texture.width, placement.texture.height, placement.texture.GetPixels());
                uvRects[placement.originalIndex] = new Rect(
                    (float)placement.x / atlasWidth,
                    (float)placement.y / atlasHeight,
                    (float)placement.texture.width / atlasWidth,
                    (float)placement.texture.height / atlasHeight
                );
            }

            atlasTexture.Apply(true);

            // Save atlas texture
            string atlasPath = GetUniquePath(normalizedPath, ATLAS_NAME, ".png");
            byte[] pngData = atlasTexture.EncodeToPNG();
            File.WriteAllBytes(atlasPath, pngData);
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

            // Use shader from first material
            Material firstMaterial = materialGroups.Keys.First();
            Material atlasMat = new Material(firstMaterial.shader);

            // Try to find the correct texture property name for this shader
            if (atlasMat.HasProperty("_BaseMap")) atlasMat.SetTexture("_BaseMap", savedAtlas); // URP
            else if (atlasMat.HasProperty("_MainTex")) atlasMat.SetTexture("_MainTex", savedAtlas); // Standard/Built-in
            else atlasMat.mainTexture = savedAtlas; // Fallback

            atlasMat.name = "Atlas";
            string matPath = GetUniquePath(normalizedPath, atlasMat.name, ".mat");
            AssetDatabase.CreateAsset(atlasMat, matPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created Atlas: {atlasPath}   |   Size: {atlasWidth}x{atlasHeight}, Packed: {texturesToPack.Length}");

            return atlasMat;
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
            public int originalIndex;
            public int x;
            public int y;
        }

        Texture2D DuplicateTexture(Texture2D source)
        {
            RenderTexture renderTex = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, renderTex);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTex;

            Texture2D readableTexture = new Texture2D(source.width, source.height);
            readableTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
            readableTexture.Apply();

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTex);
            return readableTexture;
        }

        string GetUniquePath(string folder, string name, string ext)
        {
            string path = Path.Combine(folder, name + ext);
            int index = 1;
            while (File.Exists(path) || AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                path = Path.Combine(folder, name + index++ + ext);
            return path;
        }

        struct MeshVertexKey
        {
            Vector3 position;
            Vector3 normal;
            Vector2 uv;

            public MeshVertexKey(Vector3 pos, Vector3 norm, Vector2 uv)
            {
                position = new Vector3(
                    Mathf.Round(pos.x * 10000f) / 10000f,
                    Mathf.Round(pos.y * 10000f) / 10000f,
                    Mathf.Round(pos.z * 10000f) / 10000f
                );
                normal = new Vector3(
                    Mathf.Round(norm.x * 10000f) / 10000f,
                    Mathf.Round(norm.y * 10000f) / 10000f,
                    Mathf.Round(norm.z * 10000f) / 10000f
                );
                this.uv = new Vector2(
                    Mathf.Round(uv.x * 10000f) / 10000f,
                    Mathf.Round(uv.y * 10000f) / 10000f
                );
            }

            public override bool Equals(object obj)
            {
                if (!(obj is MeshVertexKey)) return false;
                MeshVertexKey other = (MeshVertexKey)obj;
                return position == other.position && normal == other.normal && uv == other.uv;
            }

            public override int GetHashCode() => position.GetHashCode() ^ (normal.GetHashCode() << 2) ^ (uv.GetHashCode() >> 2);
        }

        struct SkinnedVertexKey
        {
            Vector3 position;
            Vector3 normal;
            Vector2 uv;
            int bone0, bone1, bone2, bone3;
            float weight0, weight1, weight2, weight3;

            public SkinnedVertexKey(Vector3 pos, Vector3 norm, Vector2 uv, BoneWeight weight)
            {
                position = new Vector3(
                    Mathf.Round(pos.x * 10000f) / 10000f,
                    Mathf.Round(pos.y * 10000f) / 10000f,
                    Mathf.Round(pos.z * 10000f) / 10000f
                );
                normal = new Vector3(
                    Mathf.Round(norm.x * 10000f) / 10000f,
                    Mathf.Round(norm.y * 10000f) / 10000f,
                    Mathf.Round(norm.z * 10000f) / 10000f
                );
                this.uv = new Vector2(
                    Mathf.Round(uv.x * 10000f) / 10000f,
                    Mathf.Round(uv.y * 10000f) / 10000f
                );
                bone0 = weight.boneIndex0;
                bone1 = weight.boneIndex1;
                bone2 = weight.boneIndex2;
                bone3 = weight.boneIndex3;
                weight0 = weight.weight0;
                weight1 = weight.weight1;
                weight2 = weight.weight2;
                weight3 = weight.weight3;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is SkinnedVertexKey)) return false;
                SkinnedVertexKey other = (SkinnedVertexKey)obj;
                return position == other.position && normal == other.normal && uv == other.uv &&
                       bone0 == other.bone0 && bone1 == other.bone1 && bone2 == other.bone2 && bone3 == other.bone3 &&
                       weight0 == other.weight0 && weight1 == other.weight1 && weight2 == other.weight2 && weight3 == other.weight3;
            }

            public override int GetHashCode() => position.GetHashCode() ^ (normal.GetHashCode() << 2) ^ (uv.GetHashCode() >> 2) ^ (bone0 << 24) ^ (bone1 << 16) ^ (bone2 << 8) ^ bone3;
        }
    }
}
#endif