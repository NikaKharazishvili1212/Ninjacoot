#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Nikson
{
    public class MeshSimplifier : EditorWindow
    {
        const string MESH_NAME = "SimplifiedMesh";

        GameObject parentObject;
        string savePath = "Nikson/Generated/";
        int qualityPercent = 100;
        bool applyToMesh = false;

        GameObject targetMesh;
        Mesh optimizedMesh;
        Mesh originalMesh; // Clone used for optimization
        Mesh originalSharedMesh; // Reference to the original shared mesh
        GameObject lastTarget;

        [MenuItem("Nikson/Optimization/2. Mesh Simplifier")]
        public static void ShowWindow() => GetWindow<MeshSimplifier>("Mesh Simplifier");

        void OnGUI()
        {
            GUILayout.Label("Mesh Simplifier Settings", EditorStyles.boldLabel);

            parentObject = (GameObject)EditorGUILayout.ObjectField("Parent Object", parentObject, typeof(GameObject), true);
            savePath = EditorGUILayout.TextField("Save Path", savePath);
            qualityPercent = EditorGUILayout.IntSlider("Quality Percent", qualityPercent, 1, 100);
            applyToMesh = EditorGUILayout.Toggle("Apply to Mesh", applyToMesh);
            EditorGUILayout.HelpBox(
                "When enabled: Simplified mesh replaces the original in the scene\n" +
                "When disabled: Only saves the simplified mesh as an asset",
                MessageType.Info);

            EditorGUILayout.Space();

            GUI.enabled = parentObject != null;
            if (GUILayout.Button("Preview", GUILayout.Height(30))) Preview();
            if (GUILayout.Button("Generate Simplified Mesh", GUILayout.Height(30))) Generate();
            GUI.enabled = true;
        }

        void Preview()
        {
            // Find target mesh in children
            if (!targetMesh)
            {
                var meshFilter = parentObject.GetComponentInChildren<MeshFilter>();
                var skinnedMeshRenderer = parentObject.GetComponentInChildren<SkinnedMeshRenderer>();
                if (meshFilter != null) targetMesh = meshFilter.gameObject;
                else if (skinnedMeshRenderer != null) targetMesh = skinnedMeshRenderer.gameObject;
                else
                {
                    Debug.LogWarning("No MeshFilter or SkinnedMeshRenderer found in children.");
                    return;
                }
            }

            if (!ValidateTarget()) return;
            var sharedMesh = GetSharedMesh(targetMesh);
            if (originalSharedMesh == null || targetMesh != lastTarget)
            {
                lastTarget = targetMesh;
                originalSharedMesh = sharedMesh;
                originalMesh = Instantiate(originalSharedMesh);
            }
            float quality = Mathf.Clamp01(qualityPercent / 100f);
            var meshSimplifier = new UnityMeshSimplifier();
            meshSimplifier.Initialize(originalMesh);
            meshSimplifier.SimplifyMesh(quality);
            optimizedMesh = meshSimplifier.ToMesh();
            SetSharedMesh(targetMesh, optimizedMesh);
        }

        void Generate()
        {
            if (!ValidateTarget()) return;

            // Get or create the mesh to simplify
            var sharedMesh = GetSharedMesh(targetMesh);
            if (originalSharedMesh == null || targetMesh != lastTarget)
            {
                lastTarget = targetMesh;
                originalSharedMesh = sharedMesh;
                originalMesh = Instantiate(originalSharedMesh);
            }

            // Simplify the mesh
            float quality = Mathf.Clamp01(qualityPercent / 100f);
            var meshSimplifier = new UnityMeshSimplifier();
            meshSimplifier.Initialize(originalMesh);
            meshSimplifier.SimplifyMesh(quality);
            Mesh meshToSave = meshSimplifier.ToMesh();

            string normalizedPath = savePath.Replace("\\", "/");
            if (!normalizedPath.StartsWith("Assets/")) normalizedPath = "Assets/" + normalizedPath;
            if (!normalizedPath.EndsWith("/")) normalizedPath += "/";

            if (!Directory.Exists(normalizedPath))
            {
                Directory.CreateDirectory(normalizedPath);
                AssetDatabase.Refresh();
            }

            string meshPath = GetUniquePath(normalizedPath, MESH_NAME, ".asset");
            meshToSave.name = Path.GetFileNameWithoutExtension(meshPath);

            AssetDatabase.CreateAsset(meshToSave, meshPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Saved Mesh: {meshPath} (with {qualityPercent}% quality){(applyToMesh ? $"   |   Applied simplified mesh to {targetMesh.name}" : string.Empty)}");

            // Apply or reset based on user choice
            if (applyToMesh) SetSharedMesh(targetMesh, meshToSave);
            else if (optimizedMesh != null && originalSharedMesh != null) SetSharedMesh(targetMesh, originalSharedMesh);

            EditorUtility.SetDirty(parentObject);
            parentObject = null; // Reset UI field to prevent accidental regeneration
        }

        string GetUniquePath(string folder, string name, string ext)
        {
            string path = Path.Combine(folder, name + ext);
            int index = 1;
            while (File.Exists(path) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null) path = Path.Combine(folder, name + index++ + ext);
            return path;
        }

        bool ValidateTarget()
        {
            if (targetMesh == null)
            {
                Debug.LogError("No target GameObject assigned.");
                return false;
            }
            var sharedMesh = GetSharedMesh(targetMesh);
            if (sharedMesh == null)
            {
                Debug.LogError("The GameObject must have a MeshFilter or SkinnedMeshRenderer with a valid mesh.");
                return false;
            }
            return true;
        }

        Mesh GetSharedMesh(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null) return mf.sharedMesh;
            var sk = go.GetComponent<SkinnedMeshRenderer>();
            if (sk != null) return sk.sharedMesh;
            return null;
        }

        void SetSharedMesh(GameObject go, Mesh mesh)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null) { mf.sharedMesh = mesh; return; }
            var sk = go.GetComponent<SkinnedMeshRenderer>();
            if (sk != null) sk.sharedMesh = mesh;
        }
    }

    public struct BlendShape
    {
        public string ShapeName;
        public BlendShapeFrame[] Frames;
        public BlendShape(string shapeName, BlendShapeFrame[] frames)
        {
            this.ShapeName = shapeName;
            this.Frames = frames;
        }
    }

    public struct BlendShapeFrame
    {
        public float FrameWeight;
        public Vector3[] DeltaVertices;
        public Vector3[] DeltaNormals;
        public Vector3[] DeltaTangents;
        public BlendShapeFrame(float frameWeight, Vector3[] deltaVertices, Vector3[] deltaNormals, Vector3[] deltaTangents)
        {
            this.FrameWeight = frameWeight;
            this.DeltaVertices = deltaVertices;
            this.DeltaNormals = deltaNormals;
            this.DeltaTangents = deltaTangents;
        }
    }

    public sealed class UnityMeshSimplifier
    {
        bool preserveBorderEdges = false;
        bool preserveUVSeamEdges = false;
        bool preserveUVFoldoverEdges = false;
        bool enableSmartLink = true;
        int maxIterationCount = 100;
        double agressiveness = 7.0;
        double vertexLinkDistanceSqr = double.Epsilon;
        int subMeshCount = 0;
        int[] subMeshOffsets = null;
        ResizableArray<Triangle> triangles = null;
        ResizableArray<Vertex> vertices = null;
        ResizableArray<Ref> refs = null;
        ResizableArray<Vector3> vertNormals = null;
        ResizableArray<Vector4> vertTangents = null;
        UVChannels<Vector2> vertUV2D = null;
        UVChannels<Vector3> vertUV3D = null;
        UVChannels<Vector4> vertUV4D = null;
        ResizableArray<Color> vertColors = null;
        ResizableArray<BoneWeight> vertBoneWeights = null;
        ResizableArray<BlendShapeContainer> blendShapes = null;
        Matrix4x4[] bindposes = null;
        double[] errArr = new double[3];
        int[] attributeIndexArr = new int[3];

        public bool PreserveBorderEdges { get { return preserveBorderEdges; } set { preserveBorderEdges = value; } }
        public bool PreserveUVSeamEdges { get { return preserveUVSeamEdges; } set { preserveUVSeamEdges = value; } }
        public bool PreserveUVFoldoverEdges { get { return preserveUVFoldoverEdges; } set { preserveUVFoldoverEdges = value; } }
        public bool EnableSmartLink { get { return enableSmartLink; } set { enableSmartLink = value; } }
        public int MaxIterationCount { get { return maxIterationCount; } set { maxIterationCount = value; } }
        public double Agressiveness { get { return agressiveness; } set { agressiveness = value; } }
        public double VertexLinkDistance { get { return Math.Sqrt(vertexLinkDistanceSqr); } set { vertexLinkDistanceSqr = (value > double.Epsilon ? value * value : double.Epsilon); } }

        public Vector3[] Vertices
        {
            get
            {
                int vertexCount = this.vertices.Length;
                var vertices = new Vector3[vertexCount];
                var vertArr = this.vertices.Data;
                for (int i = 0; i < vertexCount; i++)
                    vertices[i] = (Vector3)vertArr[i].p;
                return vertices;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));
                bindposes = null;
                vertices.Resize(value.Length);
                var vertArr = vertices.Data;
                for (int i = 0; i < value.Length; i++)
                    vertArr[i] = new Vertex(value[i]);
            }
        }

        public int SubMeshCount { get { return subMeshCount; } }
        public int BlendShapeCount { get { return (blendShapes != null ? blendShapes.Length : 0); } }
        public Vector3[] Normals { get { return (vertNormals != null ? vertNormals.Data : null); } set { InitializeVertexAttribute(value, ref vertNormals, "normals"); } }
        public Vector4[] Tangents { get { return (vertTangents != null ? vertTangents.Data : null); } set { InitializeVertexAttribute(value, ref vertTangents, "tangents"); } }
        public Vector2[] UV1 { get { return GetUVs2D(0); } set { SetUVs(0, value); } }
        public Vector2[] UV2 { get { return GetUVs2D(1); } set { SetUVs(1, value); } }
        public Vector2[] UV3 { get { return GetUVs2D(2); } set { SetUVs(2, value); } }
        public Vector2[] UV4 { get { return GetUVs2D(3); } set { SetUVs(3, value); } }
        public Color[] Colors { get { return (vertColors != null ? vertColors.Data : null); } set { InitializeVertexAttribute(value, ref vertColors, "colors"); } }
        public BoneWeight[] BoneWeights { get { return (vertBoneWeights != null ? vertBoneWeights.Data : null); } set { InitializeVertexAttribute(value, ref vertBoneWeights, "boneWeights"); } }

        public UnityMeshSimplifier()
        {
            triangles = new ResizableArray<Triangle>(0);
            vertices = new ResizableArray<Vertex>(0);
            refs = new ResizableArray<Ref>(0);
        }

        public UnityMeshSimplifier(Mesh mesh) : this() { if (mesh != null) Initialize(mesh); }

        struct Triangle
        {
            public int v0;
            public int v1;
            public int v2;
            public int subMeshIndex;
            public int va0;
            public int va1;
            public int va2;
            public double err0;
            public double err1;
            public double err2;
            public double err3;
            public bool deleted;
            public bool dirty;
            public Vector3d n;

            public int this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return (index == 0 ? v0 : (index == 1 ? v1 : v2)); }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    switch (index)
                    {
                        case 0: v0 = value; break;
                        case 1: v1 = value; break;
                        case 2: v2 = value; break;
                        default: throw new IndexOutOfRangeException();
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Triangle(int v0, int v1, int v2, int subMeshIndex)
            {
                this.v0 = v0;
                this.v1 = v1;
                this.v2 = v2;
                this.subMeshIndex = subMeshIndex;
                this.va0 = v0;
                this.va1 = v1;
                this.va2 = v2;
                err0 = err1 = err2 = err3 = 0;
                deleted = dirty = false;
                n = new Vector3d();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GetAttributeIndices(int[] attributeIndices)
            {
                attributeIndices[0] = va0;
                attributeIndices[1] = va1;
                attributeIndices[2] = va2;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void SetAttributeIndex(int index, int value)
            {
                switch (index)
                {
                    case 0: va0 = value; break;
                    case 1: va1 = value; break;
                    case 2: va2 = value; break;
                    default: throw new IndexOutOfRangeException();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void GetErrors(double[] err)
            {
                err[0] = err0;
                err[1] = err1;
                err[2] = err2;
            }
        }

        struct Vertex
        {
            public Vector3d p;
            public int tstart;
            public int tcount;
            public SymmetricMatrix q;
            public bool borderEdge;
            public bool uvSeamEdge;
            public bool uvFoldoverEdge;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vertex(Vector3d p)
            {
                this.p = p;
                this.tstart = 0;
                this.tcount = 0;
                this.q = new SymmetricMatrix();
                this.borderEdge = true;
                this.uvSeamEdge = false;
                this.uvFoldoverEdge = false;
            }
        }

        struct Ref
        {
            public int tid;
            public int tvertex;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Set(int tid, int tvertex)
            {
                this.tid = tid;
                this.tvertex = tvertex;
            }
        }

        class UVChannels<TVec>
        {
            ResizableArray<TVec>[] channels = null;
            TVec[][] channelsData = null;

            public TVec[][] Data
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    for (int i = 0; i < 4; i++)
                    {
                        if (channels[i] != null) channelsData[i] = channels[i].Data;
                        else channelsData[i] = null;
                    }
                    return channelsData;
                }
            }

            public ResizableArray<TVec> this[int index]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return channels[index]; }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set { channels[index] = value; }
            }

            public UVChannels()
            {
                channels = new ResizableArray<TVec>[4];
                channelsData = new TVec[4][];
            }

            public void Resize(int capacity, bool trimExess = false)
            {
                for (int i = 0; i < 4; i++)
                    if (channels[i] != null)
                        channels[i].Resize(capacity, trimExess);
            }
        }

        class BlendShapeContainer
        {
            string shapeName;
            BlendShapeFrameContainer[] frames;

            public BlendShapeContainer(BlendShape blendShape)
            {
                shapeName = blendShape.ShapeName;
                frames = new BlendShapeFrameContainer[blendShape.Frames.Length];
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = new BlendShapeFrameContainer(blendShape.Frames[i]);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void MoveVertexElement(int dst, int src)
            {
                for (int i = 0; i < frames.Length; i++)
                    frames[i].MoveVertexElement(dst, src);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void InterpolateVertexAttributes(int dst, int i0, int i1, int i2, ref Vector3 barycentricCoord)
            {
                for (int i = 0; i < frames.Length; i++)
                    frames[i].InterpolateVertexAttributes(dst, i0, i1, i2, ref barycentricCoord);
            }

            public void Resize(int length, bool trimExess = false)
            {
                for (int i = 0; i < frames.Length; i++)
                    frames[i].Resize(length, trimExess);
            }

            public BlendShape ToBlendShape()
            {
                var shapeFrames = new BlendShapeFrame[frames.Length];
                for (int i = 0; i < shapeFrames.Length; i++)
                    shapeFrames[i] = frames[i].ToBlendShapeFrame();
                return new BlendShape(shapeName, shapeFrames);
            }
        }

        class BlendShapeFrameContainer
        {
            float frameWeight;
            ResizableArray<Vector3> deltaVertices;
            ResizableArray<Vector3> deltaNormals;
            ResizableArray<Vector3> deltaTangents;

            public BlendShapeFrameContainer(BlendShapeFrame frame)
            {
                frameWeight = frame.FrameWeight;
                deltaVertices = new ResizableArray<Vector3>(frame.DeltaVertices);
                deltaNormals = new ResizableArray<Vector3>(frame.DeltaNormals);
                deltaTangents = new ResizableArray<Vector3>(frame.DeltaTangents);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void MoveVertexElement(int dst, int src)
            {
                deltaVertices[dst] = deltaVertices[src];
                deltaNormals[dst] = deltaNormals[src];
                deltaTangents[dst] = deltaTangents[src];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void InterpolateVertexAttributes(int dst, int i0, int i1, int i2, ref Vector3 barycentricCoord)
            {
                deltaVertices[dst] = (deltaVertices[i0] * barycentricCoord.x) + (deltaVertices[i1] * barycentricCoord.y) + (deltaVertices[i2] * barycentricCoord.z);
                deltaNormals[dst] = Vector3.Normalize((deltaNormals[i0] * barycentricCoord.x) + (deltaNormals[i1] * barycentricCoord.y) + (deltaNormals[i2] * barycentricCoord.z));
                deltaTangents[dst] = Vector3.Normalize((deltaTangents[i0] * barycentricCoord.x) + (deltaTangents[i1] * barycentricCoord.y) + (deltaTangents[i2] * barycentricCoord.z));
            }

            public void Resize(int length, bool trimExess = false)
            {
                deltaVertices.Resize(length, trimExess);
                deltaNormals.Resize(length, trimExess);
                deltaTangents.Resize(length, trimExess);
            }

            public BlendShapeFrame ToBlendShapeFrame()
            {
                var resultVertices = deltaVertices.ToArray();
                var resultNormals = deltaNormals.ToArray();
                var resultTangents = deltaTangents.ToArray();
                return new BlendShapeFrame(frameWeight, resultVertices, resultNormals, resultTangents);
            }
        }

        struct BorderVertex
        {
            public int index;
            public int hash;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public BorderVertex(int index, int hash)
            {
                this.index = index;
                this.hash = hash;
            }
        }

        class BorderVertexComparer : IComparer<BorderVertex>
        {
            public static readonly BorderVertexComparer instance = new BorderVertexComparer();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Compare(BorderVertex x, BorderVertex y) => x.hash.CompareTo(y.hash);
        }

        void InitializeVertexAttribute<T>(T[] attributeValues, ref ResizableArray<T> attributeArray, string attributeName)
        {
            if (attributeValues != null && attributeValues.Length == vertices.Length)
            {
                if (attributeArray == null) attributeArray = new ResizableArray<T>(attributeValues.Length, attributeValues.Length);
                else attributeArray.Resize(attributeValues.Length);
                var arrayData = attributeArray.Data;
                Array.Copy(attributeValues, 0, arrayData, 0, attributeValues.Length);
            }
            else
            {
                if (attributeValues != null && attributeValues.Length > 0) Debug.LogErrorFormat("Failed to set vertex attribute '{0}' with {1} length of array, when {2} was needed.", attributeName, attributeValues.Length, vertices.Length);
                attributeArray = null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        double VertexError(ref SymmetricMatrix q, double x, double y, double z) => q.m0 * x * x + 2 * q.m1 * x * y + 2 * q.m2 * x * z + 2 * q.m3 * x + q.m4 * y * y + 2 * q.m5 * y * z + 2 * q.m6 * y + q.m7 * z * z + 2 * q.m8 * z + q.m9;

        double CalculateError(ref Vertex vert0, ref Vertex vert1, out Vector3d result)
        {
            SymmetricMatrix q = (vert0.q + vert1.q);
            bool borderEdge = (vert0.borderEdge & vert1.borderEdge);
            double error = 0.0;
            double det = q.Determinant1();
            if (det != 0.0 && !borderEdge)
            {
                result = new Vector3d(-1.0 / det * q.Determinant2(), 1.0 / det * q.Determinant3(), -1.0 / det * q.Determinant4());
                error = VertexError(ref q, result.x, result.y, result.z);
            }
            else
            {
                Vector3d p1 = vert0.p;
                Vector3d p2 = vert1.p;
                Vector3d p3 = (p1 + p2) * 0.5f;
                double error1 = VertexError(ref q, p1.x, p1.y, p1.z);
                double error2 = VertexError(ref q, p2.x, p2.y, p2.z);
                double error3 = VertexError(ref q, p3.x, p3.y, p3.z);
                error = MathHelper.Min(error1, error2, error3);
                if (error == error3) result = p3;
                else if (error == error2) result = p2;
                else result = p1;
            }
            return error;
        }

        static void CalculateBarycentricCoords(ref Vector3d point, ref Vector3d a, ref Vector3d b, ref Vector3d c, out Vector3 result)
        {
            Vector3 v0 = (Vector3)(b - a), v1 = (Vector3)(c - a), v2 = (Vector3)(point - a);
            float d00 = Vector3.Dot(v0, v0);
            float d01 = Vector3.Dot(v0, v1);
            float d11 = Vector3.Dot(v1, v1);
            float d20 = Vector3.Dot(v2, v0);
            float d21 = Vector3.Dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            float v = (d11 * d20 - d01 * d21) / denom;
            float w = (d00 * d21 - d01 * d20) / denom;
            float u = 1f - v - w;
            result = new Vector3(u, v, w);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector4 NormalizeTangent(Vector4 tangent)
        {
            var tangentVec = new Vector3(tangent.x, tangent.y, tangent.z);
            tangentVec.Normalize();
            return new Vector4(tangentVec.x, tangentVec.y, tangentVec.z, tangent.w);
        }

        bool Flipped(ref Vector3d p, int i0, int i1, ref Vertex v0, bool[] deleted)
        {
            int tcount = v0.tcount;
            var refs = this.refs.Data;
            var triangles = this.triangles.Data;
            var vertices = this.vertices.Data;
            for (int k = 0; k < tcount; k++)
            {
                Ref r = refs[v0.tstart + k];
                if (triangles[r.tid].deleted) continue;
                int s = r.tvertex;
                int id1 = triangles[r.tid][(s + 1) % 3];
                int id2 = triangles[r.tid][(s + 2) % 3];
                if (id1 == i1 || id2 == i1)
                {
                    deleted[k] = true;
                    continue;
                }
                Vector3d d1 = vertices[id1].p - p;
                d1.Normalize();
                Vector3d d2 = vertices[id2].p - p;
                d2.Normalize();
                double dot = Vector3d.Dot(ref d1, ref d2);
                if (System.Math.Abs(dot) > 0.999) return true;
                Vector3d n;
                Vector3d.Cross(ref d1, ref d2, out n);
                n.Normalize();
                deleted[k] = false;
                dot = Vector3d.Dot(ref n, ref triangles[r.tid].n);
                if (dot < 0.2) return true;
            }
            return false;
        }

        void UpdateTriangles(int i0, int ia0, ref Vertex v, ResizableArray<bool> deleted, ref int deletedTriangles)
        {
            Vector3d p;
            int tcount = v.tcount;
            var triangles = this.triangles.Data;
            var vertices = this.vertices.Data;
            for (int k = 0; k < tcount; k++)
            {
                Ref r = refs[v.tstart + k];
                int tid = r.tid;
                Triangle t = triangles[tid];
                if (t.deleted) continue;
                if (deleted[k])
                {
                    triangles[tid].deleted = true;
                    ++deletedTriangles;
                    continue;
                }
                t[r.tvertex] = i0;
                if (ia0 != -1) t.SetAttributeIndex(r.tvertex, ia0);
                t.dirty = true;
                t.err0 = CalculateError(ref vertices[t.v0], ref vertices[t.v1], out p);
                t.err1 = CalculateError(ref vertices[t.v1], ref vertices[t.v2], out p);
                t.err2 = CalculateError(ref vertices[t.v2], ref vertices[t.v0], out p);
                t.err3 = MathHelper.Min(t.err0, t.err1, t.err2);
                triangles[tid] = t;
                refs.Add(r);
            }
        }

        void InterpolateVertexAttributes(int dst, int i0, int i1, int i2, ref Vector3 barycentricCoord)
        {
            if (vertNormals != null) vertNormals[dst] = Vector3.Normalize((vertNormals[i0] * barycentricCoord.x) + (vertNormals[i1] * barycentricCoord.y) + (vertNormals[i2] * barycentricCoord.z));
            if (vertTangents != null) vertTangents[dst] = NormalizeTangent((vertTangents[i0] * barycentricCoord.x) + (vertTangents[i1] * barycentricCoord.y) + (vertTangents[i2] * barycentricCoord.z));
            if (vertUV2D != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    var vertUV = vertUV2D[i];
                    if (vertUV != null)
                        vertUV[dst] = (vertUV[i0] * barycentricCoord.x) + (vertUV[i1] * barycentricCoord.y) + (vertUV[i2] * barycentricCoord.z);
                }
            }
            if (vertUV3D != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    var vertUV = vertUV3D[i];
                    if (vertUV != null)
                        vertUV[dst] = (vertUV[i0] * barycentricCoord.x) + (vertUV[i1] * barycentricCoord.y) + (vertUV[i2] * barycentricCoord.z);
                }
            }
            if (vertUV4D != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    var vertUV = vertUV4D[i];
                    if (vertUV != null)
                        vertUV[dst] = (vertUV[i0] * barycentricCoord.x) + (vertUV[i1] * barycentricCoord.y) + (vertUV[i2] * barycentricCoord.z);
                }
            }
            if (vertColors != null) vertColors[dst] = (vertColors[i0] * barycentricCoord.x) + (vertColors[i1] * barycentricCoord.y) + (vertColors[i2] * barycentricCoord.z);
            if (blendShapes != null)
            {
                for (int i = 0; i < blendShapes.Length; i++)
                    blendShapes[i].InterpolateVertexAttributes(dst, i0, i1, i2, ref barycentricCoord);
            }
        }

        bool AreUVsTheSame(int channel, int indexA, int indexB)
        {
            if (vertUV2D != null)
            {
                var vertUV = vertUV2D[channel];
                if (vertUV != null)
                    return vertUV[indexA] == vertUV[indexB];
            }
            if (vertUV3D != null)
            {
                var vertUV = vertUV3D[channel];
                if (vertUV != null)
                    return vertUV[indexA] == vertUV[indexB];
            }
            if (vertUV4D != null)
            {
                var vertUV = vertUV4D[channel];
                if (vertUV != null)
                    return vertUV[indexA] == vertUV[indexB];
            }
            return false;
        }

        void RemoveVertexPass(int startTrisCount, int targetTrisCount, double threshold, ResizableArray<bool> deleted0, ResizableArray<bool> deleted1, ref int deletedTris)
        {
            var triangles = this.triangles.Data;
            int triangleCount = this.triangles.Length;
            var vertices = this.vertices.Data;
            Vector3d p;
            Vector3 barycentricCoord;
            for (int tid = 0; tid < triangleCount; tid++)
            {
                if (triangles[tid].dirty || triangles[tid].deleted || triangles[tid].err3 > threshold) continue;
                triangles[tid].GetErrors(errArr);
                triangles[tid].GetAttributeIndices(attributeIndexArr);
                for (int edgeIndex = 0; edgeIndex < 3; edgeIndex++)
                {
                    if (errArr[edgeIndex] > threshold) continue;
                    int nextEdgeIndex = ((edgeIndex + 1) % 3);
                    int i0 = triangles[tid][edgeIndex];
                    int i1 = triangles[tid][nextEdgeIndex];
                    if (vertices[i0].borderEdge != vertices[i1].borderEdge) continue;
                    else if (vertices[i0].uvSeamEdge != vertices[i1].uvSeamEdge) continue;
                    else if (vertices[i0].uvFoldoverEdge != vertices[i1].uvFoldoverEdge) continue;
                    else if (preserveBorderEdges && vertices[i0].borderEdge) continue;
                    else if (preserveUVSeamEdges && vertices[i0].uvSeamEdge) continue;
                    else if (preserveUVFoldoverEdges && vertices[i0].uvFoldoverEdge) continue;
                    CalculateError(ref vertices[i0], ref vertices[i1], out p);
                    deleted0.Resize(vertices[i0].tcount);
                    deleted1.Resize(vertices[i1].tcount);
                    if (Flipped(ref p, i0, i1, ref vertices[i0], deleted0.Data)) continue;
                    if (Flipped(ref p, i1, i0, ref vertices[i1], deleted1.Data)) continue;
                    int nextNextEdgeIndex = ((edgeIndex + 2) % 3);
                    int i2 = triangles[tid][nextNextEdgeIndex];
                    CalculateBarycentricCoords(ref p, ref vertices[i0].p, ref vertices[i1].p, ref vertices[i2].p, out barycentricCoord);
                    vertices[i0].p = p;
                    vertices[i0].q += vertices[i1].q;
                    int ia0 = attributeIndexArr[edgeIndex];
                    int ia1 = attributeIndexArr[nextEdgeIndex];
                    int ia2 = attributeIndexArr[nextNextEdgeIndex];
                    InterpolateVertexAttributes(ia0, ia0, ia1, ia2, ref barycentricCoord);
                    if (vertices[i0].uvSeamEdge) ia0 = -1;
                    int tstart = refs.Length;
                    UpdateTriangles(i0, ia0, ref vertices[i0], deleted0, ref deletedTris);
                    UpdateTriangles(i0, ia0, ref vertices[i1], deleted1, ref deletedTris);
                    int tcount = refs.Length - tstart;
                    if (tcount <= vertices[i0].tcount)
                    {
                        if (tcount > 0) Array.Copy(refs.Data, tstart, refs.Data, vertices[i0].tstart, tcount);
                    }
                    else vertices[i0].tstart = tstart;
                    vertices[i0].tcount = tcount;
                    break;
                }
                if ((startTrisCount - deletedTris) <= targetTrisCount) break;
            }
        }

        void UpdateMesh(int iteration)
        {
            var triangles = this.triangles.Data;
            var vertices = this.vertices.Data;
            int triangleCount = this.triangles.Length;
            int vertexCount = this.vertices.Length;
            if (iteration > 0)
            {
                int dst = 0;
                for (int i = 0; i < triangleCount; i++)
                {
                    if (!triangles[i].deleted)
                    {
                        if (dst != i) triangles[dst] = triangles[i];
                        dst++;
                    }
                }
                this.triangles.Resize(dst);
                triangles = this.triangles.Data;
                triangleCount = dst;
            }
            UpdateReferences();
            if (iteration == 0)
            {
                var refs = this.refs.Data;
                var vcount = new List<int>(8);
                var vids = new List<int>(8);
                int vsize = 0;
                for (int i = 0; i < vertexCount; i++)
                {
                    vertices[i].borderEdge = false;
                    vertices[i].uvSeamEdge = false;
                    vertices[i].uvFoldoverEdge = false;
                }
                int ofs, id, borderVertexCount = 0;
                double borderMinX = double.MaxValue;
                double borderMaxX = double.MinValue;
                for (int i = 0; i < vertexCount; i++)
                {
                    int tstart = vertices[i].tstart;
                    int tcount = vertices[i].tcount;
                    vcount.Clear();
                    vids.Clear();
                    vsize = 0;
                    for (int j = 0; j < tcount; j++)
                    {
                        int tid = refs[tstart + j].tid;
                        for (int k = 0; k < 3; k++)
                        {
                            ofs = 0;
                            id = triangles[tid][k];
                            while (ofs < vsize)
                            {
                                if (vids[ofs] == id) break;
                                ++ofs;
                            }
                            if (ofs == vsize)
                            {
                                vcount.Add(1);
                                vids.Add(id);
                                ++vsize;
                            }
                            else ++vcount[ofs];
                        }
                    }
                    for (int j = 0; j < vsize; j++)
                    {
                        if (vcount[j] == 1)
                        {
                            id = vids[j];
                            vertices[id].borderEdge = true;
                            ++borderVertexCount;
                            if (enableSmartLink)
                            {
                                if (vertices[id].p.x < borderMinX) borderMinX = vertices[id].p.x;
                                if (vertices[id].p.x > borderMaxX) borderMaxX = vertices[id].p.x;
                            }
                        }
                    }
                }
                if (enableSmartLink)
                {
                    var borderVertices = new BorderVertex[borderVertexCount];
                    int borderIndexCount = 0;
                    double borderAreaWidth = borderMaxX - borderMinX;
                    for (int i = 0; i < vertexCount; i++)
                    {
                        if (vertices[i].borderEdge)
                        {
                            int vertexHash = (int)(((((vertices[i].p.x - borderMinX) / borderAreaWidth) * 2.0) - 1.0) * int.MaxValue);
                            borderVertices[borderIndexCount] = new BorderVertex(i, vertexHash);
                            ++borderIndexCount;
                        }
                    }
                    Array.Sort(borderVertices, 0, borderIndexCount, BorderVertexComparer.instance);
                    double vertexLinkDistance = Math.Sqrt(vertexLinkDistanceSqr);
                    int hashMaxDistance = Math.Max((int)((vertexLinkDistance / borderAreaWidth) * int.MaxValue), 1);
                    for (int i = 0; i < borderIndexCount; i++)
                    {
                        int myIndex = borderVertices[i].index;
                        if (myIndex == -1) continue;
                        var myPoint = vertices[myIndex].p;
                        for (int j = i + 1; j < borderIndexCount; j++)
                        {
                            int otherIndex = borderVertices[j].index;
                            if (otherIndex == -1) continue;
                            else if ((borderVertices[j].hash - borderVertices[i].hash) > hashMaxDistance) break;
                            var otherPoint = vertices[otherIndex].p;
                            var sqrMagnitude = ((myPoint.x - otherPoint.x) * (myPoint.x - otherPoint.x)) + ((myPoint.y - otherPoint.y) * (myPoint.y - otherPoint.y)) + ((myPoint.z - otherPoint.z) * (myPoint.z - otherPoint.z));
                            if (sqrMagnitude <= vertexLinkDistanceSqr)
                            {
                                borderVertices[j].index = -1;
                                vertices[myIndex].borderEdge = false;
                                vertices[otherIndex].borderEdge = false;
                                if (AreUVsTheSame(0, myIndex, otherIndex))
                                {
                                    vertices[myIndex].uvFoldoverEdge = true;
                                    vertices[otherIndex].uvFoldoverEdge = true;
                                }
                                else
                                {
                                    vertices[myIndex].uvSeamEdge = true;
                                    vertices[otherIndex].uvSeamEdge = true;
                                }
                                int otherTriangleCount = vertices[otherIndex].tcount;
                                int otherTriangleStart = vertices[otherIndex].tstart;
                                for (int k = 0; k < otherTriangleCount; k++)
                                {
                                    var r = refs[otherTriangleStart + k];
                                    triangles[r.tid][r.tvertex] = myIndex;
                                }
                            }
                        }
                    }
                    UpdateReferences();
                }
                for (int i = 0; i < vertexCount; i++)
                    vertices[i].q = new SymmetricMatrix();
                int v0, v1, v2;
                Vector3d n, p0, p1, p2, p10, p20, dummy;
                SymmetricMatrix sm;
                for (int i = 0; i < triangleCount; i++)
                {
                    v0 = triangles[i].v0;
                    v1 = triangles[i].v1;
                    v2 = triangles[i].v2;
                    p0 = vertices[v0].p;
                    p1 = vertices[v1].p;
                    p2 = vertices[v2].p;
                    p10 = p1 - p0;
                    p20 = p2 - p0;
                    Vector3d.Cross(ref p10, ref p20, out n);
                    n.Normalize();
                    triangles[i].n = n;
                    sm = new SymmetricMatrix(n.x, n.y, n.z, -Vector3d.Dot(ref n, ref p0));
                    vertices[v0].q += sm;
                    vertices[v1].q += sm;
                    vertices[v2].q += sm;
                }
                for (int i = 0; i < triangleCount; i++)
                {
                    var triangle = triangles[i];
                    triangles[i].err0 = CalculateError(ref vertices[triangle.v0], ref vertices[triangle.v1], out dummy);
                    triangles[i].err1 = CalculateError(ref vertices[triangle.v1], ref vertices[triangle.v2], out dummy);
                    triangles[i].err2 = CalculateError(ref vertices[triangle.v2], ref vertices[triangle.v0], out dummy);
                    triangles[i].err3 = MathHelper.Min(triangles[i].err0, triangles[i].err1, triangles[i].err2);
                }
            }
        }

        void UpdateReferences()
        {
            int triangleCount = this.triangles.Length;
            int vertexCount = this.vertices.Length;
            var triangles = this.triangles.Data;
            var vertices = this.vertices.Data;
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i].tstart = 0;
                vertices[i].tcount = 0;
            }
            for (int i = 0; i < triangleCount; i++)
            {
                ++vertices[triangles[i].v0].tcount;
                ++vertices[triangles[i].v1].tcount;
                ++vertices[triangles[i].v2].tcount;
            }
            int tstart = 0;
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i].tstart = tstart;
                tstart += vertices[i].tcount;
                vertices[i].tcount = 0;
            }
            this.refs.Resize(tstart);
            var refs = this.refs.Data;
            for (int i = 0; i < triangleCount; i++)
            {
                int v0 = triangles[i].v0;
                int v1 = triangles[i].v1;
                int v2 = triangles[i].v2;
                int start0 = vertices[v0].tstart;
                int count0 = vertices[v0].tcount;
                int start1 = vertices[v1].tstart;
                int count1 = vertices[v1].tcount;
                int start2 = vertices[v2].tstart;
                int count2 = vertices[v2].tcount;
                refs[start0 + count0].Set(i, 0);
                refs[start1 + count1].Set(i, 1);
                refs[start2 + count2].Set(i, 2);
                ++vertices[v0].tcount;
                ++vertices[v1].tcount;
                ++vertices[v2].tcount;
            }
        }

        void CompactMesh()
        {
            int dst = 0;
            var vertices = this.vertices.Data;
            int vertexCount = this.vertices.Length;
            for (int i = 0; i < vertexCount; i++)
                vertices[i].tcount = 0;
            var vertNormals = (this.vertNormals != null ? this.vertNormals.Data : null);
            var vertTangents = (this.vertTangents != null ? this.vertTangents.Data : null);
            var vertUV2D = (this.vertUV2D != null ? this.vertUV2D.Data : null);
            var vertUV3D = (this.vertUV3D != null ? this.vertUV3D.Data : null);
            var vertUV4D = (this.vertUV4D != null ? this.vertUV4D.Data : null);
            var vertColors = (this.vertColors != null ? this.vertColors.Data : null);
            var vertBoneWeights = (this.vertBoneWeights != null ? this.vertBoneWeights.Data : null);
            var blendShapes = (this.blendShapes != null ? this.blendShapes.Data : null);
            int lastSubMeshIndex = -1;
            subMeshOffsets = new int[subMeshCount];
            var triangles = this.triangles.Data;
            int triangleCount = this.triangles.Length;
            for (int i = 0; i < triangleCount; i++)
            {
                var triangle = triangles[i];
                if (!triangle.deleted)
                {
                    if (triangle.va0 != triangle.v0)
                    {
                        int iDest = triangle.va0;
                        int iSrc = triangle.v0;
                        vertices[iDest].p = vertices[iSrc].p;
                        if (vertBoneWeights != null) vertBoneWeights[iDest] = vertBoneWeights[iSrc];
                        triangle.v0 = triangle.va0;
                    }
                    if (triangle.va1 != triangle.v1)
                    {
                        int iDest = triangle.va1;
                        int iSrc = triangle.v1;
                        vertices[iDest].p = vertices[iSrc].p;
                        if (vertBoneWeights != null) vertBoneWeights[iDest] = vertBoneWeights[iSrc];
                        triangle.v1 = triangle.va1;
                    }
                    if (triangle.va2 != triangle.v2)
                    {
                        int iDest = triangle.va2;
                        int iSrc = triangle.v2;
                        vertices[iDest].p = vertices[iSrc].p;
                        if (vertBoneWeights != null) vertBoneWeights[iDest] = vertBoneWeights[iSrc];
                        triangle.v2 = triangle.va2;
                    }
                    int newTriangleIndex = dst++;
                    triangles[newTriangleIndex] = triangle;
                    vertices[triangle.v0].tcount = 1;
                    vertices[triangle.v1].tcount = 1;
                    vertices[triangle.v2].tcount = 1;
                    if (triangle.subMeshIndex > lastSubMeshIndex)
                    {
                        for (int j = lastSubMeshIndex + 1; j < triangle.subMeshIndex; j++)
                            subMeshOffsets[j] = newTriangleIndex;
                        subMeshOffsets[triangle.subMeshIndex] = newTriangleIndex;
                        lastSubMeshIndex = triangle.subMeshIndex;
                    }
                }
            }
            triangleCount = dst;
            for (int i = lastSubMeshIndex + 1; i < subMeshCount; i++)
                subMeshOffsets[i] = triangleCount;
            this.triangles.Resize(triangleCount);
            triangles = this.triangles.Data;
            dst = 0;
            for (int i = 0; i < vertexCount; i++)
            {
                var vert = vertices[i];
                if (vert.tcount > 0)
                {
                    vert.tstart = dst;
                    vertices[i] = vert;
                    if (dst != i)
                    {
                        vertices[dst].p = vert.p;
                        if (vertNormals != null) vertNormals[dst] = vertNormals[i];
                        if (vertTangents != null) vertTangents[dst] = vertTangents[i];
                        if (vertUV2D != null)
                        {
                            for (int j = 0; j < 4; j++)
                            {
                                var vertUV = vertUV2D[j];
                                if (vertUV != null)
                                    vertUV[dst] = vertUV[i];
                            }
                        }
                        if (vertUV3D != null)
                        {
                            for (int j = 0; j < 4; j++)
                            {
                                var vertUV = vertUV3D[j];
                                if (vertUV != null)
                                    vertUV[dst] = vertUV[i];
                            }
                        }
                        if (vertUV4D != null)
                        {
                            for (int j = 0; j < 4; j++)
                            {
                                var vertUV = vertUV4D[j];
                                if (vertUV != null)
                                    vertUV[dst] = vertUV[i];
                            }
                        }
                        if (vertColors != null) vertColors[dst] = vertColors[i];
                        if (vertBoneWeights != null) vertBoneWeights[dst] = vertBoneWeights[i];
                        if (blendShapes != null)
                        {
                            for (int shapeIndex = 0; shapeIndex < this.blendShapes.Length; shapeIndex++)
                                blendShapes[shapeIndex].MoveVertexElement(dst, i);
                        }
                    }
                    ++dst;
                }
            }
            for (int i = 0; i < triangleCount; i++)
            {
                var triangle = triangles[i];
                triangle.v0 = vertices[triangle.v0].tstart;
                triangle.v1 = vertices[triangle.v1].tstart;
                triangle.v2 = vertices[triangle.v2].tstart;
                triangles[i] = triangle;
            }
            vertexCount = dst;
            this.vertices.Resize(vertexCount);
            if (vertNormals != null) this.vertNormals.Resize(vertexCount, true);
            if (vertTangents != null) this.vertTangents.Resize(vertexCount, true);
            if (vertUV2D != null) this.vertUV2D.Resize(vertexCount, true);
            if (vertUV3D != null) this.vertUV3D.Resize(vertexCount, true);
            if (vertUV4D != null) this.vertUV4D.Resize(vertexCount, true);
            if (vertColors != null) this.vertColors.Resize(vertexCount, true);
            if (vertBoneWeights != null) this.vertBoneWeights.Resize(vertexCount, true);
            if (blendShapes != null)
                for (int i = 0; i < this.blendShapes.Length; i++)
                    blendShapes[i].Resize(vertexCount, false);
        }

        void CalculateSubMeshOffsets()
        {
            int lastSubMeshIndex = -1;
            subMeshOffsets = new int[subMeshCount];
            var triangles = this.triangles.Data;
            int triangleCount = this.triangles.Length;
            for (int i = 0; i < triangleCount; i++)
            {
                var triangle = triangles[i];
                if (triangle.subMeshIndex > lastSubMeshIndex)
                {
                    for (int j = lastSubMeshIndex + 1; j < triangle.subMeshIndex; j++)
                        subMeshOffsets[j] = i;
                    subMeshOffsets[triangle.subMeshIndex] = i;
                    lastSubMeshIndex = triangle.subMeshIndex;
                }
            }
            for (int i = lastSubMeshIndex + 1; i < subMeshCount; i++)
                subMeshOffsets[i] = triangleCount;
        }

        public int[][] GetAllSubMeshTriangles()
        {
            var indices = new int[subMeshCount][];
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                indices[subMeshIndex] = GetSubMeshTriangles(subMeshIndex);
            return indices;
        }

        public int[] GetSubMeshTriangles(int subMeshIndex)
        {
            if (subMeshIndex < 0) throw new ArgumentOutOfRangeException(nameof(subMeshIndex));
            if (subMeshOffsets == null) CalculateSubMeshOffsets();
            if (subMeshIndex >= subMeshOffsets.Length) throw new ArgumentOutOfRangeException(nameof(subMeshIndex));
            else if (subMeshOffsets.Length != subMeshCount) throw new InvalidOperationException("The sub-mesh triangle offsets array is not the same size as the count of sub-meshes.");
            var triangles = this.triangles.Data;
            int triangleCount = this.triangles.Length;
            int startOffset = subMeshOffsets[subMeshIndex];
            if (startOffset >= triangleCount) return new int[0];
            int endOffset = ((subMeshIndex + 1) < subMeshCount ? subMeshOffsets[subMeshIndex + 1] : triangleCount);
            int subMeshTriangleCount = endOffset - startOffset;
            if (subMeshTriangleCount < 0) subMeshTriangleCount = 0;
            int[] subMeshIndices = new int[subMeshTriangleCount * 3];
            for (int triangleIndex = startOffset; triangleIndex < endOffset; triangleIndex++)
            {
                var triangle = triangles[triangleIndex];
                int offset = (triangleIndex - startOffset) * 3;
                subMeshIndices[offset] = triangle.v0;
                subMeshIndices[offset + 1] = triangle.v1;
                subMeshIndices[offset + 2] = triangle.v2;
            }
            return subMeshIndices;
        }

        public void ClearSubMeshes()
        {
            subMeshCount = 0;
            subMeshOffsets = null;
            triangles.Resize(0);
        }

        public void AddSubMeshTriangles(int[] triangles)
        {
            if (triangles == null) throw new ArgumentNullException(nameof(triangles));
            else if ((triangles.Length % 3) != 0) throw new ArgumentException("The index array length must be a multiple of 3 in order to represent triangles.", nameof(triangles));
            int subMeshIndex = subMeshCount++;
            int triangleIndex = this.triangles.Length;
            int subMeshTriangleCount = triangles.Length / 3;
            this.triangles.Resize(this.triangles.Length + subMeshTriangleCount);
            var trisArr = this.triangles.Data;
            for (int i = 0; i < subMeshTriangleCount; i++)
            {
                int offset = i * 3;
                int v0 = triangles[offset];
                int v1 = triangles[offset + 1];
                int v2 = triangles[offset + 2];
                trisArr[triangleIndex + i] = new Triangle(v0, v1, v2, subMeshIndex);
            }
        }

        public void AddSubMeshTriangles(int[][] triangles)
        {
            if (triangles == null) throw new ArgumentNullException(nameof(triangles));
            int totalTriangleCount = 0;
            for (int i = 0; i < triangles.Length; i++)
            {
                if (triangles[i] == null) throw new ArgumentException(string.Format("The index array at index {0} is null.", i));
                else if ((triangles[i].Length % 3) != 0) throw new ArgumentException(string.Format("The index array length at index {0} must be a multiple of 3 in order to represent triangles.", i), nameof(triangles));
                totalTriangleCount += triangles[i].Length / 3;
            }
            int triangleIndex = this.triangles.Length;
            this.triangles.Resize(this.triangles.Length + totalTriangleCount);
            var trisArr = this.triangles.Data;
            for (int i = 0; i < triangles.Length; i++)
            {
                int subMeshIndex = subMeshCount++;
                var subMeshTriangles = triangles[i];
                int subMeshTriangleCount = subMeshTriangles.Length / 3;
                for (int j = 0; j < subMeshTriangleCount; j++)
                {
                    int offset = j * 3;
                    int v0 = subMeshTriangles[offset];
                    int v1 = subMeshTriangles[offset + 1];
                    int v2 = subMeshTriangles[offset + 2];
                    trisArr[triangleIndex + j] = new Triangle(v0, v1, v2, subMeshIndex);
                }
                triangleIndex += subMeshTriangleCount;
            }
        }

        public Vector2[] GetUVs2D(int channel)
        {
            if (channel < 0 || channel >= 4) throw new ArgumentOutOfRangeException(nameof(channel));
            if (vertUV2D != null && vertUV2D[channel] != null) return vertUV2D[channel].Data;
            else return null;
        }

        public Vector3[] GetUVs3D(int channel)
        {
            if (channel < 0 || channel >= 4) throw new ArgumentOutOfRangeException(nameof(channel));
            if (vertUV3D != null && vertUV3D[channel] != null) return vertUV3D[channel].Data;
            else return null;
        }

        public Vector4[] GetUVs4D(int channel)
        {
            if (channel < 0 || channel >= 4) throw new ArgumentOutOfRangeException(nameof(channel));
            if (vertUV4D != null && vertUV4D[channel] != null) return vertUV4D[channel].Data;
            else return null;
        }

        public void GetUVs(int channel, List<Vector2> uvs)
        {
            if (channel < 0 || channel >= 4) throw new ArgumentOutOfRangeException(nameof(channel));
            else if (uvs == null) throw new ArgumentNullException(nameof(uvs));
            uvs.Clear();
            if (vertUV2D != null && vertUV2D[channel] != null)
            {
                var uvData = vertUV2D[channel].Data;
                if (uvData != null) uvs.AddRange(uvData);
            }
        }

        public void GetUVs(int channel, List<Vector3> uvs)
        {
            if (channel < 0 || channel >= 4) throw new ArgumentOutOfRangeException(nameof(channel));
            else if (uvs == null) throw new ArgumentNullException(nameof(uvs));
            uvs.Clear();
            if (vertUV3D != null && vertUV3D[channel] != null)
            {
                var uvData = vertUV3D[channel].Data;
                if (uvData != null) uvs.AddRange(uvData);
            }
        }

        public void GetUVs(int channel, List<Vector4> uvs)
        {
            if (channel < 0 || channel >= 4) throw new ArgumentOutOfRangeException(nameof(channel));
            else if (uvs == null) throw new ArgumentNullException(nameof(uvs));
            uvs.Clear();
            if (vertUV4D != null && vertUV4D[channel] != null)
            {
                var uvData = vertUV4D[channel].Data;
                if (uvData != null) uvs.AddRange(uvData);
            }
        }

        public void SetUVs(int channel, Vector2[] uvs)
        {
            if (channel < 0 || channel >= 4) throw new ArgumentOutOfRangeException(nameof(channel));
            if (uvs != null && uvs.Length > 0)
            {
                if (vertUV2D == null) vertUV2D = new UVChannels<Vector2>();
                int uvCount = uvs.Length;
                var uvSet = vertUV2D[channel];
                if (uvSet != null) uvSet.Resize(uvCount);
                else
                {
                    uvSet = new ResizableArray<Vector2>(uvCount, uvCount);
                    vertUV2D[channel] = uvSet;
                }
                var uvData = uvSet.Data;
                uvs.CopyTo(uvData, 0);
            }
            else if (vertUV2D != null) vertUV2D[channel] = null;
            if (vertUV3D != null) vertUV3D[channel] = null;
            if (vertUV4D != null) vertUV4D[channel] = null;
        }

        public void SetUVs(int channel, Vector3[] uvs)
        {
            if (channel < 0 || channel >= 4) throw new ArgumentOutOfRangeException(nameof(channel));
            if (uvs != null && uvs.Length > 0)
            {
                if (vertUV3D == null) vertUV3D = new UVChannels<Vector3>();
                int uvCount = uvs.Length;
                var uvSet = vertUV3D[channel];
                if (uvSet != null) uvSet.Resize(uvCount);
                else
                {
                    uvSet = new ResizableArray<Vector3>(uvCount, uvCount);
                    vertUV3D[channel] = uvSet;
                }
                var uvData = uvSet.Data;
                uvs.CopyTo(uvData, 0);
            }
            else if (vertUV3D != null) vertUV3D[channel] = null;
            if (vertUV2D != null) vertUV2D[channel] = null;
            if (vertUV4D != null) vertUV4D[channel] = null;
        }

        public void SetUVs(int channel, Vector4[] uvs)
        {
            if (channel < 0 || channel >= 4) throw new ArgumentOutOfRangeException(nameof(channel));
            if (uvs != null && uvs.Length > 0)
            {
                if (vertUV4D == null) vertUV4D = new UVChannels<Vector4>();
                int uvCount = uvs.Length;
                var uvSet = vertUV4D[channel];
                if (uvSet != null) uvSet.Resize(uvCount);
                else
                {
                    uvSet = new ResizableArray<Vector4>(uvCount, uvCount);
                    vertUV4D[channel] = uvSet;
                }
                var uvData = uvSet.Data;
                uvs.CopyTo(uvData, 0);
            }
            else if (vertUV4D != null) vertUV4D[channel] = null;
            if (vertUV2D != null) vertUV2D[channel] = null;
            if (vertUV3D != null) vertUV3D[channel] = null;
        }

        public void SetUVs(int channel, List<Vector2> uvs)
        {
            if (channel < 0 || channel >= 4) throw new ArgumentOutOfRangeException(nameof(channel));
            if (uvs != null && uvs.Count > 0)
            {
                if (vertUV2D == null) vertUV2D = new UVChannels<Vector2>();
                int uvCount = uvs.Count;
                var uvSet = vertUV2D[channel];
                if (uvSet != null) uvSet.Resize(uvCount);
                else
                {
                    uvSet = new ResizableArray<Vector2>(uvCount, uvCount);
                    vertUV2D[channel] = uvSet;
                }
                var uvData = uvSet.Data;
                uvs.CopyTo(uvData, 0);
            }
            else if (vertUV2D != null) vertUV2D[channel] = null;
            if (vertUV3D != null) vertUV3D[channel] = null;
            if (vertUV4D != null) vertUV4D[channel] = null;
        }

        public void SetUVs(int channel, List<Vector3> uvs)
        {
            if (channel < 0 || channel >= 4) throw new ArgumentOutOfRangeException(nameof(channel));
            if (uvs != null && uvs.Count > 0)
            {
                if (vertUV3D == null) vertUV3D = new UVChannels<Vector3>();
                int uvCount = uvs.Count;
                var uvSet = vertUV3D[channel];
                if (uvSet != null) uvSet.Resize(uvCount);
                else
                {
                    uvSet = new ResizableArray<Vector3>(uvCount, uvCount);
                    vertUV3D[channel] = uvSet;
                }
                var uvData = uvSet.Data;
                uvs.CopyTo(uvData, 0);
            }
            else if (vertUV3D != null) vertUV3D[channel] = null;
            if (vertUV2D != null) vertUV2D[channel] = null;
            if (vertUV4D != null) vertUV4D[channel] = null;
        }

        public void SetUVs(int channel, List<Vector4> uvs)
        {
            if (channel < 0 || channel >= 4) throw new ArgumentOutOfRangeException(nameof(channel));
            if (uvs != null && uvs.Count > 0)
            {
                if (vertUV4D == null) vertUV4D = new UVChannels<Vector4>();
                int uvCount = uvs.Count;
                var uvSet = vertUV4D[channel];
                if (uvSet != null) uvSet.Resize(uvCount);
                else
                {
                    uvSet = new ResizableArray<Vector4>(uvCount, uvCount);
                    vertUV4D[channel] = uvSet;
                }
                var uvData = uvSet.Data;
                uvs.CopyTo(uvData, 0);
            }
            else if (vertUV4D != null) vertUV4D[channel] = null;
            if (vertUV2D != null) vertUV2D[channel] = null;
            if (vertUV3D != null) vertUV3D[channel] = null;
        }

        public void SetUVsAuto(int channel, List<Vector4> uvs)
        {
            if (channel < 0 || channel >= 4) throw new ArgumentOutOfRangeException(nameof(channel));
            if (uvs != null && uvs.Count > 0)
            {
                int usedComponents = MeshUtils.GetUsedUVComponents(uvs);
                if (usedComponents <= 2)
                {
                    var uv2D = MeshUtils.ConvertUVsTo2D(uvs);
                    SetUVs(channel, uv2D);
                }
                else if (usedComponents == 3)
                {
                    var uv3D = MeshUtils.ConvertUVsTo3D(uvs);
                    SetUVs(channel, uv3D);
                }
                else SetUVs(channel, uvs);
            }
            else
            {
                if (vertUV2D != null) vertUV2D[channel] = null;
                if (vertUV3D != null) vertUV3D[channel] = null;
                if (vertUV4D != null) vertUV4D[channel] = null;
            }
        }

        public BlendShape[] GetAllBlendShapes()
        {
            if (blendShapes == null) return null;
            var results = new BlendShape[blendShapes.Length];
            for (int i = 0; i < results.Length; i++)
                results[i] = blendShapes[i].ToBlendShape();
            return results;
        }

        public BlendShape GetBlendShape(int blendShapeIndex)
        {
            if (blendShapes == null || blendShapeIndex < 0 || blendShapeIndex >= blendShapes.Length) throw new ArgumentOutOfRangeException(nameof(blendShapeIndex));
            return blendShapes[blendShapeIndex].ToBlendShape();
        }

        public void ClearBlendShapes()
        {
            if (blendShapes != null)
            {
                blendShapes.Clear();
                blendShapes = null;
            }
        }

        public void AddBlendShape(BlendShape blendShape)
        {
            var frames = blendShape.Frames;
            if (frames == null || frames.Length == 0) throw new ArgumentException("The frames cannot be null or empty.", nameof(blendShape));
            if (this.blendShapes == null) this.blendShapes = new ResizableArray<BlendShapeContainer>(4, 0);
            var container = new BlendShapeContainer(blendShape);
            this.blendShapes.Add(container);
        }

        public void AddBlendShapes(BlendShape[] blendShapes)
        {
            if (blendShapes == null) throw new ArgumentNullException(nameof(blendShapes));
            if (this.blendShapes == null) this.blendShapes = new ResizableArray<BlendShapeContainer>(Math.Max(4, blendShapes.Length), 0);
            for (int i = 0; i < blendShapes.Length; i++)
            {
                var frames = blendShapes[i].Frames;
                if (frames == null || frames.Length == 0) throw new ArgumentException(string.Format("The frames of blend shape at index {0} cannot be null or empty.", i), nameof(blendShapes));
                var container = new BlendShapeContainer(blendShapes[i]);
                this.blendShapes.Add(container);
            }
        }

        public void Initialize(Mesh mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            this.Vertices = mesh.vertices;
            this.Normals = mesh.normals;
            this.Tangents = mesh.tangents;
            this.Colors = mesh.colors;
            this.BoneWeights = mesh.boneWeights;
            this.bindposes = mesh.bindposes;
            for (int channel = 0; channel < 4; channel++)
            {
                var uvs = MeshUtils.GetMeshUVs(mesh, channel);
                SetUVsAuto(channel, uvs);
            }
            var blendShapes = MeshUtils.GetMeshBlendShapes(mesh);
            if (blendShapes != null && blendShapes.Length > 0) AddBlendShapes(blendShapes);
            ClearSubMeshes();
            int subMeshCount = mesh.subMeshCount;
            var subMeshTriangles = new int[subMeshCount][];
            for (int i = 0; i < subMeshCount; i++)
                subMeshTriangles[i] = mesh.GetTriangles(i);
            AddSubMeshTriangles(subMeshTriangles);
        }

        public void SimplifyMesh(float quality)
        {
            quality = Mathf.Clamp01(quality);
            int deletedTris = 0;
            ResizableArray<bool> deleted0 = new ResizableArray<bool>(20);
            ResizableArray<bool> deleted1 = new ResizableArray<bool>(20);
            var triangles = this.triangles.Data;
            int triangleCount = this.triangles.Length;
            int startTrisCount = triangleCount;
            var vertices = this.vertices.Data;
            int targetTrisCount = Mathf.RoundToInt(triangleCount * quality);
            for (int iteration = 0; iteration < maxIterationCount; iteration++)
            {
                if ((startTrisCount - deletedTris) <= targetTrisCount) break;
                if ((iteration % 5) == 0)
                {
                    UpdateMesh(iteration);
                    triangles = this.triangles.Data;
                    triangleCount = this.triangles.Length;
                    vertices = this.vertices.Data;
                }
                for (int i = 0; i < triangleCount; i++)
                    triangles[i].dirty = false;
                double threshold = 0.000000001 * Math.Pow(iteration + 3, agressiveness);
                RemoveVertexPass(startTrisCount, targetTrisCount, threshold, deleted0, deleted1, ref deletedTris);
            }
            CompactMesh();
        }

        public Mesh ToMesh()
        {
            var vertices = this.Vertices;
            var normals = this.Normals;
            var tangents = this.Tangents;
            var colors = this.Colors;
            var boneWeights = this.BoneWeights;
            var indices = GetAllSubMeshTriangles();
            var blendShapes = GetAllBlendShapes();
            List<Vector2>[] uvs2D = null;
            List<Vector3>[] uvs3D = null;
            List<Vector4>[] uvs4D = null;
            if (vertUV2D != null)
            {
                uvs2D = new List<Vector2>[4];
                for (int uvChannel = 0; uvChannel < uvs2D.Length; uvChannel++)
                {
                    if (vertUV2D[uvChannel] != null)
                    {
                        var uvs = new List<Vector2>(vertices.Length);
                        GetUVs(uvChannel, uvs);
                        uvs2D[uvChannel] = uvs;
                    }
                }
            }
            if (vertUV3D != null)
            {
                uvs3D = new List<Vector3>[4];
                for (int uvChannel = 0; uvChannel < uvs3D.Length; uvChannel++)
                {
                    if (vertUV3D[uvChannel] != null)
                    {
                        var uvs = new List<Vector3>(vertices.Length);
                        GetUVs(uvChannel, uvs);
                        uvs3D[uvChannel] = uvs;
                    }
                }
            }
            if (vertUV4D != null)
            {
                uvs4D = new List<Vector4>[4];
                for (int uvChannel = 0; uvChannel < uvs4D.Length; uvChannel++)
                {
                    if (vertUV4D[uvChannel] != null)
                    {
                        var uvs = new List<Vector4>(vertices.Length);
                        GetUVs(uvChannel, uvs);
                        uvs4D[uvChannel] = uvs;
                    }
                }
            }
            return MeshUtils.CreateMesh(vertices, indices, normals, tangents, colors, boneWeights, uvs2D, uvs3D, uvs4D, bindposes, blendShapes);
        }
    }

    internal sealed class ResizableArray<T>
    {
        T[] items = null;
        int length = 0;
        static T[] emptyArr = new T[0];

        public int Length { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return length; } }
        public T[] Data { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return items; } }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return items[index]; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set { items[index] = value; }
        }

        public ResizableArray(int capacity) : this(capacity, 0) { }

        public ResizableArray(int capacity, int length)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            else if (length < 0 || length > capacity) throw new ArgumentOutOfRangeException(nameof(length));
            if (capacity > 0) items = new T[capacity];
            else items = emptyArr;
            this.length = length;
        }

        public ResizableArray(T[] initialArray)
        {
            if (initialArray == null) throw new ArgumentNullException(nameof(initialArray));
            if (initialArray.Length > 0)
            {
                items = new T[initialArray.Length];
                length = initialArray.Length;
                Array.Copy(initialArray, 0, items, 0, initialArray.Length);
            }
            else
            {
                items = emptyArr;
                length = 0;
            }
        }

        void IncreaseCapacity(int capacity)
        {
            T[] newItems = new T[capacity];
            Array.Copy(items, 0, newItems, 0, System.Math.Min(length, capacity));
            items = newItems;
        }

        public void Clear()
        {
            Array.Clear(items, 0, length);
            length = 0;
        }

        public void Resize(int length, bool trimExess = false)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (length > items.Length) IncreaseCapacity(length);
            this.length = length;
            if (trimExess) TrimExcess();
        }

        public void TrimExcess()
        {
            if (items.Length == length) return;
            var newItems = new T[length];
            Array.Copy(items, 0, newItems, 0, length);
            items = newItems;
        }

        public void Add(T item)
        {
            if (length >= items.Length) IncreaseCapacity(items.Length << 1);
            items[length++] = item;
        }

        public T[] ToArray()
        {
            var newItems = new T[length];
            Array.Copy(items, 0, newItems, 0, length);
            return newItems;
        }
    }

    public struct SymmetricMatrix
    {
        public double m0;
        public double m1;
        public double m2;
        public double m3;
        public double m4;
        public double m5;
        public double m6;
        public double m7;
        public double m8;
        public double m9;

        public double this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                switch (index)
                {
                    case 0: return m0;
                    case 1: return m1;
                    case 2: return m2;
                    case 3: return m3;
                    case 4: return m4;
                    case 5: return m5;
                    case 6: return m6;
                    case 7: return m7;
                    case 8: return m8;
                    case 9: return m9;
                    default: throw new IndexOutOfRangeException();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SymmetricMatrix(double c)
        {
            this.m0 = c; this.m1 = c; this.m2 = c; this.m3 = c; this.m4 = c;
            this.m5 = c; this.m6 = c; this.m7 = c; this.m8 = c; this.m9 = c;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SymmetricMatrix(double m0, double m1, double m2, double m3, double m4, double m5, double m6, double m7, double m8, double m9)
        {
            this.m0 = m0; this.m1 = m1; this.m2 = m2; this.m3 = m3; this.m4 = m4;
            this.m5 = m5; this.m6 = m6; this.m7 = m7; this.m8 = m8; this.m9 = m9;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SymmetricMatrix(double a, double b, double c, double d)
        {
            this.m0 = a * a; this.m1 = a * b; this.m2 = a * c; this.m3 = a * d;
            this.m4 = b * b; this.m5 = b * c; this.m6 = b * d;
            this.m7 = c * c; this.m8 = c * d;
            this.m9 = d * d;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SymmetricMatrix operator +(SymmetricMatrix a, SymmetricMatrix b)
        {
            return new SymmetricMatrix(
                a.m0 + b.m0, a.m1 + b.m1, a.m2 + b.m2, a.m3 + b.m3,
                a.m4 + b.m4, a.m5 + b.m5, a.m6 + b.m6,
                a.m7 + b.m7, a.m8 + b.m8,
                a.m9 + b.m9
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal double Determinant1() => m0 * m4 * m7 + m2 * m1 * m5 + m1 * m5 * m2 - m2 * m4 * m2 - m0 * m5 * m5 - m1 * m1 * m7;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal double Determinant2() => m1 * m5 * m8 + m3 * m4 * m7 + m2 * m6 * m5 - m3 * m5 * m5 - m1 * m6 * m7 - m2 * m4 * m8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal double Determinant3() => m0 * m5 * m8 + m3 * m1 * m7 + m2 * m6 * m2 - m3 * m5 * m2 - m0 * m6 * m7 - m2 * m1 * m8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal double Determinant4() => m0 * m4 * m8 + m3 * m1 * m5 + m1 * m6 * m2 - m3 * m4 * m2 - m0 * m6 * m5 - m1 * m1 * m8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public double Determinant(int a11, int a12, int a13, int a21, int a22, int a23, int a31, int a32, int a33) => this[a11] * this[a22] * this[a33] + this[a13] * this[a21] * this[a32] + this[a12] * this[a23] * this[a31] - this[a13] * this[a22] * this[a31] - this[a11] * this[a23] * this[a32] - this[a12] * this[a21] * this[a33];
    }

    public struct Vector3d : IEquatable<Vector3d>
    {
        public static readonly Vector3d zero = new Vector3d(0, 0, 0);
        public const double Epsilon = double.Epsilon;
        public double x;
        public double y;
        public double z;

        public double Magnitude { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return System.Math.Sqrt(x * x + y * y + z * z); } }
        public double MagnitudeSqr { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return (x * x + y * y + z * z); } }

        public Vector3d Normalized
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { Vector3d result; Normalize(ref this, out result); return result; }
        }

        public double this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                switch (index)
                {
                    case 0: return x;
                    case 1: return y;
                    case 2: return z;
                    default: throw new IndexOutOfRangeException("Invalid Vector3d index!");
                }
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                switch (index)
                {
                    case 0: x = value; break;
                    case 1: y = value; break;
                    case 2: z = value; break;
                    default: throw new IndexOutOfRangeException("Invalid Vector3d index!");
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3d(double value) { this.x = value; this.y = value; this.z = value; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3d(Vector3 vector) { this.x = vector.x; this.y = vector.y; this.z = vector.z; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator +(Vector3d a, Vector3d b) { return new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator -(Vector3d a, Vector3d b) { return new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator *(Vector3d a, double d) { return new Vector3d(a.x * d, a.y * d, a.z * d); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator *(double d, Vector3d a) { return new Vector3d(a.x * d, a.y * d, a.z * d); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator /(Vector3d a, double d) { return new Vector3d(a.x / d, a.y / d, a.z / d); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3d operator -(Vector3d a) { return new Vector3d(-a.x, -a.y, -a.z); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Vector3d lhs, Vector3d rhs) { return (lhs - rhs).MagnitudeSqr < Epsilon; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Vector3d lhs, Vector3d rhs) { return (lhs - rhs).MagnitudeSqr >= Epsilon; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Vector3d(Vector3 v) { return new Vector3d(v.x, v.y, v.z); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Vector3(Vector3d v) { return new Vector3((float)v.x, (float)v.y, (float)v.z); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Scale(ref Vector3d scale) { x *= scale.x; y *= scale.y; z *= scale.z; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Normalize()
        {
            double mag = this.Magnitude;
            if (mag > Epsilon) { x /= mag; y /= mag; z /= mag; }
            else x = y = z = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clamp(double min, double max)
        {
            if (x < min) x = min; else if (x > max) x = max;
            if (y < min) y = min; else if (y > max) y = max;
            if (z < min) z = min; else if (z > max) z = max;
        }

        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() << 2 ^ z.GetHashCode() >> 2;

        public override bool Equals(object other) { if (!(other is Vector3d)) return false; Vector3d vector = (Vector3d)other; return (x == vector.x && y == vector.y && z == vector.z); }

        public bool Equals(Vector3d other) => (x == other.x && y == other.y && z == other.z);

        public override string ToString() => string.Format("({0:F1}, {1:F1}, {2:F1})", x, y, z);

        public string ToString(string format) => string.Format("({0}, {1}, {2})", x.ToString(format), y.ToString(format), z.ToString(format));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Dot(ref Vector3d lhs, ref Vector3d rhs) => lhs.x * rhs.x + lhs.y * rhs.y + lhs.z * rhs.z;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Cross(ref Vector3d lhs, ref Vector3d rhs, out Vector3d result) { result = new Vector3d(lhs.y * rhs.z - lhs.z * rhs.y, lhs.z * rhs.x - lhs.x * rhs.z, lhs.x * rhs.y - lhs.y * rhs.x); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Angle(ref Vector3d from, ref Vector3d to)
        {
            Vector3d fromNormalized = from.Normalized;
            Vector3d toNormalized = to.Normalized;
            return System.Math.Acos(MathHelper.Clamp(Vector3d.Dot(ref fromNormalized, ref toNormalized), -1.0, 1.0)) * MathHelper.Rad2Degd;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Lerp(ref Vector3d a, ref Vector3d b, double t, out Vector3d result) { result = new Vector3d(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(ref Vector3d a, ref Vector3d b, out Vector3d result) { result = new Vector3d(a.x * b.x, a.y * b.y, a.z * b.z); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Normalize(ref Vector3d value, out Vector3d result)
        {
            double mag = value.Magnitude;
            if (mag > Epsilon) result = new Vector3d(value.x / mag, value.y / mag, value.z / mag);
            else result = Vector3d.zero;
        }
    }

    public static class MathHelper
    {
        public const float PI = 3.14159274f;
        public const double PId = 3.1415926535897932384626433832795;
        public const float Deg2Rad = PI / 180f;
        public const double Deg2Radd = PId / 180.0;
        public const float Rad2Deg = 180f / PI;
        public const double Rad2Degd = 180.0 / PId;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Min(double val1, double val2, double val3) => (val1 < val2 ? (val1 < val3 ? val1 : val3) : (val2 < val3 ? val2 : val3));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Clamp(double value, double min, double max) => (value >= min ? (value <= max ? value : max) : min);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double TriangleArea(ref Vector3d p0, ref Vector3d p1, ref Vector3d p2)
        {
            var dx = p1 - p0;
            var dy = p2 - p0;
            return dx.Magnitude * (Math.Sin(Vector3d.Angle(ref dx, ref dy) * Deg2Radd) * dy.Magnitude) * 0.5f;
        }
    }

    public static class MeshUtils
    {
        public const int UVChannelCount = 4;

        public static Mesh CreateMesh(Vector3[] vertices, int[][] indices, Vector3[] normals, Vector4[] tangents, Color[] colors, BoneWeight[] boneWeights, List<Vector2>[] uvs2D, List<Vector3>[] uvs3D, List<Vector4>[] uvs4D, Matrix4x4[] bindposes, BlendShape[] blendShapes)
        {
            var newMesh = new Mesh();
            int subMeshCount = indices.Length;
            newMesh.subMeshCount = subMeshCount;
            newMesh.vertices = vertices;
            if (normals != null && normals.Length > 0) newMesh.normals = normals;
            if (tangents != null && tangents.Length > 0) newMesh.tangents = tangents;
            if (colors != null && colors.Length > 0) newMesh.colors = colors;
            if (boneWeights != null && boneWeights.Length > 0) newMesh.boneWeights = boneWeights;
            if (uvs2D != null)
            {
                for (int uvChannel = 0; uvChannel < uvs2D.Length; uvChannel++)
                    if (uvs2D[uvChannel] != null && uvs2D[uvChannel].Count > 0)
                        newMesh.SetUVs(uvChannel, uvs2D[uvChannel]);
            }
            if (uvs3D != null)
            {
                for (int uvChannel = 0; uvChannel < uvs3D.Length; uvChannel++)
                    if (uvs3D[uvChannel] != null && uvs3D[uvChannel].Count > 0)
                        newMesh.SetUVs(uvChannel, uvs3D[uvChannel]);
            }
            if (uvs4D != null)
            {
                for (int uvChannel = 0; uvChannel < uvs4D.Length; uvChannel++)
                    if (uvs4D[uvChannel] != null && uvs4D[uvChannel].Count > 0)
                        newMesh.SetUVs(uvChannel, uvs4D[uvChannel]);
            }
            if (blendShapes != null) ApplyMeshBlendShapes(newMesh, blendShapes);
            if (bindposes != null && bindposes.Length > 0) newMesh.bindposes = bindposes;
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
                newMesh.SetTriangles(indices[subMeshIndex], subMeshIndex, false);
            newMesh.RecalculateBounds();
            return newMesh;
        }

        public static BlendShape[] GetMeshBlendShapes(Mesh mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            int vertexCount = mesh.vertexCount;
            int blendShapeCount = mesh.blendShapeCount;
            if (blendShapeCount == 0) return null;
            var blendShapes = new BlendShape[blendShapeCount];
            for (int blendShapeIndex = 0; blendShapeIndex < blendShapeCount; blendShapeIndex++)
            {
                string shapeName = mesh.GetBlendShapeName(blendShapeIndex);
                int frameCount = mesh.GetBlendShapeFrameCount(blendShapeIndex);
                var frames = new BlendShapeFrame[frameCount];
                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    float frameWeight = mesh.GetBlendShapeFrameWeight(blendShapeIndex, frameIndex);
                    var deltaVertices = new Vector3[vertexCount];
                    var deltaNormals = new Vector3[vertexCount];
                    var deltaTangents = new Vector3[vertexCount];
                    mesh.GetBlendShapeFrameVertices(blendShapeIndex, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                    frames[frameIndex] = new BlendShapeFrame(frameWeight, deltaVertices, deltaNormals, deltaTangents);
                }
                blendShapes[blendShapeIndex] = new BlendShape(shapeName, frames);
            }
            return blendShapes;
        }

        public static void ApplyMeshBlendShapes(Mesh mesh, BlendShape[] blendShapes)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            mesh.ClearBlendShapes();
            if (blendShapes == null || blendShapes.Length == 0) return;
            for (int blendShapeIndex = 0; blendShapeIndex < blendShapes.Length; blendShapeIndex++)
            {
                string shapeName = blendShapes[blendShapeIndex].ShapeName;
                var frames = blendShapes[blendShapeIndex].Frames;
                if (frames != null)
                    for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                        mesh.AddBlendShapeFrame(shapeName, frames[frameIndex].FrameWeight, frames[frameIndex].DeltaVertices, frames[frameIndex].DeltaNormals, frames[frameIndex].DeltaTangents);
            }
        }

        public static List<Vector4>[] GetMeshUVs(Mesh mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            var uvs = new List<Vector4>[UVChannelCount];
            for (int channel = 0; channel < UVChannelCount; channel++)
                uvs[channel] = GetMeshUVs(mesh, channel);
            return uvs;
        }

        public static List<Vector4> GetMeshUVs(Mesh mesh, int channel)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            else if (channel < 0 || channel >= UVChannelCount) throw new ArgumentOutOfRangeException(nameof(channel));
            var uvList = new List<Vector4>(mesh.vertexCount);
            mesh.GetUVs(channel, uvList);
            return uvList;
        }

        public static int GetUsedUVComponents(List<Vector4> uvs)
        {
            if (uvs == null || uvs.Count == 0) return 0;
            int usedComponents = 1;
            foreach (var uv in uvs)
            {
                if (usedComponents < 2 && uv.y != 0f) usedComponents = 2;
                if (usedComponents < 3 && uv.z != 0f) usedComponents = 3;
                if (usedComponents < 4 && uv.w != 0f) { usedComponents = 4; break; }
            }
            return usedComponents;
        }

        public static Vector2[] ConvertUVsTo2D(List<Vector4> uvs)
        {
            if (uvs == null) return null;
            var uv2D = new Vector2[uvs.Count];
            for (int i = 0; i < uv2D.Length; i++)
            {
                var uv = uvs[i];
                uv2D[i] = new Vector2(uv.x, uv.y);
            }
            return uv2D;
        }

        public static Vector3[] ConvertUVsTo3D(List<Vector4> uvs)
        {
            if (uvs == null) return null;
            var uv3D = new Vector3[uvs.Count];
            for (int i = 0; i < uv3D.Length; i++)
            {
                var uv = uvs[i];
                uv3D[i] = new Vector3(uv.x, uv.y, uv.z);
            }
            return uv3D;
        }
    }
}
#endif