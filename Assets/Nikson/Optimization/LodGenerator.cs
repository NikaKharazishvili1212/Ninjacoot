#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using static Nikson.OptimizationHub;

namespace Nikson
{
    public class LodGenerator : EditorWindow
    {
        public void DrawGUI() => OnGUI();

        void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Select a GameObject containing a mesh and click \"Generate\" to automatically create a LOD Group with three levels of detail.\n\n" +
                "Each LOD level is saved as a separate mesh asset. The original mesh becomes LOD0.",
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

            Lod0Percent = EditorGUILayout.IntSlider("LOD0 Quality %", Lod0Percent, 1, 100);
            Lod1Percent = EditorGUILayout.IntSlider("LOD1 Quality %", Lod1Percent, 1, 100);
            Lod2Percent = EditorGUILayout.IntSlider("LOD2 Quality %", Lod2Percent, 1, 100);

            EditorGUILayout.Space();

            GUI.enabled = ParentObject != null;
            if (GUILayout.Button("Generate", GUILayout.Height(30))) Generate();
            GUI.enabled = true;
        }

        void Generate()
        {
            var meshFilter = ParentObject.GetComponentInChildren<MeshFilter>();
            var skinnedRenderer = ParentObject.GetComponentInChildren<SkinnedMeshRenderer>();

            Mesh originalMesh = null;
            if (meshFilter != null) originalMesh = meshFilter.sharedMesh;
            else if (skinnedRenderer != null) originalMesh = skinnedRenderer.sharedMesh;

            if (originalMesh == null)
            {
                Debug.LogError("No mesh found on the selected GameObject!");
                return;
            }

            string normalizedPath = SavePath.Replace("\\", "/");
            if (!normalizedPath.EndsWith("/")) normalizedPath += "/";
            if (!Directory.Exists(normalizedPath))
            {
                Directory.CreateDirectory(normalizedPath);
                AssetDatabase.Refresh();
            }

            Mesh[] lodMeshes = new Mesh[3];
            int[] percents = { Lod0Percent, Lod1Percent, Lod2Percent };

            for (int i = 0; i < 3; i++)
            {
                float quality = Mathf.Clamp01(percents[i] / 100f);
                var simplifier = new UnityMeshSimplifier();
                simplifier.Initialize(originalMesh);
                simplifier.SimplifyMesh(quality);
                lodMeshes[i] = simplifier.ToMesh();

                string meshPath = GetUniquePath(normalizedPath, $"{originalMesh.name}_LOD{i}", ".asset");
                lodMeshes[i].name = Path.GetFileNameWithoutExtension(meshPath);
                AssetDatabase.CreateAsset(lodMeshes[i], meshPath);
            }

            AssetDatabase.SaveAssets();

            // Remove existing LODGroup if any
            LODGroup existingGroup = ParentObject.GetComponent<LODGroup>();
            if (existingGroup != null) Undo.DestroyObjectImmediate(existingGroup);

            LODGroup lodGroup = Undo.AddComponent<LODGroup>(ParentObject);

            MeshRenderer renderer = ParentObject.GetComponentInChildren<MeshRenderer>();

            UnityEngine.LOD[] lods = new UnityEngine.LOD[4];

            lods[0].screenRelativeTransitionHeight = 0.6f;
            lods[0].renderers = renderer != null ? new Renderer[] { renderer } : new Renderer[0];

            float[] screenTransitions = { 0.3f, 0.15f, 0.05f };
            for (int i = 0; i < 3; i++)
            {
                GameObject lodObj = new GameObject($"LOD{i + 1}");
                Undo.RegisterCreatedObjectUndo(lodObj, "Create LOD");
                lodObj.transform.SetParent(ParentObject.transform);
                lodObj.transform.localPosition = Vector3.zero;
                lodObj.transform.localRotation = Quaternion.identity;
                lodObj.transform.localScale = Vector3.one;

                MeshFilter lf = lodObj.AddComponent<MeshFilter>();
                lf.sharedMesh = lodMeshes[i];

                MeshRenderer lr = lodObj.AddComponent<MeshRenderer>();
                if (renderer != null) lr.sharedMaterials = renderer.sharedMaterials;

                lods[i + 1].screenRelativeTransitionHeight = screenTransitions[i];
                lods[i + 1].renderers = new Renderer[] { lr };
            }

            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();

            Debug.Log($"Created LOD Group on {ParentObject.name}   |   LOD0: {Lod0Percent}%   LOD1: {Lod1Percent}%   LOD2: {Lod2Percent}%");
            EditorUtility.SetDirty(ParentObject);
        }
    }
}
#endif