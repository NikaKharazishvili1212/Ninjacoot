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
        public void OnGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            SelectedMeshCombinerMode = GUILayout.SelectionGrid(SelectedMeshCombinerMode, MESH_COMBINER_MODE_LABELS, 2, GUILayout.Width(250), GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();

            DrawIcon();
            if (SelectedMeshCombinerMode == 0) DrawMeshCombiner();
            else DrawSkinnedMeshCombiner();
        }

        void DrawMeshCombiner()
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
            CurrentOriginalMeshHandling = (int)(OriginalMeshHandling)EditorGUILayout.EnumPopup("Original Meshes", (OriginalMeshHandling)CurrentOriginalMeshHandling);

            EditorGUILayout.Space();
            GUI.enabled = Selection.gameObjects.Length == 1;
            if (CenteredButton("Generate")) GenerateMesh();
            GUI.enabled = true;
            EditorGUILayout.Space();
        }

        void GenerateMesh()
        {
            MeshFilter[] meshFilters = Selected.GetComponentsInChildren<MeshFilter>(true);

            if (meshFilters.Length == 0) { SetStatus(2, "No MeshFilters found under the selected object!"); return; }
            if (meshFilters.Length == 1) { SetStatus(2, "Only one mesh found — nothing to combine!"); return; }

            MeshRenderer[] renderers = Selected.GetComponentsInChildren<MeshRenderer>(true);
            var materialGroups = new Dictionary<Material, List<MeshCombineData>>();

            for (int i = 0; i < meshFilters.Length; i++)
            {
                Mesh mesh = meshFilters[i].sharedMesh;
                if (mesh == null) continue;

                Material[] mats = renderers[i].sharedMaterials;
                for (int j = 0; j < mesh.subMeshCount; j++)
                {
                    Material mat = j < mats.Length ? mats[j] : null;
                    if (mat == null) continue;
                    if (!materialGroups.ContainsKey(mat)) materialGroups[mat] = new List<MeshCombineData>();
                    materialGroups[mat].Add(new MeshCombineData { mesh = mesh, subMeshIndex = j, transform = meshFilters[i].transform });
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
                List<Texture2D> textureList = new List<Texture2D>();
                foreach (var matGroup in materialGroups)
                {
                    Texture2D mainTex = matGroup.Key.mainTexture as Texture2D;
                    if (mainTex == null) { mainTex = new Texture2D(256, 256); mainTex.SetPixels(Enumerable.Repeat(Color.white, 256 * 256).ToArray()); mainTex.Apply(); }
                    else mainTex = DuplicateTexture(mainTex);
                    textureList.Add(mainTex);
                }

                Texture2D atlasTex = GenerateTextureAtlas(textureList, AtlasName, SavePath, out uvRects);
                if (atlasTex == null) return;

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
                        if (GenerateAtlas) { uv.x = uvRect.x + uv.x * uvRect.width; uv.y = uvRect.y + uv.y * uvRect.height; }
                        uvs.Add(uv);
                    }
                }
                matIndex++;
            }

            var (optVerts, optNormals, optUVs, vertexRemap) = DeduplicateVertices(vertices, normals, uvs);

            combinedMesh.vertices = optVerts.ToArray();
            combinedMesh.normals = optNormals.ToArray();
            combinedMesh.uv = optUVs.ToArray();

            AssignIndices(combinedMesh, materialGroups, vertexRemap, GenerateAtlas);

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
                if (CurrentOriginalMeshHandling == (int)OriginalMeshHandling.Destroy) Undo.DestroyObjectImmediate(filter.gameObject);
                else if (CurrentOriginalMeshHandling == (int)OriginalMeshHandling.Deactivate) filter.gameObject.SetActive(false);
            }

            string atlasInfo = GenerateAtlas ? "with texture atlas (1 material)" : $"({finalMaterials.Count} material{(finalMaterials.Count != 1 ? "s" : "")})";
            string dupeInfo = vertices.Count > optVerts.Count ? $"   |   Removed {vertices.Count - optVerts.Count} duplicate vertices ({vertices.Count} → {optVerts.Count})" : string.Empty;
            string warning = optVerts.Count > 65535 ? $"\nWarning: {optVerts.Count} vertices not supported on some older platforms." : string.Empty;
            SetStatus(optVerts.Count > 65535 ? 3 : 1, $"Created mesh: {meshPath}.\nCombined {meshFilters.Length} meshes {atlasInfo}{dupeInfo}.{warning}");

            EditorUtility.SetDirty(Selected);
            Deselect();
        }

        void DrawSkinnedMeshCombiner()
        {
            EditorGUILayout.LabelField(
                "Combines all skinned meshes under the selected parent GameObject into a single mesh, " +
                "preserving bone weights and optionally merging textures into one atlas to reduce draw calls.\n\n" +
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
            CurrentOriginalMeshHandling = (int)(OriginalMeshHandling)EditorGUILayout.EnumPopup("Original Meshes", (OriginalMeshHandling)CurrentOriginalMeshHandling);

            EditorGUILayout.Space();
            GUI.enabled = Selection.gameObjects.Length == 1;
            if (CenteredButton("Generate")) GenerateSkinnedMesh();
            GUI.enabled = true;
        }

        void GenerateSkinnedMesh()
        {
            SkinnedMeshRenderer[] skinnedRenderers = Selected.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            if (skinnedRenderers.Length == 0) { SetStatus(2, "No SkinnedMeshRenderers found under the selected object!"); return; }
            if (skinnedRenderers.Length == 1) { SetStatus(2, "Only one skinned mesh found — nothing to combine!"); return; }

            Transform rootBone = null;
            var allBones = new List<Transform>();

            foreach (var renderer in skinnedRenderers)
            {
                if (rootBone == null && renderer.rootBone != null) rootBone = renderer.rootBone;
                foreach (var bone in renderer.bones)
                    if (bone != null && !allBones.Contains(bone))
                        allBones.Add(bone);
            }

            if (rootBone == null) { SetStatus(2, "No root bone found on any SkinnedMeshRenderer. Cannot combine."); return; }

            var materialGroups = new Dictionary<Material, List<SkinnedCombineData>>();

            foreach (var renderer in skinnedRenderers)
            {
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null) continue;

                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    Material mat = i < renderer.sharedMaterials.Length ? renderer.sharedMaterials[i] : null;
                    if (mat == null) continue;
                    if (!materialGroups.ContainsKey(mat)) materialGroups[mat] = new List<SkinnedCombineData>();
                    materialGroups[mat].Add(new SkinnedCombineData { mesh = mesh, subMeshIndex = i, transform = renderer.transform, bones = renderer.bones, boneWeights = mesh.boneWeights, bindPoses = mesh.bindposes });
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
            var boneWeights = new List<BoneWeight>();
            List<Material> finalMaterials = new List<Material>();
            Rect[] uvRects = null;

            if (GenerateAtlas)
            {
                List<Texture2D> textureList = new List<Texture2D>();
                foreach (var matGroup in materialGroups)
                {
                    Texture2D mainTex = matGroup.Key.mainTexture as Texture2D;
                    if (mainTex == null) { mainTex = new Texture2D(256, 256); mainTex.SetPixels(Enumerable.Repeat(Color.white, 256 * 256).ToArray()); mainTex.Apply(); }
                    else mainTex = DuplicateTexture(mainTex);
                    textureList.Add(mainTex);
                }

                Texture2D atlasTex = GenerateTextureAtlas(textureList, AtlasName, SavePath, out uvRects);
                if (atlasTex == null) return;

                Material atlasMaterial = CreateAtlasMaterial(materialGroups.Keys.First(), atlasTex, AtlasName);
                string matPath = GetUniquePath(normalizedPath, AtlasName, ".mat");
                AssetDatabase.CreateAsset(atlasMaterial, matPath);
                AssetDatabase.SaveAssets();
                finalMaterials.Add(atlasMaterial);
            }

            Matrix4x4 worldToRoot = rootBone.worldToLocalMatrix;
            int matIndex = 0;

            foreach (var matGroup in materialGroups)
            {
                if (!GenerateAtlas) finalMaterials.Add(matGroup.Key);
                Rect uvRect = GenerateAtlas ? uvRects[matIndex] : new Rect(0, 0, 1, 1);

                foreach (var data in matGroup.Value)
                {
                    Vector3[] meshVerts = data.mesh.vertices;
                    Vector3[] meshNormals = data.mesh.normals;
                    Vector2[] meshUVs = data.mesh.uv;

                    for (int i = 0; i < meshVerts.Length; i++)
                    {
                        BoneWeight weight = data.boneWeights[i];
                        Matrix4x4 bm0 = data.bones[weight.boneIndex0].localToWorldMatrix * data.bindPoses[weight.boneIndex0];
                        Matrix4x4 bm1 = data.bones[weight.boneIndex1].localToWorldMatrix * data.bindPoses[weight.boneIndex1];
                        Matrix4x4 bm2 = data.bones[weight.boneIndex2].localToWorldMatrix * data.bindPoses[weight.boneIndex2];
                        Matrix4x4 bm3 = data.bones[weight.boneIndex3].localToWorldMatrix * data.bindPoses[weight.boneIndex3];

                        Vector3 worldVert = bm0.MultiplyPoint3x4(meshVerts[i]) * weight.weight0 + bm1.MultiplyPoint3x4(meshVerts[i]) * weight.weight1 + bm2.MultiplyPoint3x4(meshVerts[i]) * weight.weight2 + bm3.MultiplyPoint3x4(meshVerts[i]) * weight.weight3;
                        Vector3 worldNorm = bm0.MultiplyVector(meshNormals[i]) * weight.weight0 + bm1.MultiplyVector(meshNormals[i]) * weight.weight1 + bm2.MultiplyVector(meshNormals[i]) * weight.weight2 + bm3.MultiplyVector(meshNormals[i]) * weight.weight3;

                        vertices.Add(worldToRoot.MultiplyPoint3x4(worldVert));
                        normals.Add(worldToRoot.MultiplyVector(worldNorm).normalized);

                        Vector2 uv = (meshUVs != null && i < meshUVs.Length) ? meshUVs[i] : Vector2.zero;
                        if (GenerateAtlas) { uv.x = uvRect.x + uv.x * uvRect.width; uv.y = uvRect.y + uv.y * uvRect.height; }
                        uvs.Add(uv);

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

            var (optVerts, optNormals, optUVs, vertexRemap, optWeights) = DeduplicateSkinnedVertices(vertices, normals, uvs, boneWeights);

            combinedMesh.vertices = optVerts.ToArray();
            combinedMesh.normals = optNormals.ToArray();
            combinedMesh.uv = optUVs.ToArray();
            combinedMesh.boneWeights = optWeights.ToArray();

            AssignSkinnedIndices(combinedMesh, materialGroups, vertexRemap, GenerateAtlas);

            // Remap bones — remove unused
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
                w.boneIndex0 = boneRemap[w.boneIndex0]; w.boneIndex1 = boneRemap[w.boneIndex1];
                w.boneIndex2 = boneRemap[w.boneIndex2]; w.boneIndex3 = boneRemap[w.boneIndex3];
                remappedWeights[i] = w;
            }

            combinedMesh.boneWeights = remappedWeights;

            Matrix4x4[] newBindPoses = new Matrix4x4[usedBonesList.Count];
            for (int i = 0; i < usedBonesList.Count; i++)
                newBindPoses[i] = (rootBone.worldToLocalMatrix * usedBonesList[i].localToWorldMatrix).inverse;
            combinedMesh.bindposes = newBindPoses;

            Transform[] usedBones = usedBonesList.ToArray();
            combinedMesh.RecalculateBounds();

            AssetDatabase.CreateAsset(combinedMesh, meshPath);
            AssetDatabase.SaveAssets();

            GameObject combined = new GameObject(CombinedMeshName);
            Undo.RegisterCreatedObjectUndo(combined, "Combine Skinned Meshes");
            combined.transform.SetParent(Selected.transform);
            combined.transform.localPosition = Vector3.zero;
            combined.transform.localRotation = Quaternion.identity;
            combined.transform.localScale = Vector3.one;

            var newRenderer = combined.AddComponent<SkinnedMeshRenderer>();
            newRenderer.sharedMesh = combinedMesh;
            newRenderer.bones = usedBones;
            newRenderer.rootBone = rootBone;
            newRenderer.sharedMaterials = finalMaterials.ToArray();

            foreach (var renderer in skinnedRenderers)
            {
                if (CurrentOriginalMeshHandling == (int)OriginalMeshHandling.Destroy) Undo.DestroyObjectImmediate(renderer.gameObject);
                else if (CurrentOriginalMeshHandling == (int)OriginalMeshHandling.Deactivate) renderer.gameObject.SetActive(false);
            }

            string atlasInfo = GenerateAtlas ? "with texture atlas (1 material)" : $"({finalMaterials.Count} material{(finalMaterials.Count != 1 ? "s" : "")})";
            string dupeInfo = vertices.Count > optVerts.Count ? $"   |   Removed {vertices.Count - optVerts.Count} duplicate vertices ({vertices.Count} → {optVerts.Count}) and {allBones.Count - usedBones.Length} unused bones" : string.Empty;
            string warning = optVerts.Count > 65535 ? $"\nWarning: {optVerts.Count} vertices not supported on some older platforms." : string.Empty;
            SetStatus(optVerts.Count > 65535 ? 3 : 1, $"Created mesh: {meshPath}.\nCombined {skinnedRenderers.Length} meshes {atlasInfo}{dupeInfo}.{warning}");

            EditorUtility.SetDirty(Selected);
            Deselect();
        }

        // Shared helpers
        (List<Vector3>, List<Vector3>, List<Vector2>, int[]) DeduplicateVertices(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs)
        {
            var map = new Dictionary<VertexKey, int>();
            var optVerts = new List<Vector3>();
            var optNormals = new List<Vector3>();
            var optUVs = new List<Vector2>();
            int[] remap = new int[verts.Count];

            for (int i = 0; i < verts.Count; i++)
            {
                var key = new VertexKey(verts[i], norms[i], uvs[i]);
                if (map.TryGetValue(key, out int existing)) remap[i] = existing;
                else { map[key] = optVerts.Count; remap[i] = optVerts.Count; optVerts.Add(verts[i]); optNormals.Add(norms[i]); optUVs.Add(uvs[i]); }
            }
            return (optVerts, optNormals, optUVs, remap);
        }

        (List<Vector3>, List<Vector3>, List<Vector2>, int[], List<BoneWeight>) DeduplicateSkinnedVertices(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs, List<BoneWeight> weights)
        {
            var map = new Dictionary<SkinnedVertexKey, int>();
            var optVerts = new List<Vector3>();
            var optNormals = new List<Vector3>();
            var optUVs = new List<Vector2>();
            var optWeights = new List<BoneWeight>();
            int[] remap = new int[verts.Count];

            for (int i = 0; i < verts.Count; i++)
            {
                var key = new SkinnedVertexKey(verts[i], norms[i], uvs[i], weights[i]);
                if (map.TryGetValue(key, out int existing)) remap[i] = existing;
                else { map[key] = optVerts.Count; remap[i] = optVerts.Count; optVerts.Add(verts[i]); optNormals.Add(norms[i]); optUVs.Add(uvs[i]); optWeights.Add(weights[i]); }
            }
            return (optVerts, optNormals, optUVs, remap, optWeights);
        }

        void AssignIndices(Mesh mesh, Dictionary<Material, List<MeshCombineData>> materialGroups, int[] vertexRemap, bool atlas)
        {
            int vertOffset = 0;
            if (atlas)
            {
                mesh.subMeshCount = 1;
                var allIndices = new List<int>();
                foreach (var matGroup in materialGroups)
                    foreach (var data in matGroup.Value)
                    {
                        int[] raw = data.mesh.GetIndices(data.subMeshIndex);
                        for (int i = 0; i < raw.Length; i++) allIndices.Add(vertexRemap[raw[i] + vertOffset]);
                        vertOffset += data.mesh.vertices.Length;
                    }
                mesh.SetIndices(allIndices.ToArray(), MeshTopology.Triangles, 0);
            }
            else
            {
                mesh.subMeshCount = materialGroups.Count;
                int sub = 0;
                foreach (var matGroup in materialGroups)
                {
                    var indices = new List<int>();
                    foreach (var data in matGroup.Value)
                    {
                        int[] raw = data.mesh.GetIndices(data.subMeshIndex);
                        for (int i = 0; i < raw.Length; i++) indices.Add(vertexRemap[raw[i] + vertOffset]);
                        vertOffset += data.mesh.vertices.Length;
                    }
                    mesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, sub++);
                }
            }
        }

        void AssignSkinnedIndices(Mesh mesh, Dictionary<Material, List<SkinnedCombineData>> materialGroups, int[] vertexRemap, bool atlas)
        {
            int vertOffset = 0;
            if (atlas)
            {
                mesh.subMeshCount = 1;
                var allIndices = new List<int>();
                foreach (var matGroup in materialGroups)
                    foreach (var data in matGroup.Value)
                    {
                        int[] raw = data.mesh.GetIndices(data.subMeshIndex);
                        for (int i = 0; i < raw.Length; i++) allIndices.Add(vertexRemap[raw[i] + vertOffset]);
                        vertOffset += data.mesh.vertices.Length;
                    }
                mesh.SetIndices(allIndices.ToArray(), MeshTopology.Triangles, 0);
            }
            else
            {
                mesh.subMeshCount = materialGroups.Count;
                int sub = 0;
                foreach (var matGroup in materialGroups)
                {
                    var indices = new List<int>();
                    foreach (var data in matGroup.Value)
                    {
                        int[] raw = data.mesh.GetIndices(data.subMeshIndex);
                        for (int i = 0; i < raw.Length; i++) indices.Add(vertexRemap[raw[i] + vertOffset]);
                        vertOffset += data.mesh.vertices.Length;
                    }
                    mesh.SetIndices(indices.ToArray(), MeshTopology.Triangles, sub++);
                }
            }
        }

        // Local data types
        class MeshCombineData
        {
            public Mesh mesh;
            public int subMeshIndex;
            public Transform transform;
        }

        class SkinnedCombineData
        {
            public Mesh mesh;
            public int subMeshIndex;
            public Transform transform;
            public Transform[] bones;
            public BoneWeight[] boneWeights;
            public Matrix4x4[] bindPoses;
        }

        struct VertexKey
        {
            Vector3 position, normal;
            Vector2 uv;

            public VertexKey(Vector3 pos, Vector3 norm, Vector2 uv)
            {
                position = Round(pos); normal = Round(norm); this.uv = Round(uv);
            }

            static Vector3 Round(Vector3 v) => new Vector3(Mathf.Round(v.x * 10000f) / 10000f, Mathf.Round(v.y * 10000f) / 10000f, Mathf.Round(v.z * 10000f) / 10000f);
            static Vector2 Round(Vector2 v) => new Vector2(Mathf.Round(v.x * 10000f) / 10000f, Mathf.Round(v.y * 10000f) / 10000f);

            public override bool Equals(object obj)
            {
                if (!(obj is VertexKey o)) return false;
                return position == o.position && normal == o.normal && uv == o.uv;
            }
            public override int GetHashCode() => position.GetHashCode() ^ (normal.GetHashCode() << 2) ^ (uv.GetHashCode() >> 2);
        }

        struct SkinnedVertexKey
        {
            Vector3 position, normal;
            Vector2 uv;
            int bone0, bone1, bone2, bone3;
            float weight0, weight1, weight2, weight3;

            public SkinnedVertexKey(Vector3 pos, Vector3 norm, Vector2 uv, BoneWeight w)
            {
                position = Round(pos); normal = Round(norm); this.uv = Round(uv);
                bone0 = w.boneIndex0; bone1 = w.boneIndex1; bone2 = w.boneIndex2; bone3 = w.boneIndex3;
                weight0 = w.weight0; weight1 = w.weight1; weight2 = w.weight2; weight3 = w.weight3;
            }

            static Vector3 Round(Vector3 v) => new Vector3(Mathf.Round(v.x * 10000f) / 10000f, Mathf.Round(v.y * 10000f) / 10000f, Mathf.Round(v.z * 10000f) / 10000f);
            static Vector2 Round(Vector2 v) => new Vector2(Mathf.Round(v.x * 10000f) / 10000f, Mathf.Round(v.y * 10000f) / 10000f);

            public override bool Equals(object obj)
            {
                if (!(obj is SkinnedVertexKey o)) return false;
                return position == o.position && normal == o.normal && uv == o.uv &&
                       bone0 == o.bone0 && bone1 == o.bone1 && bone2 == o.bone2 && bone3 == o.bone3 &&
                       weight0 == o.weight0 && weight1 == o.weight1 && weight2 == o.weight2 && weight3 == o.weight3;
            }
            public override int GetHashCode() => position.GetHashCode() ^ (normal.GetHashCode() << 2) ^ (uv.GetHashCode() >> 2) ^ (bone0 << 24) ^ (bone1 << 16) ^ (bone2 << 8) ^ bone3;
        }
    }
}
#endif