// #if UNITY_EDITOR
// using UnityEngine;
// using UnityEditor;
// using System.IO;

// namespace Nikson
// {
//     public class LodGenerator : EditorWindow
//     {
//         const string PREF_SAVE_PATH = "Nikson_LodGenerator_SavePath";
//         const string PREF_LOD0 = "Nikson_LodGenerator_Lod0";
//         const string PREF_LOD1 = "Nikson_LodGenerator_Lod1";
//         const string PREF_LOD2 = "Nikson_LodGenerator_Lod2";

//         GameObject parentObject;
//         string savePath;
//         int lod0Percent;
//         int lod1Percent;
//         int lod2Percent;

//         [MenuItem("Tools/Nikson/Optimization/7. LOD Generator")]
//         public static void ShowWindow() => GetWindow<LodGenerator>("LOD Generator");

//         void OnEnable()
//         {
//             savePath = EditorPrefs.GetString(PREF_SAVE_PATH, "Assets/Nikson/Generated/");
//             lod0Percent = EditorPrefs.GetInt(PREF_LOD0, 70);
//             lod1Percent = EditorPrefs.GetInt(PREF_LOD1, 50);
//             lod2Percent = EditorPrefs.GetInt(PREF_LOD2, 30);
//         }

//         void OnGUI()
//         {
//             EditorGUILayout.HelpBox(
//                 "\nSelect a GameObject containing a mesh and click \"Generate\" to automatically create a LOD Group with three levels of detail.\n\n" +
//                 "Each LOD level is saved as a separate mesh asset. The original mesh becomes LOD0.\n",
//                 MessageType.Info);

//             parentObject = (GameObject)EditorGUILayout.ObjectField("Parent Object", parentObject, typeof(GameObject), true);

//             EditorGUI.BeginChangeCheck();

//             EditorGUILayout.BeginHorizontal();
//             savePath = EditorGUILayout.TextField("Save Path", savePath);
//             if (GUILayout.Button("Browse", GUILayout.Width(60)))
//             {
//                 string selected = EditorUtility.OpenFolderPanel("Select Save Folder", "Assets", "");
//                 if (!string.IsNullOrEmpty(selected))
//                 {
//                     if (selected.StartsWith(Application.dataPath))
//                         savePath = "Assets" + selected.Substring(Application.dataPath.Length);
//                     else
//                         Debug.LogWarning("Selected folder must be inside the project's Assets folder.");
//                 }
//             }
//             EditorGUILayout.EndHorizontal();

//             lod0Percent = EditorGUILayout.IntSlider("LOD0 Quality %", lod0Percent, 1, 100);
//             lod1Percent = EditorGUILayout.IntSlider("LOD1 Quality %", lod1Percent, 1, 100);
//             lod2Percent = EditorGUILayout.IntSlider("LOD2 Quality %", lod2Percent, 1, 100);

//             if (EditorGUI.EndChangeCheck())
//             {
//                 EditorPrefs.SetString(PREF_SAVE_PATH, savePath);
//                 EditorPrefs.SetInt(PREF_LOD0, lod0Percent);
//                 EditorPrefs.SetInt(PREF_LOD1, lod1Percent);
//                 EditorPrefs.SetInt(PREF_LOD2, lod2Percent);
//             }

//             EditorGUILayout.Space();

//             GUI.enabled = parentObject != null;
//             if (GUILayout.Button("Generate", GUILayout.Height(30))) Generate();
//             GUI.enabled = true;
//         }

//         void Generate()
//         {
//             var meshFilter = parentObject.GetComponentInChildren<MeshFilter>();
//             var skinnedRenderer = parentObject.GetComponentInChildren<SkinnedMeshRenderer>();

//             Mesh originalMesh = null;
//             if (meshFilter != null) originalMesh = meshFilter.sharedMesh;
//             else if (skinnedRenderer != null) originalMesh = skinnedRenderer.sharedMesh;

//             if (originalMesh == null)
//             {
//                 Debug.LogError("No mesh found on the selected GameObject!");
//                 return;
//             }

//             string normalizedPath = savePath.Replace("\\", "/");
//             if (!normalizedPath.EndsWith("/")) normalizedPath += "/";
//             if (!Directory.Exists(normalizedPath))
//             {
//                 Directory.CreateDirectory(normalizedPath);
//                 AssetDatabase.Refresh();
//             }

//             Mesh[] lodMeshes = new Mesh[3];
//             int[] percents = { lod0Percent, lod1Percent, lod2Percent };

//             for (int i = 0; i < 3; i++)
//             {
//                 float quality = Mathf.Clamp01(percents[i] / 100f);
//                 var simplifier = new UnityMeshSimplifier();
//                 simplifier.Initialize(originalMesh);
//                 simplifier.SimplifyMesh(quality);
//                 lodMeshes[i] = simplifier.ToMesh();

//                 string meshPath = GetUniquePath(normalizedPath, $"{originalMesh.name}_LOD{i}", ".asset");
//                 lodMeshes[i].name = Path.GetFileNameWithoutExtension(meshPath);
//                 AssetDatabase.CreateAsset(lodMeshes[i], meshPath);
//             }

//             AssetDatabase.SaveAssets();

//             // Remove existing LODGroup if any
//             LODGroup existingGroup = parentObject.GetComponent<LODGroup>();
//             if (existingGroup != null) Undo.DestroyObjectImmediate(existingGroup);

//             LODGroup lodGroup = Undo.AddComponent<LODGroup>(parentObject);

//             MeshRenderer renderer = parentObject.GetComponentInChildren<MeshRenderer>();

//             LOD[] lods = new LOD[4];

//             // LOD0 uses original mesh
//             lods[0] = new LOD(0.6f, renderer != null ? new Renderer[] { renderer } : new Renderer[0]);

//             // LOD1, LOD2, LOD3 use simplified meshes
//             float[] screenTransitions = { 0.3f, 0.15f, 0.05f };
//             for (int i = 0; i < 3; i++)
//             {
//                 GameObject lodObj = new GameObject($"LOD{i + 1}");
//                 Undo.RegisterCreatedObjectUndo(lodObj, "Create LOD");
//                 lodObj.transform.SetParent(parentObject.transform);
//                 lodObj.transform.localPosition = Vector3.zero;
//                 lodObj.transform.localRotation = Quaternion.identity;
//                 lodObj.transform.localScale = Vector3.one;

//                 MeshFilter lf = lodObj.AddComponent<MeshFilter>();
//                 lf.sharedMesh = lodMeshes[i];

//                 MeshRenderer lr = lodObj.AddComponent<MeshRenderer>();
//                 if (renderer != null) lr.sharedMaterials = renderer.sharedMaterials;

//                 lods[i + 1] = new LOD(screenTransitions[i], new Renderer[] { lr });
//             }

//             lodGroup.SetLODs(lods);
//             lodGroup.RecalculateBounds();

//             Debug.Log($"Created LOD Group on {parentObject.name}   |   LOD0: {lod0Percent}%   LOD1: {lod1Percent}%   LOD2: {lod2Percent}%");
//             EditorUtility.SetDirty(parentObject);
//         }

//         string GetUniquePath(string folder, string name, string ext)
//         {
//             string path = Path.Combine(folder, name + ext);
//             int n = 1;
//             while (File.Exists(path) || AssetDatabase.LoadAssetAtPath<Object>(path) != null)
//                 path = Path.Combine(folder, name + n++ + ext);
//             return path;
//         }
//     }
// }
// #endif