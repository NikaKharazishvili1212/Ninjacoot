#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace Nikson
{
    public class MeshCombiner : MeshCombinerBase
    {
        const string MESH_NAME = "CombinedMesh";

        const string PREF_SAVE_PATH = "Nikson_MeshCombiner_SavePath";
        const string PREF_ATLAS_NAME = "Nikson_MeshCombiner_AtlasName";
        const string PREF_MESH_NAME = "Nikson_MeshCombiner_MeshName";
        const string PREF_GENERATE_ATLAS = "Nikson_MeshCombiner_GenerateAtlas";
        const string PREF_MESH_HANDLING = "Nikson_MeshCombiner_MeshHandling";

        GameObject parentObject;

        string savePath;
        string atlasName;
        string meshName;
        enum OriginalMeshHandling { Destroy, Deactivate, KeepActive }
        OriginalMeshHandling originalMeshHandling;
        bool generateAtlas;

        [MenuItem("Tools/Nikson/Optimization/1. Mesh Combiner")]
        public static void ShowWindow() => GetWindow<MeshCombiner>("Mesh Combiner");

        void OnEnable()
        {
            savePath = EditorPrefs.GetString(PREF_SAVE_PATH, "Assets/Nikson/Optimization/Generated/");
            atlasName = EditorPrefs.GetString(PREF_ATLAS_NAME, ATLAS_NAME);
            meshName = EditorPrefs.GetString(PREF_MESH_NAME, MESH_NAME);
            generateAtlas = EditorPrefs.GetBool(PREF_GENERATE_ATLAS, true);
            originalMeshHandling = (OriginalMeshHandling)EditorPrefs.GetInt(PREF_MESH_HANDLING, (int)OriginalMeshHandling.Destroy);
        }

        void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "\nSelect the parent GameObject of the meshes you want to combine, " +
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
            MeshFilter[] meshFilters = parentObject.GetComponentsInChildren<MeshFilter>();

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

            // ------------------------------------------------------------------
            //  Group sub-meshes by material
            // ------------------------------------------------------------------
            MeshRenderer[] renderers = parentObject.GetComponentsInChildren<MeshRenderer>();
            var materialGroups = new Dictionary<Material, List<UnifiedCombineData>>();

            for (int i = 0; i < meshFilters.Length; i++)
            {
                Mesh mesh = meshFilters[i].sharedMesh;
                if (mesh == null) continue;

                Material[] mats = renderers[i].sharedMaterials;
                for (int j = 0; j < mesh.subMeshCount; j++)
                {
                    Material mat = j < mats.Length ? mats[j] : null;
                    if (mat == null) continue;

                    if (!materialGroups.ContainsKey(mat))
                        materialGroups[mat] = new List<UnifiedCombineData>();

                    materialGroups[mat].Add(new UnifiedCombineData
                    {
                        mesh = mesh,
                        subMeshIndex = j,
                        transform = meshFilters[i].transform
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
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Avoids silent truncation on large meshes

            // ------------------------------------------------------------------
            //  Collect vertex data
            // ------------------------------------------------------------------
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();

            List<Material> finalMaterials = new List<Material>();
            Rect[] uvRects = null;
            Material atlasMaterial = null;

            if (generateAtlas)
            {
                atlasMaterial = GenerateTextureAtlas(materialGroups, normalizedPath, atlasName, out uvRects);
                if (atlasMaterial == null) return; // Atlas generation failed
                finalMaterials.Add(atlasMaterial);
            }

            int matIndex = 0;
            foreach (var matGroup in materialGroups)
            {
                if (!generateAtlas) finalMaterials.Add(matGroup.Key);

                Rect uvRect = generateAtlas ? uvRects[matIndex] : new Rect(0, 0, 1, 1);
                Matrix4x4 worldToParent = parentObject.transform.worldToLocalMatrix;

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
                        if (generateAtlas)
                        {
                            uv.x = uvRect.x + uv.x * uvRect.width;
                            uv.y = uvRect.y + uv.y * uvRect.height;
                        }
                        uvs.Add(uv);
                    }
                }
                matIndex++;
            }

            // ------------------------------------------------------------------
            //  Vertex welding — remove duplicates
            // ------------------------------------------------------------------
            var vertexMap = new Dictionary<MeshVertexKey, int>();
            var optVerts = new List<Vector3>();
            var optNormals = new List<Vector3>();
            var optUVs = new List<Vector2>();
            int[] vertexRemap = new int[vertices.Count];

            for (int i = 0; i < vertices.Count; i++)
            {
                var key = new MeshVertexKey(vertices[i], normals[i], uvs[i]);
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
                }
            }

            // ------------------------------------------------------------------
            //  Assign vertex data
            // ------------------------------------------------------------------
            combinedMesh.vertices = optVerts.ToArray();
            combinedMesh.normals = optNormals.ToArray();
            combinedMesh.uv = optUVs.ToArray();

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
            //  Finalize & save mesh
            // ------------------------------------------------------------------
            combinedMesh.RecalculateBounds();
            combinedMesh.RecalculateNormals();
            combinedMesh.RecalculateTangents();

            WarnIfLargeMesh(optVerts.Count);

            AssetDatabase.CreateAsset(combinedMesh, meshPath);
            AssetDatabase.SaveAssets();

            // ------------------------------------------------------------------
            //  Create the combined GameObject
            // ------------------------------------------------------------------
            GameObject combined = new GameObject(MESH_NAME);
            Undo.RegisterCreatedObjectUndo(combined, "Combine Meshes");
            combined.transform.SetParent(parentObject.transform);
            combined.transform.localPosition = Vector3.zero;
            combined.transform.localRotation = Quaternion.identity;
            combined.transform.localScale = Vector3.one;

            combined.AddComponent<MeshFilter>().sharedMesh = combinedMesh;
            combined.AddComponent<MeshRenderer>().sharedMaterials = finalMaterials.ToArray();

            // Handle original meshes
            foreach (var filter in meshFilters)
            {
                if (originalMeshHandling == OriginalMeshHandling.Destroy) Undo.DestroyObjectImmediate(filter.gameObject);
                else if (originalMeshHandling == OriginalMeshHandling.Deactivate) filter.gameObject.SetActive(false);
            }

            string atlasInfo = generateAtlas
                ? "with texture atlas (1 material)"
                : $"({finalMaterials.Count} material{(finalMaterials.Count != 1 ? "s" : "")})";

            string dupeInfo = vertices.Count > optVerts.Count
                ? $"   |   Removed {vertices.Count - optVerts.Count} duplicate vertices ({vertices.Count} → {optVerts.Count})"
                : string.Empty;

            Debug.Log($"Created Mesh: {meshPath}   |   Combined {meshFilters.Length} meshes {atlasInfo}{dupeInfo}");

            EditorUtility.SetDirty(parentObject);
            parentObject = null; // Reset to prevent accidental re-generation
        }
    }
}
#endif