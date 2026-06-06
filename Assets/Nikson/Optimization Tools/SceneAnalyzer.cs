#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using static Nikson.OptimizationHub;

namespace Nikson
{
    public class SceneAnalyzer : ScriptableObject
    {
        // Stats results
        int totalMeshes, totalVertices, totalTriangles, totalMaterials, totalTextures;
        long totalTextureMemory;
        int totalLights, totalCameras, totalParticleSystems, totalAudioSources, totalAnimators, totalColliders, totalRigidbodies;

        // Top offenders
        List<(string name, int tris)> topMeshes = new List<(string, int)>();
        List<(string name, long bytes)> topTextures = new List<(string, long)>();

        bool scanned = false;
        Vector2 scroll;

        public void DrawGUI() => OnGUI();

        void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Scans the current scene and reports geometry, texture memory, and component counts. " +
                "Use this to spot heavy meshes, oversized textures, and component bloat.",
                GetStyle());

            EditorGUILayout.Space();
            if (CenteredButton("Analyze")) Analyze();
            EditorGUILayout.Space();

            if (!scanned) return;

            scroll = EditorGUILayout.BeginScrollView(scroll);

            // Geometry
            DrawHeader("Geometry");
            DrawStat("Meshes", totalMeshes);
            DrawStat("Vertices", totalVertices);
            DrawStat("Triangles", totalTriangles);
            EditorGUILayout.Space(18);

            // Rendering
            DrawHeader("Rendering");
            DrawStat("Unique Materials", totalMaterials);
            DrawStat("Unique Textures", totalTextures);
            DrawStat("Texture Memory (est.)", FormatBytes(totalTextureMemory));
            DrawStat("Lights", totalLights);
            DrawStat("Cameras", totalCameras);
            DrawStat("Particle Systems", totalParticleSystems);
            EditorGUILayout.Space(18);

            // Components
            DrawHeader("Components");
            DrawStat("Audio Sources", totalAudioSources);
            DrawStat("Animators", totalAnimators);
            DrawStat("Colliders", totalColliders);
            DrawStat("Rigidbodies", totalRigidbodies);
            EditorGUILayout.Space(18);

            // Top meshes by triangle count
            if (topMeshes.Count > 0)
            {
                DrawHeader("Top 5 Meshes by Triangle Count");
                foreach (var m in topMeshes)
                    DrawStat(m.name, m.tris.ToString("N0") + " tris");
            }
            EditorGUILayout.Space(18);

            // Top textures by memory
            if (topTextures.Count > 0)
            {
                DrawHeader("Top 5 Textures by Memory");
                foreach (var t in topTextures)
                    DrawStat(t.name, FormatBytes(t.bytes));
            }

            EditorGUILayout.EndScrollView();
        }

        void Analyze()
        {
            scanned = true;
            topMeshes.Clear();
            topTextures.Clear();

            var allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Geometry
            var meshFilters = FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var skinnedRenderers = FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            totalMeshes = meshFilters.Length + skinnedRenderers.Length;
            totalVertices = 0;
            totalTriangles = 0;

            List<(string name, int tris)> meshData = new List<(string, int)>();

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                totalVertices += mf.sharedMesh.vertexCount;
                int tris = mf.sharedMesh.triangles.Length / 3;
                totalTriangles += tris;
                meshData.Add((mf.gameObject.name + " (" + mf.sharedMesh.name + ")", tris));
            }

            foreach (var sr in skinnedRenderers)
            {
                if (sr.sharedMesh == null) continue;
                totalVertices += sr.sharedMesh.vertexCount;
                int tris = sr.sharedMesh.triangles.Length / 3;
                totalTriangles += tris;
                meshData.Add((sr.gameObject.name + " (" + sr.sharedMesh.name + ")", tris));
            }

            topMeshes = meshData.OrderByDescending(m => m.tris).Take(5).ToList();

            // Materials and textures
            HashSet<Material> materials = new HashSet<Material>();
            HashSet<Texture> textures = new HashSet<Texture>();

            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    materials.Add(mat);
                    foreach (var texName in mat.GetTexturePropertyNames())
                    {
                        var tex = mat.GetTexture(texName);
                        if (tex != null) textures.Add(tex);
                    }
                }

            totalMaterials = materials.Count;
            totalTextures = textures.Count;

            // Texture memory estimate
            List<(string name, long bytes)> texData = new List<(string, long)>();
            totalTextureMemory = 0;
            foreach (var tex in textures)
            {
                long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
                totalTextureMemory += bytes;
                texData.Add((tex.name, bytes));
            }
            topTextures = texData.OrderByDescending(t => t.bytes).Take(5).ToList();

            // Components
            totalLights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            totalCameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            totalParticleSystems = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            totalAudioSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            totalAnimators = FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            totalColliders = FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            totalRigidbodies = FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        }

        void DrawHeader(string title) => EditorGUILayout.LabelField(title, GetStyle());

        void DrawStat(string label, int value) => DrawStat(label, value.ToString("N0"));

        void DrawStat(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("               " + label, GetStyle(alignment: TextAnchor.MiddleLeft));
            EditorGUILayout.LabelField(value + "               ", GetStyle(alignment: TextAnchor.MiddleRight));
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