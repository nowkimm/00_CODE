// =============================================================================
// MeshData.cs - Mesh Data Structure for SMR Welding
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SMRWelding.Mesh
{
    /// <summary>
    /// Reconstructed mesh data container
    /// </summary>
    [Serializable]
    public class MeshData
    {
        public Vector3[] Vertices;
        public int[] Triangles;
        public Vector3[] Normals;
        public Vector2[] UVs;
        public Bounds Bounds;
        public float SurfaceArea;
        public float Volume;
        public DateTime CreationTime;

        public int VertexCount => Vertices?.Length ?? 0;
        public int TriangleCount => Triangles != null ? Triangles.Length / 3 : 0;
        public bool HasNormals => Normals != null && Normals.Length == VertexCount;
        public bool HasUVs => UVs != null && UVs.Length == VertexCount;

        public MeshData() { CreationTime = DateTime.Now; }

        public MeshData(Vector3[] vertices, int[] triangles)
        {
            Vertices = vertices;
            Triangles = triangles;
            CreationTime = DateTime.Now;
            CalculateBounds();
        }

        /// <summary>
        /// Calculate bounding box
        /// </summary>
        public void CalculateBounds()
        {
            if (Vertices == null || Vertices.Length == 0)
            {
                Bounds = new Bounds();
                return;
            }

            Vector3 min = Vertices[0];
            Vector3 max = Vertices[0];

            for (int i = 1; i < Vertices.Length; i++)
            {
                min = Vector3.Min(min, Vertices[i]);
                max = Vector3.Max(max, Vertices[i]);
            }

            Bounds = new Bounds((min + max) * 0.5f, max - min);
        }

        /// <summary>
        /// Calculate normals from triangles
        /// </summary>
        public void RecalculateNormals()
        {
            if (Vertices == null || Triangles == null) return;

            Normals = new Vector3[Vertices.Length];
            int[] counts = new int[Vertices.Length];

            for (int i = 0; i < Triangles.Length; i += 3)
            {
                int i0 = Triangles[i];
                int i1 = Triangles[i + 1];
                int i2 = Triangles[i + 2];

                Vector3 v0 = Vertices[i0];
                Vector3 v1 = Vertices[i1];
                Vector3 v2 = Vertices[i2];

                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                Normals[i0] += normal; counts[i0]++;
                Normals[i1] += normal; counts[i1]++;
                Normals[i2] += normal; counts[i2]++;
            }

            for (int i = 0; i < Normals.Length; i++)
            {
                if (counts[i] > 0)
                    Normals[i] = Normals[i].normalized;
            }
        }

        /// <summary>
        /// Calculate surface area
        /// </summary>
        public float CalculateSurfaceArea()
        {
            if (Vertices == null || Triangles == null) return 0f;

            float area = 0f;
            for (int i = 0; i < Triangles.Length; i += 3)
            {
                Vector3 v0 = Vertices[Triangles[i]];
                Vector3 v1 = Vertices[Triangles[i + 1]];
                Vector3 v2 = Vertices[Triangles[i + 2]];

                area += Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            }

            SurfaceArea = area;
            return area;
        }

        /// <summary>
        /// Simplify mesh by merging close vertices
        /// </summary>
        public MeshData Simplify(float mergeDistance)
        {
            if (Vertices == null || Vertices.Length == 0) return this;

            var vertexMap = new Dictionary<Vector3Int, int>();
            var newVertices = new List<Vector3>();
            var indexRemap = new int[Vertices.Length];
            float invDist = 1f / mergeDistance;

            for (int i = 0; i < Vertices.Length; i++)
            {
                Vector3Int key = new Vector3Int(
                    Mathf.RoundToInt(Vertices[i].x * invDist),
                    Mathf.RoundToInt(Vertices[i].y * invDist),
                    Mathf.RoundToInt(Vertices[i].z * invDist)
                );

                if (!vertexMap.TryGetValue(key, out int newIndex))
                {
                    newIndex = newVertices.Count;
                    vertexMap[key] = newIndex;
                    newVertices.Add(Vertices[i]);
                }
                indexRemap[i] = newIndex;
            }

            var newTriangles = new List<int>();
            for (int i = 0; i < Triangles.Length; i += 3)
            {
                int i0 = indexRemap[Triangles[i]];
                int i1 = indexRemap[Triangles[i + 1]];
                int i2 = indexRemap[Triangles[i + 2]];

                if (i0 != i1 && i1 != i2 && i2 != i0)
                {
                    newTriangles.Add(i0);
                    newTriangles.Add(i1);
                    newTriangles.Add(i2);
                }
            }

            var result = new MeshData(newVertices.ToArray(), newTriangles.ToArray());
            result.RecalculateNormals();
            return result;
        }

        /// <summary>
        /// Convert to Unity Mesh
        /// </summary>
        public UnityEngine.Mesh ToUnityMesh(string name = "GeneratedMesh")
        {
            var mesh = new UnityEngine.Mesh { name = name };

            if (VertexCount > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = Vertices;
            mesh.triangles = Triangles;

            if (HasNormals)
                mesh.normals = Normals;
            else
                mesh.RecalculateNormals();

            if (HasUVs)
                mesh.uv = UVs;

            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Create from Unity Mesh
        /// </summary>
        public static MeshData FromUnityMesh(UnityEngine.Mesh mesh)
        {
            return new MeshData
            {
                Vertices = mesh.vertices,
                Triangles = mesh.triangles,
                Normals = mesh.normals,
                UVs = mesh.uv,
                Bounds = mesh.bounds
            };
        }
    }
}
