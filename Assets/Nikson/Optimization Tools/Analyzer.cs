#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using static Nikson.OptimizationHub;

namespace Nikson
{
    public class Analyzer : ScriptableObject
    {
        List<List<string>> duplicateGroups = new List<List<string>>();
        List<(string path, string issue)> projectIssues = new List<(string, string)>();

        // Scene
        int totalMeshes, totalVertices, totalTriangles;
        int totalMaterials, totalTextures;
        long totalTextureMemory;
        int totalLights, totalCameras, totalParticles;
        int totalAudio, totalAnimators, totalColliders, totalRigidbodies;

        // Shared
        bool scanned;
        int lastAnalyzerMode = -1;
        Vector2 scroll;
        bool showMeshIssues = true, showTextureIssues = true, showAudioIssues = true;

        public void OnGUI()
        {
            if (SelectedAnalyzerMode != lastAnalyzerMode)
            {
                lastAnalyzerMode = SelectedAnalyzerMode;
                scanned = false;
                SetStatus(0, string.Empty);
            }

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            SelectedAnalyzerMode = GUILayout.SelectionGrid(SelectedAnalyzerMode, ANALYZER_MODE_LABELS, 4, GUILayout.Width(340), GUILayout.Height(30));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayout.Space();

            DrawIcon();
            if (SelectedAnalyzerMode == 0) // Canvas
            {
                EditorGUILayout.LabelField(
                    "Unity rebuilds the entire Canvas mesh whenever any Graphic component on it changes. " +
                    "The more Graphic components a Canvas has, the more expensive that rebuild is.\n\n" +
                    "This tool counts all Graphic components (Image, Text, RawImage, etc.) on the selected Canvas " +
                    "and tells you whether it is safe for frequent updates, or whether you should split it " +
                    "into smaller canvases to avoid performance issues.\n\n" +
                    "Select a GameObject with a Canvas component in the Hierarchy, then click \"Analyze\".",
                    GetStyle());

                EditorGUILayout.Space();
                GUI.enabled = Selection.gameObjects.Length == 1 && Selected != null && Selected.GetComponent<Canvas>() != null;
                if (CenteredButton("Analyze")) AnalyzeCanvas();
                GUI.enabled = true;
                EditorGUILayout.Space();
            }
            else if (SelectedAnalyzerMode == 1) // Scene
            {
                EditorGUILayout.LabelField(
                    "Scans the current scene and reports geometry, rendering, and component counts. " +
                    "Use this for a quick overview of what is in the scene. " +
                    "For per-asset cost analysis, use Project mode. " +
                    "Click \"Analyze\" to scan.",
                    GetStyle());

                EditorGUILayout.Space();
                if (CenteredButton("Analyze")) { scanned = false; AnalyzeScene(); }
                EditorGUILayout.Space();

                if (!scanned) return;

                scroll = EditorGUILayout.BeginScrollView(scroll);

                DrawSection("Geometry");
                DrawRow("Meshes", totalMeshes.ToString("N0"));
                DrawRow("Vertices", totalVertices.ToString("N0"));
                DrawRow("Triangles", totalTriangles.ToString("N0"));
                EditorGUILayout.Space(10);

                DrawSection("Rendering");
                DrawRow("Materials", totalMaterials.ToString("N0"));
                DrawRow("Textures", totalTextures.ToString("N0"));
                DrawRow("Tex Memory", FormatBytes(totalTextureMemory));
                DrawRow("Lights", totalLights.ToString("N0"));
                DrawRow("Cameras", totalCameras.ToString("N0"));
                DrawRow("Particles", totalParticles.ToString("N0"));
                EditorGUILayout.Space(10);

                DrawSection("Components");
                DrawRow("Audio Sources", totalAudio.ToString("N0"));
                DrawRow("Animators", totalAnimators.ToString("N0"));
                DrawRow("Colliders", totalColliders.ToString("N0"));
                DrawRow("Rigidbodies", totalRigidbodies.ToString("N0"));

                EditorGUILayout.EndScrollView();
            }
            else if (SelectedAnalyzerMode == 2) // Project
            {
                EditorGUILayout.LabelField(
                    "Scans all assets in the project for issues that hurt performance or increase build size.\n\n" +
                    "Checks for: high-poly meshes, oversized or misconfigured textures, and expensive audio clips. " +
                    "Click \"Analyze\" to scan.",
                    GetStyle());

                EditorGUILayout.Space();
                if (CenteredButton("Analyze")) { scanned = false; AnalyzeProject(); }
                EditorGUILayout.Space();

                if (!scanned) return;

                scroll = EditorGUILayout.BeginScrollView(scroll);

                var meshIssues = projectIssues.Where(i => i.issue.StartsWith("[Mesh]")).ToList();
                var textureIssues = projectIssues.Where(i => i.issue.StartsWith("[Texture]")).ToList();
                var audioIssues = projectIssues.Where(i => i.issue.StartsWith("[Audio]")).ToList();

                if (meshIssues.Count > 0) DrawIssueFoldout("Mesh Issues", meshIssues, "[Mesh] ", ref showMeshIssues);
                if (textureIssues.Count > 0) DrawIssueFoldout("Texture Issues", textureIssues, "[Texture] ", ref showTextureIssues);
                if (audioIssues.Count > 0) DrawIssueFoldout("Audio Issues", audioIssues, "[Audio] ", ref showAudioIssues);

                if (projectIssues.Count == 0)
                    EditorGUILayout.LabelField("No issues found. Project looks clean.", GetStyle());

                EditorGUILayout.EndScrollView();
            }
            else if (SelectedAnalyzerMode == 3) // Duplicates
            {
                EditorGUILayout.LabelField(
                    "Scans the entire project for duplicate assets by comparing file contents (MD5 hash). " +
                    "Works for textures, audio, scripts, and any binary asset that is byte-for-byte identical.\n\n" +
                    "Cannot detect duplicates for Unity-serialized assets (.prefab, .mat, .anim, .mesh, .lighting, .unity) " +
                    "because Unity embeds a unique GUID into each one at creation time.\n\n" +
                    "Click a path to ping it in the Project window. No files are deleted. " +
                    "Click \"Analyze\" to scan.",
                    GetStyle());

                EditorGUILayout.Space();
                if (CenteredButton("Analyze")) { scanned = false; AnalyzeDuplicates(); }
                EditorGUILayout.Space();

                if (!scanned) return;

                if (duplicateGroups.Count == 0) EditorGUILayout.LabelField("No duplicate assets found. Project looks clean.", GetStyle(color: Color.green));
                else
                {
                    EditorGUILayout.LabelField($"Found {duplicateGroups.Count} duplicate group{(duplicateGroups.Count != 1 ? "s" : "")}.", GetStyle(color: Color.yellow));
                    EditorGUILayout.Space();
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    foreach (var group in duplicateGroups)
                    {
                        foreach (var assetPath in group)
                        {
                            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                            EditorGUILayout.ObjectField(asset, typeof(Object), false);
                        }
                    }
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        void DrawIssueFoldout(string label, List<(string path, string issue)> issues, string prefix, ref bool show)
        {
            show = EditorGUILayout.Foldout(show, $"{label} ({issues.Count})", true, EditorStyles.foldoutHeader);
            if (show) { EditorGUILayout.Space(); foreach (var item in issues) DrawIssueRow(item.path, item.issue.Substring(prefix.Length)); }
            EditorGUILayout.Space();
        }

        void AnalyzeCanvas()
        {
            Graphic[] graphics = Selected.GetComponentsInChildren<Graphic>(true);
            int count = graphics.Length;

            if (count < 50) SetStatus(1, $"Graphics count: {count}.\nSafe for frequent changes. Canvas rebuild cost is low.");
            else if (count < 150) SetStatus(3, $"Graphics count: {count}.\nAcceptable for occasional updates. Avoid per-frame changes.");
            else SetStatus(2, $"Graphics count: {count}.\nNOT suitable for frequent updates.\nRecommended: keep mostly static or split into smaller canvases.");
        }

        void AnalyzeScene()
        {
            scanned = true;

            var meshFilters = FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var skinnedRenderers = FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            totalMeshes = meshFilters.Length + skinnedRenderers.Length;
            totalVertices = totalTriangles = 0;

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                totalVertices += mf.sharedMesh.vertexCount;
                totalTriangles += mf.sharedMesh.triangles.Length / 3;
            }
            foreach (var sr in skinnedRenderers)
            {
                if (sr.sharedMesh == null) continue;
                totalVertices += sr.sharedMesh.vertexCount;
                totalTriangles += sr.sharedMesh.triangles.Length / 3;
            }

            var materials = new HashSet<Material>();
            var textures = new HashSet<Texture>();

            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    materials.Add(mat);
                    foreach (var prop in mat.GetTexturePropertyNames())
                    {
                        var tex = mat.GetTexture(prop);
                        if (tex != null) textures.Add(tex);
                    }
                }
            }

            totalMaterials = materials.Count;
            totalTextures = textures.Count;
            totalTextureMemory = 0;
            foreach (var tex in textures)
                totalTextureMemory += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);

            totalLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            totalCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            totalParticles = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            totalAudio = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            totalAnimators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            totalColliders = FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            totalRigidbodies = FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        void AnalyzeProject()
        {
            scanned = true;
            projectIssues.Clear();

            foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) continue;
                int tris = mesh.triangles.Length / 3;
                if (tris > 50000) projectIssues.Add((path, $"[Mesh] {tris:N0} triangles — consider LODs or decimation"));
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) continue;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                if (tex.width > 4096 || tex.height > 4096)
                    projectIssues.Add((path, $"[Texture] {tex.width}x{tex.height} — exceeds 4096px, high GPU memory cost"));
                if (!IsPowerOfTwo(tex.width) || !IsPowerOfTwo(tex.height))
                    projectIssues.Add((path, $"[Texture] {tex.width}x{tex.height} — non-power-of-two, prevents GPU compression"));
                if (importer.isReadable)
                    projectIssues.Add((path, "[Texture] Read/Write enabled — doubles memory usage at runtime"));
                if (!importer.mipmapEnabled && importer.textureType != TextureImporterType.Sprite && importer.textureType != TextureImporterType.GUI)
                    projectIssues.Add((path, "[Texture] Mipmaps disabled — causes aliasing and GPU overdraw in 3D"));
            }

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;
                var settings = importer.defaultSampleSettings;
                long fileSize = new FileInfo(Path.GetFullPath(path)).Length;

                if (settings.compressionFormat == AudioCompressionFormat.PCM)
                    projectIssues.Add((path, "[Audio] Uncompressed PCM — use Vorbis for music/long clips, ADPCM for short SFX"));
                if (settings.loadType == AudioClipLoadType.DecompressOnLoad && fileSize > 200 * 1024)
                    projectIssues.Add((path, $"[Audio] Decompress On Load on a {FormatBytes(fileSize)} clip — use Compressed In Memory or Streaming"));
                if (!importer.forceToMono && fileSize > 500 * 1024)
                    projectIssues.Add((path, $"[Audio] Stereo clip ({FormatBytes(fileSize)}) — enable Force To Mono for non-spatial audio to halve memory"));
            }

            if (projectIssues.Count == 0) SetStatus(1, "No issues found. Project looks clean.");
            else SetStatus(3, $"Found {projectIssues.Count} asset issue{(projectIssues.Count != 1 ? "s" : "")}.");
        }

        void AnalyzeDuplicates()
        {
            scanned = true;
            duplicateGroups.Clear();

            var hashToAssets = new Dictionary<string, List<string>>();
            foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets" }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath)) continue;
                if (AssetDatabase.IsValidFolder(assetPath)) continue;
                if (assetPath.EndsWith(".meta")) continue;

                string fullPath = Path.GetFullPath(assetPath);
                if (!File.Exists(fullPath)) continue;

                long fileSize = new FileInfo(fullPath).Length;
                if (fileSize == 0 || fileSize > 50 * 1024 * 1024) continue;

                string hash = ComputeMD5(fullPath);
                if (string.IsNullOrEmpty(hash)) continue;

                if (!hashToAssets.ContainsKey(hash)) hashToAssets[hash] = new List<string>();
                hashToAssets[hash].Add(assetPath);
            }

            foreach (var kvp in hashToAssets)
                if (kvp.Value.Count >= 2)
                    duplicateGroups.Add(kvp.Value.OrderBy(p => p).ToList());

            duplicateGroups = duplicateGroups
                .OrderByDescending(g => g.Count)
                .ThenBy(g => Path.GetFileName(g[0]))
                .ToList();
        }

        string ComputeMD5(string filePath)
        {
            try
            {
                using (var md5 = MD5.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch { return null; }
        }

        bool IsPowerOfTwo(int x) => x > 0 && (x & (x - 1)) == 0;

        void DrawSection(string title)
        {
            EditorGUILayout.LabelField(title, GetStyle(alignment: TextAnchor.MiddleCenter));
            EditorGUILayout.Space(2);
        }

        void DrawRow(string label, string value)
        {
            string extraSpaces = "          ";
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(extraSpaces + label, GetStyle(alignment: TextAnchor.MiddleLeft));
            EditorGUILayout.LabelField(value + extraSpaces, GetStyle(alignment: TextAnchor.MiddleRight));
            EditorGUILayout.EndHorizontal();
        }

        void DrawIssueRow(string assetPath, string issue)
        {
            int fontSize = 12;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            if (GUILayout.Button(Path.GetFileName(assetPath), GetStyle(fontSize: fontSize, alignment: TextAnchor.MiddleLeft), GUILayout.Width(180)))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset != null) { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
            }
            EditorGUILayout.LabelField(issue, GetStyle(fontSize: fontSize, alignment: TextAnchor.MiddleLeft));
            EditorGUILayout.EndHorizontal();
        }

        string FormatBytes(long bytes)
        {
            if (bytes >= 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
            if (bytes >= 1024) return $"{bytes / 1024f:F1} KB";
            return $"{bytes} B";
        }
    }
}
#endif