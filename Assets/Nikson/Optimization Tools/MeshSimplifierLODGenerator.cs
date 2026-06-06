#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using UnityMeshSimplifier;
using static Nikson.OptimizationHub;

namespace Nikson
{
    public class MeshSimplifierLODGenerator : ScriptableObject
    {
        public void DrawGUI() => OnGUI();

        void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Simplify a mesh by reducing its triangle count, or generate LOD levels.\n\n" +
                "Select a GameObject with a mesh in the Hierarchy, select options and then click \"Generate\". " +
                "If a file already exists, a number will be appended automatically (e.g. SimplifiedMesh1).",
                GetStyle());

            EditorGUILayout.Space();
            DrawResetButton();
            EditorGUILayout.Space();

            DrawSavePathField();
            SimplifiedMeshName = EditorGUILayout.TextField("Mesh Name", SimplifiedMeshName);
            SimplifierMode = (int)(Mode)EditorGUILayout.EnumPopup("Mode", (Mode)SimplifierMode);

            EditorGUILayout.Space();

            if (SimplifierMode == (int)Mode.SimplifyMesh) SimplifyQuality = EditorGUILayout.IntSlider("Quality %", SimplifyQuality, 1, 100);
            else
            {
                LodLevelCount = Mathf.Clamp(EditorGUILayout.IntField("LOD Levels", LodLevelCount), 1, 8);

                // Resize arrays if level count changed
                if (LodQualities == null || LodQualities.Length != LodLevelCount)
                {
                    var newQ = new int[LodLevelCount];
                    var newH = new int[LodLevelCount];
                    for (int i = 0; i < LodLevelCount; i++)
                    {
                        newQ[i] = LodQualities != null && i < LodQualities.Length ? LodQualities[i] : (int)Mathf.Lerp(65, 10, (float)i / Mathf.Max(1, LodLevelCount - 1));
                        newH[i] = LodScreenHeights != null && i < LodScreenHeights.Length ? LodScreenHeights[i] : (int)Mathf.Lerp(60, 2, (float)i / Mathf.Max(1, LodLevelCount - 1));
                    }
                    LodQualities = newQ;
                    LodScreenHeights = newH;
                }

                EditorGUILayout.Space();
                for (int i = 0; i < LodLevelCount; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"LOD {i + 1}", GUILayout.Width(40));
                    LodQualities[i] = EditorGUILayout.IntSlider("Quality %", LodQualities[i], 1, 100);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("", GUILayout.Width(40));
                    LodScreenHeights[i] = EditorGUILayout.IntSlider("Screen Height %", LodScreenHeights[i], 1, 100);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(4);
                }
            }

            EditorGUILayout.Space();
            GUI.enabled = Selection.gameObjects.Length == 1;
            if (CenteredButton("Generate")) Generate();
            GUI.enabled = true;
        }

        void Generate()
        {
            if (SimplifierMode == (int)Mode.SimplifyMesh) GenerateSimplified();
            else GenerateLODs();
        }

        void GenerateSimplified()
        {
            var meshFilter = Selected.GetComponent<MeshFilter>();
            var skinnedRenderer = Selected.GetComponent<SkinnedMeshRenderer>();

            Mesh sourceMesh = meshFilter != null ? meshFilter.sharedMesh : skinnedRenderer != null ? skinnedRenderer.sharedMesh : null;

            if (sourceMesh == null)
            {
                SetStatus(2, "Selected object has no MeshFilter or SkinnedMeshRenderer!");
                return;
            }

            int trisBefore = sourceMesh.triangles.Length / 3;

            var simplifier = new MeshSimplifier();
            simplifier.Initialize(sourceMesh);
            simplifier.SimplifyMesh(SimplifyQuality / 100f);
            Mesh simplified = simplifier.ToMesh();
            simplified.bindposes = sourceMesh.bindposes;

            int trisAfter = simplified.triangles.Length / 3;

            string normalizedPath = NormalizePath(SavePath);
            EnsureDirectory(normalizedPath);
            string meshPath = GetUniquePath(normalizedPath, SimplifiedMeshName, ".asset");
            simplified.name = Path.GetFileNameWithoutExtension(meshPath);

            AssetDatabase.CreateAsset(simplified, meshPath);
            AssetDatabase.SaveAssets();

            if (meshFilter != null)
            {
                Undo.RecordObject(meshFilter, "Simplify Mesh");
                meshFilter.sharedMesh = simplified;
            }
            else if (skinnedRenderer != null)
            {
                Undo.RecordObject(skinnedRenderer, "Simplify Mesh");
                skinnedRenderer.sharedMesh = simplified;
            }

            EditorUtility.SetDirty(Selected);
            SetStatus(1, $"Simplified mesh saved: {meshPath}.\nTriangles: {trisBefore:N0} → {trisAfter:N0} ({Mathf.RoundToInt((1f - (float)trisAfter / trisBefore) * 100)}% reduction, quality: {SimplifyQuality}%).");
            Deselect();
        }

        void GenerateLODs()
        {
            var meshFilter = Selected.GetComponent<MeshFilter>();
            var skinnedRenderer = Selected.GetComponent<SkinnedMeshRenderer>();

            Mesh sourceMesh = meshFilter != null ? meshFilter.sharedMesh : skinnedRenderer != null ? skinnedRenderer.sharedMesh : null;
            Renderer sourceRenderer = meshFilter != null ? (Renderer)meshFilter.GetComponent<MeshRenderer>() : skinnedRenderer != null ? skinnedRenderer : null;

            if (sourceMesh == null || sourceRenderer == null)
            {
                SetStatus(2, "Selected object has no mesh renderer!");
                return;
            }

            string normalizedPath = NormalizePath(SavePath);
            EnsureDirectory(normalizedPath);

            // Remove existing LODGroup if present
            var existingGroup = Selected.GetComponent<LODGroup>();
            if (existingGroup != null) Undo.DestroyObjectImmediate(existingGroup);

            var lodGroup = Undo.AddComponent<LODGroup>(Selected);
            var lods = new UnityEngine.LOD[LodLevelCount + 1];

            // LOD 0 = original mesh, no simplification
            lods[0] = new UnityEngine.LOD(1f, new Renderer[] { sourceRenderer });

            for (int i = 0; i < LodLevelCount; i++)
            {
                var simplifier = new MeshSimplifier();
                simplifier.Initialize(sourceMesh);
                simplifier.SimplifyMesh(LodQualities[i] / 100f);
                Mesh lodMesh = simplifier.ToMesh();
                lodMesh.bindposes = sourceMesh.bindposes;

                string lodMeshPath = GetUniquePath(normalizedPath, $"{SimplifiedMeshName}_LOD{i + 1}", ".asset");
                lodMesh.name = Path.GetFileNameWithoutExtension(lodMeshPath);
                AssetDatabase.CreateAsset(lodMesh, lodMeshPath);

                // Create child GameObject for this LOD level
                var lodGO = new GameObject($"{Selected.name}_LOD{i + 1}");
                Undo.RegisterCreatedObjectUndo(lodGO, "Generate LODs");
                lodGO.transform.SetParent(Selected.transform);
                lodGO.transform.localPosition = Vector3.zero;
                lodGO.transform.localRotation = Quaternion.identity;
                lodGO.transform.localScale = Vector3.one;

                Renderer lodRenderer;
                if (meshFilter != null)
                {
                    var mf = lodGO.AddComponent<MeshFilter>();
                    mf.sharedMesh = lodMesh;
                    var mr = lodGO.AddComponent<MeshRenderer>();
                    mr.sharedMaterials = sourceRenderer.sharedMaterials;
                    lodRenderer = mr;
                }
                else
                {
                    var smr = lodGO.AddComponent<SkinnedMeshRenderer>();
                    smr.sharedMesh = lodMesh;
                    smr.sharedMaterials = sourceRenderer.sharedMaterials;
                    smr.rootBone = skinnedRenderer.rootBone;
                    smr.bones = skinnedRenderer.bones;
                    lodRenderer = smr;
                }

                lods[i + 1] = new UnityEngine.LOD(LodScreenHeights[i] / 100f, new Renderer[] { lodRenderer });
            }

            // Last LOD culls the object
            lods[LodLevelCount] = new UnityEngine.LOD(0f, new Renderer[0]);

            AssetDatabase.SaveAssets();
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            EditorUtility.SetDirty(Selected);
            SetStatus(1, $"Generated lod: {normalizedPath}.\n{LodLevelCount} LOD {(LodLevelCount == 1 ? "level" : "levels")} for {Selected.name}.");
            Deselect();
        }
    }
}
#endif