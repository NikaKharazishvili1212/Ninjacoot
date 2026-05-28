#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nikson
{
    // ---------------------------------------------------------------------------
    //  Shared data container used by both combiners
    // ---------------------------------------------------------------------------
    public class UnifiedCombineData
    {
        public Mesh mesh;
        public int subMeshIndex;
        public Transform transform;
        // Skinned-mesh only (null for regular meshes)
        public Transform[] bones;
        public BoneWeight[] boneWeights;
        public Matrix4x4[] bindPoses;
    }

    // ---------------------------------------------------------------------------
    //  Vertex-key structs for duplicate-vertex detection
    // ---------------------------------------------------------------------------
    public struct MeshVertexKey
    {
        Vector3 position;
        Vector3 normal;
        Vector2 uv;

        public MeshVertexKey(Vector3 pos, Vector3 norm, Vector2 uv)
        {
            position = Round(pos);
            normal = Round(norm);
            this.uv = Round(uv);
        }

        static Vector3 Round(Vector3 v) => new Vector3(
            Mathf.Round(v.x * 10000f) / 10000f,
            Mathf.Round(v.y * 10000f) / 10000f,
            Mathf.Round(v.z * 10000f) / 10000f);

        static Vector2 Round(Vector2 v) => new Vector2(
            Mathf.Round(v.x * 10000f) / 10000f,
            Mathf.Round(v.y * 10000f) / 10000f);

        public override bool Equals(object obj)
        {
            if (!(obj is MeshVertexKey)) return false;
            MeshVertexKey o = (MeshVertexKey)obj;
            return position == o.position && normal == o.normal && uv == o.uv;
        }

        public override int GetHashCode() =>
            position.GetHashCode() ^ (normal.GetHashCode() << 2) ^ (uv.GetHashCode() >> 2);
    }

    public struct SkinnedVertexKey
    {
        Vector3 position;
        Vector3 normal;
        Vector2 uv;
        int bone0, bone1, bone2, bone3;
        float weight0, weight1, weight2, weight3;

        public SkinnedVertexKey(Vector3 pos, Vector3 norm, Vector2 uv, BoneWeight w)
        {
            position = Round(pos);
            normal = Round(norm);
            this.uv = Round(uv);
            bone0 = w.boneIndex0; bone1 = w.boneIndex1;
            bone2 = w.boneIndex2; bone3 = w.boneIndex3;
            weight0 = w.weight0; weight1 = w.weight1;
            weight2 = w.weight2; weight3 = w.weight3;
        }

        static Vector3 Round(Vector3 v) => new Vector3(
            Mathf.Round(v.x * 10000f) / 10000f,
            Mathf.Round(v.y * 10000f) / 10000f,
            Mathf.Round(v.z * 10000f) / 10000f);

        static Vector2 Round(Vector2 v) => new Vector2(
            Mathf.Round(v.x * 10000f) / 10000f,
            Mathf.Round(v.y * 10000f) / 10000f);

        public override bool Equals(object obj)
        {
            if (!(obj is SkinnedVertexKey)) return false;
            SkinnedVertexKey o = (SkinnedVertexKey)obj;
            return position == o.position && normal == o.normal && uv == o.uv &&
                   bone0 == o.bone0 && bone1 == o.bone1 &&
                   bone2 == o.bone2 && bone3 == o.bone3 &&
                   weight0 == o.weight0 && weight1 == o.weight1 &&
                   weight2 == o.weight2 && weight3 == o.weight3;
        }

        public override int GetHashCode() =>
            position.GetHashCode() ^ (normal.GetHashCode() << 2) ^ (uv.GetHashCode() >> 2) ^
            (bone0 << 24) ^ (bone1 << 16) ^ (bone2 << 8) ^ bone3;
    }

    // ---------------------------------------------------------------------------
    //  Texture-atlas helpers
    // ---------------------------------------------------------------------------
    public class FreeSpace
    {
        public int x, y, width, height;
        public FreeSpace(int x, int y, int w, int h) { this.x = x; this.y = y; width = w; height = h; }
    }

    public struct TexturePlacement
    {
        public Texture2D texture;
        public int originalIndex;
        public int x, y;
    }

    // ---------------------------------------------------------------------------
    //  Base window — shared atlas generation & file utilities
    // ---------------------------------------------------------------------------
    public abstract class MeshCombinerBase : EditorWindow
    {
        protected const string ATLAS_NAME = "Atlas";
        protected const int MAX_ATLAS_SIZE = 4096;

        // ------------------------------------------------------------------
        //  Texture atlas
        // ------------------------------------------------------------------
        protected Material GenerateTextureAtlas(
            Dictionary<Material, List<UnifiedCombineData>> materialGroups,
            string normalizedPath,
            string atlasName,
            out Rect[] uvRects)
        {
            // Collect textures
            List<Texture2D> textureList = new List<Texture2D>();
            Dictionary<Material, int> matToTexIndex = new Dictionary<Material, int>();
            int texIndex = 0;

            foreach (var matGroup in materialGroups)
            {
                Texture2D mainTex = matGroup.Key.mainTexture as Texture2D;
                if (mainTex == null)
                {
                    mainTex = new Texture2D(256, 256);
                    Color[] pixels = Enumerable.Repeat(Color.white, 256 * 256).ToArray();
                    mainTex.SetPixels(pixels);
                    mainTex.Apply();
                }
                else
                    mainTex = DuplicateTexture(mainTex);

                matToTexIndex[matGroup.Key] = texIndex++;
                textureList.Add(mainTex);
            }

            Texture2D[] textures = textureList.ToArray();

            // Sort by area (largest first) for better packing
            int[] sorted = Enumerable.Range(0, textures.Length).ToArray();
            System.Array.Sort(sorted, (a, b) =>
            {
                int da = textures[a].width * textures[a].height;
                int db = textures[b].width * textures[b].height;
                return da != db ? db.CompareTo(da) : textures[b].height.CompareTo(textures[a].height);
            });

            // Bin packing
            List<TexturePlacement> placements = new List<TexturePlacement>();
            List<FreeSpace> freeSpaces = new List<FreeSpace>();
            uvRects = new Rect[textures.Length];
            freeSpaces.Add(new FreeSpace(0, 0, MAX_ATLAS_SIZE, MAX_ATLAS_SIZE));

            int atlasW = 0, atlasH = 0;

            foreach (int i in sorted)
            {
                Texture2D tex = textures[i];
                FreeSpace bestSpace = null;
                int bestWaste = int.MaxValue, bestScore = int.MaxValue;

                foreach (var space in freeSpaces)
                {
                    if (space.width < tex.width || space.height < tex.height) continue;

                    int waste = (space.width - tex.width) + (space.height - tex.height);
                    int newW = Mathf.Max(atlasW, space.x + tex.width);
                    int newH = Mathf.Max(atlasH, space.y + tex.height);
                    int dW = newW - atlasW, dH = newH - atlasH;

                    int score;
                    if (dW == 0 && dH == 0) score = 0;
                    else if (atlasW < atlasH) score = dH * 1000 + dW;
                    else if (atlasH < atlasW) score = dW * 1000 + dH;
                    else score = dW + dH;

                    if (bestSpace == null || score < bestScore || (score == bestScore && waste < bestWaste))
                    { bestSpace = space; bestWaste = waste; bestScore = score; }
                }

                if (bestSpace == null)
                {
                    Debug.LogError($"Failed to pack texture {tex.width}x{tex.height} into atlas!");
                    uvRects = null;
                    return null;
                }

                int px = bestSpace.x, py = bestSpace.y;
                placements.Add(new TexturePlacement { texture = tex, originalIndex = i, x = px, y = py });

                atlasW = Mathf.Max(atlasW, px + tex.width);
                atlasH = Mathf.Max(atlasH, py + tex.height);
                freeSpaces.Remove(bestSpace);

                List<FreeSpace> newSpaces = new List<FreeSpace>();
                if (bestSpace.width > tex.width) newSpaces.Add(new FreeSpace(px + tex.width, py, bestSpace.width - tex.width, tex.height));
                if (bestSpace.height > tex.height) newSpaces.Add(new FreeSpace(px, py + tex.height, tex.width, bestSpace.height - tex.height));
                if (bestSpace.width > tex.width && bestSpace.height > tex.height)
                    newSpaces.Add(new FreeSpace(px + tex.width, py + tex.height, bestSpace.width - tex.width, bestSpace.height - tex.height));

                foreach (var ns in newSpaces)
                    if (ns.width > 0 && ns.height > 0)
                        AddAndMergeSpace(freeSpaces, ns, placements);
            }

            // Decide whether to snap to power-of-two
            int potW = Mathf.NextPowerOfTwo(atlasW);
            int potH = Mathf.NextPowerOfTwo(atlasH);
            atlasW = ((potW - atlasW) / (float)atlasW > 0.25f) ? Mathf.Min(atlasW, MAX_ATLAS_SIZE) : Mathf.Min(potW, MAX_ATLAS_SIZE);
            atlasH = ((potH - atlasH) / (float)atlasH > 0.25f) ? Mathf.Min(atlasH, MAX_ATLAS_SIZE) : Mathf.Min(potH, MAX_ATLAS_SIZE);

            if (atlasW > MAX_ATLAS_SIZE || atlasH > MAX_ATLAS_SIZE)
            {
                Debug.LogError($"Atlas too large! Required: {atlasW}x{atlasH}, maximum: {MAX_ATLAS_SIZE}x{MAX_ATLAS_SIZE}");
                uvRects = null;
                return null;
            }

            // Blit into final atlas texture
            Texture2D atlas = new Texture2D(atlasW, atlasH, TextureFormat.RGBA32, true);
            atlas.SetPixels(Enumerable.Repeat(Color.clear, atlasW * atlasH).ToArray());

            foreach (var p in placements)
            {
                atlas.SetPixels(p.x, p.y, p.texture.width, p.texture.height, p.texture.GetPixels());
                uvRects[p.originalIndex] = new Rect(
                    (float)p.x / atlasW, (float)p.y / atlasH,
                    (float)p.texture.width / atlasW, (float)p.texture.height / atlasH);
            }
            atlas.Apply(true);

            // Save atlas PNG
            string atlasPath = GetUniquePath(normalizedPath, atlasName, ".png");
            File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
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

            // Build atlas material using the first material's shader
            Material firstMat = materialGroups.Keys.First();
            Material atlasMat = new Material(firstMat.shader);
            if (atlasMat.HasProperty("_BaseMap")) atlasMat.SetTexture("_BaseMap", savedAtlas); // URP
            else if (atlasMat.HasProperty("_MainTex")) atlasMat.SetTexture("_MainTex", savedAtlas); // Standard/Built-in
            else atlasMat.mainTexture = savedAtlas;           // Fallback

            atlasMat.name = atlasName;
            string matPath = GetUniquePath(normalizedPath, atlasName, ".mat");
            AssetDatabase.CreateAsset(atlasMat, matPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created Atlas: {atlasPath}   |   Size: {atlasW}x{atlasH}, Packed: {textures.Length} textures");
            return atlasMat;
        }

        // ------------------------------------------------------------------
        //  Atlas packing helpers
        // ------------------------------------------------------------------
        void AddAndMergeSpace(List<FreeSpace> spaces, FreeSpace newSpace, List<TexturePlacement> placements)
        {
            foreach (var p in placements)
                if (SpacesOverlap(newSpace, p)) return;
            foreach (var s in spaces)
                if (SpaceContainedIn(newSpace, s)) return;
            spaces.RemoveAll(s => SpaceContainedIn(s, newSpace));
            spaces.Add(newSpace);
        }

        bool SpacesOverlap(FreeSpace s, TexturePlacement p) =>
            !(s.x >= p.x + p.texture.width || s.x + s.width <= p.x ||
              s.y >= p.y + p.texture.height || s.y + s.height <= p.y);

        bool SpaceContainedIn(FreeSpace inner, FreeSpace outer) =>
            inner.x >= outer.x && inner.y >= outer.y &&
            inner.x + inner.width <= outer.x + outer.width &&
            inner.y + inner.height <= outer.y + outer.height;

        // ------------------------------------------------------------------
        //  Texture utilities
        // ------------------------------------------------------------------
        protected Texture2D DuplicateTexture(Texture2D source)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D readable = new Texture2D(source.width, source.height);
            readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readable.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return readable;
        }

        // ------------------------------------------------------------------
        //  File / path utilities
        // ------------------------------------------------------------------
        protected string GetUniquePath(string folder, string name, string ext)
        {
            string path = Path.Combine(folder, name + ext);
            int n = 1;
            while (File.Exists(path) || AssetDatabase.LoadAssetAtPath<Object>(path) != null)
                path = Path.Combine(folder, name + n++ + ext);
            return path;
        }

        protected string NormalizePath(string raw)
        {
            string p = raw.Replace("\\", "/");
            if (!p.EndsWith("/")) p += "/";
            return p;
        }

        protected void EnsureDirectory(string normalizedPath)
        {
            if (!Directory.Exists(normalizedPath))
            {
                Directory.CreateDirectory(normalizedPath);
                AssetDatabase.Refresh();
            }
        }

        // ------------------------------------------------------------------
        //  Validation helpers (shared UI warnings)
        // ------------------------------------------------------------------
        protected static void WarnIfLargeMesh(int vertexCount)
        {
            if (vertexCount > 65535)
                Debug.LogWarning($"Combined mesh has {vertexCount} vertices (> 65535). " +
                    "Unity will automatically use 32-bit index format, which is not supported on some older platforms.");
        }
    }
}
#endif