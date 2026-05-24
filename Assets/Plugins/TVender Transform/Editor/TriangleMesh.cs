using UnityEngine;

namespace TVender.VTransform
{
    [HideInInspector]
    public class TriangleMesh
    {
        public Vector3[] Vertices;

        public Vector3[] Normals;

        public int[] Triangles = new int[3] { 0, 1, 2 };

        public TriangleMesh()
        {
            Vertices = new Vector3[3];
            Normals = new Vector3[3];
        }

        public void SetVertices(Vector3 p0, Vector3 p1, Vector3 p2)
        {
            Vertices[0] = p0;
            Vertices[1] = p1;
            Vertices[2] = p2;
        }

        public void SetNormals(Vector3 n0, Vector3 n1, Vector3 n2)
        {
            Normals[0] = n0;
            Normals[1] = n1;
            Normals[2] = n2;
        }

        public Mesh GetMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = Vertices;
            mesh.normals = Normals;
            mesh.triangles = Triangles;

            return mesh;
        }
    }
}