#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace Nikson
{
    public class SkinnedMeshCombiner : MeshCombinerBase
    {
        const string SKINNED_MESH_NAME = "CombinedSkinnedMesh";

        const string PREF_SAVE_PATH = "Nikson_SkinnedMeshCombiner_SavePath";
        const string PREF_ATLAS_NAME = "Nikson_SkinnedMeshCombiner_AtlasName";
        const string PREF_MESH_NAME = "Nikson_SkinnedMeshCombiner_MeshName";
        const string PREF_MESH_HANDLING = "Nikson_SkinnedMeshCombiner_MeshHandling";
        const string PREF_GENERATE_ATLAS = "Nikson_SkinnedMeshCombiner_GenerateAtlas";

        GameObject parentObject;
        string savePath;
        string atlasName;
        string meshName;
        enum OriginalMeshHandling { Destroy, Deactivate, KeepActive }
        OriginalMeshHandling originalMeshHandling;
        bool generateAtlas;

        [MenuItem("Tools/Nikson/Optimization/2. Skinned Mesh Combiner")]
        public static void ShowWindow() => GetWindow<SkinnedMeshCombiner>("Skinned Mesh Combiner");

        void OnEnable()
        {
            savePath = EditorPrefs.GetString(PREF_SAVE_PATH, "Assets/Nikson/Optimization/Generated/");
            atlasName = EditorPrefs.GetString(PREF_ATLAS_NAME, ATLAS_NAME);
            meshName = EditorPrefs.GetString(PREF_MESH_NAME, SKINNED_MESH_NAME);
            originalMeshHandling = (OriginalMeshHandling)EditorPrefs.GetInt(PREF_MESH_HANDLING, (int)OriginalMeshHandling.Destroy);
            generateAtlas = EditorPrefs.GetBool(PREF_GENERATE_ATLAS, true);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "\nSelect the parent GameObject of the skinned meshes you want to combine, " +
                "choose the desired settings, and click \"Generate\".\n\n" +
                "If a file with the chosen name already exists, a number will be appended automatically (e.g. Atlas1, Atlas2).\n",
                MessageType.Info);

            parentObject = (GameObject)EditorGUILayout.ObjectField("Parent Object", parentObject, typeof(GameObject), true);

            EditorGUI.BeginChangeCheck();

            // Browse save path
            EditorGUILayout.BeginHorizontal();
            savePath = EditorGUILayout.TextField("Save Path", savePath);
            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string selected = EditorUtility.OpenFolderPanel("Select Save Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    if (selected.StartsWith(Application.dataPath))
                        savePath = "Assets" + selected.Substring(Application.dataPath.Length);
                    else
                        Debug.LogWarning("Selected folder must be inside the project's Assets folder.");
                }
            }
            EditorGUILayout.EndHorizontal();

            if (generateAtlas) atlasName = EditorGUILayout.TextField("Atlas Name", atlasName);
            meshName = EditorGUILayout.TextField("Mesh Name", meshName);
            generateAtlas = EditorGUILayout.Toggle("Generate Atlas", generateAtlas);
            originalMeshHandling = (OriginalMeshHandling)EditorGUILayout.EnumPopup("Original Meshes", originalMeshHandling);

            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PREF_SAVE_PATH, savePath);
                EditorPrefs.SetString(PREF_MESH_NAME, meshName);
                EditorPrefs.SetBool(PREF_GENERATE_ATLAS, generateAtlas);
                EditorPrefs.SetString(PREF_ATLAS_NAME, atlasName);
                EditorPrefs.SetInt(PREF_MESH_HANDLING, (int)originalMeshHandling);
            }

            EditorGUILayout.Space();

            GUI.enabled = parentObject != null;
            if (GUILayout.Button("Generate", GUILayout.Height(30))) Generate();
            GUI.enabled = true;
        }

        void Generate()
        {
            SkinnedMeshRenderer[] skinnedRenderers = parentObject.GetComponentsInChildren<SkinnedMeshRenderer>();

            if (skinnedRenderers.Length == 0)
            {
                Debug.LogError("No SkinnedMeshRenderers found under the selected object!");
                return;
            }

            if (skinnedRenderers.Length == 1)
            {
                Debug.LogError("Only one skinned mesh found — nothing to combine!");
                return;
            }

            // ------------------------------------------------------------------
            //  Collect all unique bones across all renderers
            //  NOTE: rootBone is taken from the first renderer that has one.
            //        It is intentionally NOT overwritten after being set.
            // ------------------------------------------------------------------
            Transform rootBone = null;
            var allBones = new List<Transform>();

            foreach (var renderer in skinnedRenderers)
            {
                if (rootBone == null && renderer.rootBone != null)
                    rootBone = renderer.rootBone;

                foreach (var bone in renderer.bones)
                    if (bone != null && !allBones.Contains(bone))
                        allBones.Add(bone);
            }

            if (rootBone == null)
            {
                Debug.LogError("No root bone found on any SkinnedMeshRenderer. Cannot combine.");
                return;
            }

            // ------------------------------------------------------------------
            //  Group sub-meshes by material
            // ------------------------------------------------------------------
            var materialGroups = new Dictionary<Material, List<UnifiedCombineData>>();

            foreach (var renderer in skinnedRenderers)
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    Material mat = i < renderer.sharedMaterials.Length ? renderer.sharedMaterials[i] : null;
                    if (mat == null) continue;

                    if (!materialGroups.ContainsKey(mat))
                        materialGroups[mat] = new List<UnifiedCombineData>();

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

            // ------------------------------------------------------------------
            //  Set up output paths
            // ------------------------------------------------------------------
            string normalizedPath = NormalizePath(savePath);
            EnsureDirectory(normalizedPath);

            string meshPath = GetUniquePath(normalizedPath, meshName, ".asset");
            Mesh combinedMesh = new Mesh { name = Path.GetFileNameWithoutExtension(meshPath) };
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            // ------------------------------------------------------------------
            //  Collect vertex data
            // ------------------------------------------------------------------
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var boneWeights = new List<BoneWeight>();

            List<Material> finalMaterials = new List<Material>();
            Rect[] uvRects = null;
            Material atlasMaterial = null;

            if (generateAtlas)
            {
                atlasMaterial = GenerateTextureAtlas(materialGroups, normalizedPath, atlasName, out uvRects);
                if (atlasMaterial == null) return;
                finalMaterials.Add(atlasMaterial);
            }

            Matrix4x4 worldToRoot = rootBone.worldToLocalMatrix;
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

                    for (int i = 0; i < meshVerts.Length; i++)
                    {
                        BoneWeight weight = data.boneWeights[i];

                        // Blend vertex position and normal through the bone matrices
                        Matrix4x4 bm0 = data.bones[weight.boneIndex0].localToWorldMatrix * data.bindPoses[weight.boneIndex0];
                        Matrix4x4 bm1 = data.bones[weight.boneIndex1].localToWorldMatrix * data.bindPoses[weight.boneIndex1];
                        Matrix4x4 bm2 = data.bones[weight.boneIndex2].localToWorldMatrix * data.bindPoses[weight.boneIndex2];
                        Matrix4x4 bm3 = data.bones[weight.boneIndex3].localToWorldMatrix * data.bindPoses[weight.boneIndex3];

                        Vector3 worldVert =
                            bm0.MultiplyPoint3x4(meshVerts[i]) * weight.weight0 +
                            bm1.MultiplyPoint3x4(meshVerts[i]) * weight.weight1 +
                            bm2.MultiplyPoint3x4(meshVerts[i]) * weight.weight2 +
                            bm3.MultiplyPoint3x4(meshVerts[i]) * weight.weight3;

                        Vector3 worldNorm =
                            bm0.MultiplyVector(meshNormals[i]) * weight.weight0 +
                            bm1.MultiplyVector(meshNormals[i]) * weight.weight1 +
                            bm2.MultiplyVector(meshNormals[i]) * weight.weight2 +
                            bm3.MultiplyVector(meshNormals[i]) * weight.weight3;

                        vertices.Add(worldToRoot.MultiplyPoint3x4(worldVert));
                        normals.Add(worldToRoot.MultiplyVector(worldNorm).normalized);

                        Vector2 uv = (meshUVs != null && i < meshUVs.Length) ? meshUVs[i] : Vector2.zero;
                        if (generateAtlas)
                        {
                            uv.x = uvRect.x + uv.x * uvRect.width;
                            uv.y = uvRect.y + uv.y * uvRect.height;
                        }
                        uvs.Add(uv);

                        // Remap bone indices to the unified bone list
                        boneWeights.Add(new BoneWeight
                        {
                            boneIndex0 = allBones.IndexOf(data.bones[weight.boneIndex0]),
                            boneIndex1 = allBones.IndexOf(data.bones[weight.boneIndex1]),
                            boneIndex2 = allBones.IndexOf(data.bones[weight.boneIndex2]),
                            boneIndex3 = allBones.IndexOf(data.bones[weight.boneIndex3]),
                            weight0 = weight.weight0,
                            weight1 = weight.weight1,
                            weight2 = weight.weight2,
                            weight3 = weight.weight3,
                        });
                    }
                }
                matIndex++;
            }

            // ------------------------------------------------------------------
            //  Vertex welding — remove duplicates
            // ------------------------------------------------------------------
            var vertexMap = new Dictionary<SkinnedVertexKey, int>();
            var optVerts = new List<Vector3>();
            var optNormals = new List<Vector3>();
            var optUVs = new List<Vector2>();
            var optWeights = new List<BoneWeight>();
            int[] vertexRemap = new int[vertices.Count];

            for (int i = 0; i < vertices.Count; i++)
            {
                var key = new SkinnedVertexKey(vertices[i], normals[i], uvs[i], boneWeights[i]);
                if (vertexMap.TryGetValue(key, out int existing))
                {
                    vertexRemap[i] = existing;
                }
                else
                {
                    int newIdx = optVerts.Count;
                    vertexMap[key] = newIdx;
                    vertexRemap[i] = newIdx;
                    optVerts.Add(vertices[i]);
                    optNormals.Add(normals[i]);
                    optUVs.Add(uvs[i]);
                    optWeights.Add(boneWeights[i]);
                }
            }

            // ------------------------------------------------------------------
            //  Assign vertex data
            // ------------------------------------------------------------------
            combinedMesh.vertices = optVerts.ToArray();
            combinedMesh.normals = optNormals.ToArray();
            combinedMesh.uv = optUVs.ToArray();
            combinedMesh.boneWeights = optWeights.ToArray();

            // ------------------------------------------------------------------
            //  Build index buffers
            // ------------------------------------------------------------------
            int vertOffset = 0;

            if (generateAtlas)
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

            // ------------------------------------------------------------------
            //  Bone optimization — strip unused bones & remap weights
            // ------------------------------------------------------------------
            bool[] boneUsed = new bool[allBones.Count];
            foreach (var w in optWeights)
            {
                if (w.weight0 > 0) boneUsed[w.boneIndex0] = true;
                if (w.weight1 > 0) boneUsed[w.boneIndex1] = true;
                if (w.weight2 > 0) boneUsed[w.boneIndex2] = true;
                if (w.weight3 > 0) boneUsed[w.boneIndex3] = true;
            }

            var usedBonesList = new List<Transform>();
            int[] boneRemap = new int[allBones.Count];

            for (int i = 0; i < allBones.Count; i++)
            {
                if (!boneUsed[i]) continue;
                boneRemap[i] = usedBonesList.Count;
                usedBonesList.Add(allBones[i]);
            }

            BoneWeight[] remappedWeights = new BoneWeight[optWeights.Count];
            for (int i = 0; i < optWeights.Count; i++)
            {
                BoneWeight w = optWeights[i];
                w.boneIndex0 = boneRemap[w.boneIndex0];
                w.boneIndex1 = boneRemap[w.boneIndex1];
                w.boneIndex2 = boneRemap[w.boneIndex2];
                w.boneIndex3 = boneRemap[w.boneIndex3];
                remappedWeights[i] = w;
            }

            combinedMesh.boneWeights = remappedWeights;

            // Rebuild bind poses for the used bones
            Matrix4x4[] newBindPoses = new Matrix4x4[usedBonesList.Count];
            for (int i = 0; i < usedBonesList.Count; i++)
                newBindPoses[i] = (rootBone.worldToLocalMatrix * usedBonesList[i].localToWorldMatrix).inverse;
            combinedMesh.bindposes = newBindPoses;

            Transform[] usedBones = usedBonesList.ToArray();

            // ------------------------------------------------------------------
            //  Finalize & save mesh
            // ------------------------------------------------------------------
            combinedMesh.RecalculateBounds();
            // Note: normals & tangents are intentionally NOT recalculated for skinned meshes
            // because they must stay consistent with the bind pose, not the world-space snapshot.

            WarnIfLargeMesh(optVerts.Count);

            AssetDatabase.CreateAsset(combinedMesh, meshPath);
            AssetDatabase.SaveAssets();

            // ------------------------------------------------------------------
            //  Create the combined GameObject
            // ------------------------------------------------------------------
            GameObject combined = new GameObject(SKINNED_MESH_NAME);
            Undo.RegisterCreatedObjectUndo(combined, "Combine Skinned Meshes");
            combined.transform.SetParent(parentObject.transform);
            combined.transform.localPosition = Vector3.zero;
            combined.transform.localRotation = Quaternion.identity;
            combined.transform.localScale = Vector3.one;

            var newRenderer = combined.AddComponent<SkinnedMeshRenderer>();
            newRenderer.sharedMesh = combinedMesh;
            newRenderer.bones = usedBones;
            newRenderer.rootBone = rootBone;
            newRenderer.sharedMaterials = finalMaterials.ToArray();

            // Handle original meshes
            foreach (var renderer in skinnedRenderers)
            {
                if (originalMeshHandling == OriginalMeshHandling.Destroy) Undo.DestroyObjectImmediate(renderer.gameObject);
                else if (originalMeshHandling == OriginalMeshHandling.Deactivate) renderer.gameObject.SetActive(false);
            }

            string atlasInfo = generateAtlas
                ? "with texture atlas (1 material)"
                : $"({finalMaterials.Count} material{(finalMaterials.Count != 1 ? "s" : "")})";

            string dupeInfo = vertices.Count > optVerts.Count
                ? $"   |   Removed {vertices.Count - optVerts.Count} duplicate vertices ({vertices.Count} → {optVerts.Count})" +
                  $" and {allBones.Count - usedBones.Length} unused bones"
                : string.Empty;

            Debug.Log($"Created Mesh: {meshPath}   |   Combined {skinnedRenderers.Length} skinned meshes {atlasInfo}{dupeInfo}");

            EditorUtility.SetDirty(parentObject);
            parentObject = null; // Reset to prevent accidental re-generation
        }
    }
}
#endif