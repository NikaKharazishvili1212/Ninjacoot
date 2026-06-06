#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Nikson.OptimizationHub;

namespace Nikson
{
    public class MeshCombiner : ScriptableObject
    {
        public void DrawGUI() => OnGUI();

        void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Combines all meshes under the selected parent GameObject into a single mesh, " +
                "optionally merging their textures into one atlas to reduce draw calls.\n\n" +
                "Select a parent GameObject in the Hierarchy, select options and then click \"Generate\". " +
                "If a file already exists, a number will be appended automatically (e.g. CombinedMesh1).",
                GetStyle());

            EditorGUILayout.Space();
            DrawResetButton();
            EditorGUILayout.Space();

            DrawSavePathField();

            CombinedMeshName = EditorGUILayout.TextField("Mesh Name", CombinedMeshName);
            if (GenerateAtlas) AtlasName = EditorGUILayout.TextField("Atlas Name", AtlasName);
            GenerateAtlas = EditorGUILayout.Toggle("Generate Atlas", GenerateAtlas);
            MeshHandling = (int)(OriginalMeshHandling)EditorGUILayout.EnumPopup("Original Meshes", (OriginalMeshHandling)MeshHandling);

            EditorGUILayout.Space();
            GUI.enabled = Selection.gameObjects.Length == 1;
            if (CenteredButton("Generate")) Generate();
            GUI.enabled = true;
        }

        void Generate()
        {
            MeshFilter[] meshFilters = Selected.GetComponentsInChildren<MeshFilter>(true);

            if (meshFilters.Length == 0)
            {
                SetStatus(2, "No MeshFilters found under the selected object!");
                return;
            }

            if (meshFilters.Length == 1)
            {
                SetStatus(2, "Only one mesh found — nothing to combine!");
                return;
            }

            MeshRenderer[] renderers = Selected.GetComponentsInChildren<MeshRenderer>(true);
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

            string meshPath = GetUniquePath(normalizedPath, CombinedMeshName, ".asset");
            Mesh combinedMesh = new Mesh { name = Path.GetFileNameWithoutExtension(meshPath) };
            combinedMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();

            List<Material> finalMaterials = new List<Material>();
            Rect[] uvRects = null;

            if (GenerateAtlas)
            {
                // Collect textures (one per material)
                List<Texture2D> textureList = new List<Texture2D>();
                foreach (var matGroup in materialGroups)
                {
                    Texture2D mainTex = matGroup.Key.mainTexture as Texture2D;
                    if (mainTex == null)
                    {
                        mainTex = new Texture2D(256, 256);
                        mainTex.SetPixels(Enumerable.Repeat(Color.white, 256 * 256).ToArray());
                        mainTex.Apply();
                    }
                    else mainTex = DuplicateTexture(mainTex);
                    textureList.Add(mainTex);
                }

                Texture2D atlasTex = GenerateTextureAtlas(textureList, AtlasName, SavePath, out uvRects);
                if (atlasTex == null) return;

                // Create material from atlas
                Material atlasMaterial = CreateAtlasMaterial(materialGroups.Keys.First(), atlasTex, AtlasName);
                string matPath = GetUniquePath(normalizedPath, AtlasName, ".mat");
                AssetDatabase.CreateAsset(atlasMaterial, matPath);
                AssetDatabase.SaveAssets();
                finalMaterials.Add(atlasMaterial);
            }

            int matIndex = 0;
            foreach (var matGroup in materialGroups)
            {
                if (!GenerateAtlas) finalMaterials.Add(matGroup.Key);

                Rect uvRect = GenerateAtlas ? uvRects[matIndex] : new Rect(0, 0, 1, 1);
                Matrix4x4 worldToParent = Selected.transform.worldToLocalMatrix;

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
                        if (GenerateAtlas)
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

            if (GenerateAtlas)
            {
                int vertOffset = 0;
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
                int vertOffset = 0;
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

            AssetDatabase.CreateAsset(combinedMesh, meshPath);
            AssetDatabase.SaveAssets();

            GameObject combined = new GameObject(CombinedMeshName);
            Undo.RegisterCreatedObjectUndo(combined, "Combine Meshes");
            combined.transform.SetParent(Selected.transform);
            combined.transform.localPosition = Vector3.zero;
            combined.transform.localRotation = Quaternion.identity;
            combined.transform.localScale = Vector3.one;

            combined.AddComponent<MeshFilter>().sharedMesh = combinedMesh;
            combined.AddComponent<MeshRenderer>().sharedMaterials = finalMaterials.ToArray();

            foreach (var filter in meshFilters)
            {
                if (MeshHandling == (int)OriginalMeshHandling.Destroy) Undo.DestroyObjectImmediate(filter.gameObject);
                else if (MeshHandling == (int)OriginalMeshHandling.Deactivate) filter.gameObject.SetActive(false);
            }

            string atlasInfo = GenerateAtlas
                ? "with texture atlas (1 material)"
                : $"({finalMaterials.Count} material{(finalMaterials.Count != 1 ? "s" : "")})";

            string dupeInfo = vertices.Count > optVerts.Count
                ? $"   |   Removed {vertices.Count - optVerts.Count} duplicate vertices ({vertices.Count} → {optVerts.Count})"
                : string.Empty;

            string warning = optVerts.Count > 65535 ? $"\nWarning: {optVerts.Count} vertices not supported on some older platforms." : string.Empty;
            SetStatus(optVerts.Count > 65535 ? 3 : 1, $"Created mesh: {meshPath}.\nCombined {meshFilters.Length} meshes {atlasInfo}{dupeInfo}.{warning}");

            EditorUtility.SetDirty(Selected);
            Deselect();
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

            static Vector3 Round(Vector3 v) => new Vector3(Mathf.Round(v.x * 10000f) / 10000f, Mathf.Round(v.y * 10000f) / 10000f, Mathf.Round(v.z * 10000f) / 10000f);

            static Vector2 Round(Vector2 v) => new Vector2(Mathf.Round(v.x * 10000f) / 10000f, Mathf.Round(v.y * 10000f) / 10000f);

            public override bool Equals(object obj)
            {
                if (!(obj is VertexKey)) return false;
                VertexKey o = (VertexKey)obj;
                return position == o.position && normal == o.normal && uv == o.uv;
            }

            public override int GetHashCode() => position.GetHashCode() ^ (normal.GetHashCode() << 2) ^ (uv.GetHashCode() >> 2);
        }
    }
}
#endif