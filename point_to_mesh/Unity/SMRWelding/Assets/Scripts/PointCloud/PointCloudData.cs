// =============================================================================
// PointCloudData.cs - Point Cloud Data Structure
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SMRWelding.PointCloud
{
    /// <summary>
    /// Point cloud data container
    /// </summary>
    [Serializable]
    public class PointCloudData
    {
        public Vector3[] Points;
        public Vector3[] Normals;
        public Color[] Colors;
        public Bounds Bounds;
        public string SourceFile;
        public DateTime LoadTime;

        public int Count => Points?.Length ?? 0;
        public bool HasNormals => Normals != null && Normals.Length == Count;
        public bool HasColors => Colors != null && Colors.Length == Count;

        public PointCloudData() { }

        public PointCloudData(Vector3[] points)
        {
            Points = points;
            CalculateBounds();
            LoadTime = DateTime.Now;
        }

        public PointCloudData(Vector3[] points, Vector3[] normals) : this(points)
        {
            Normals = normals;
        }

        public PointCloudData(Vector3[] points, Vector3[] normals, Color[] colors) : this(points, normals)
        {
            Colors = colors;
        }

        /// <summary>
        /// Calculate bounding box
        /// </summary>
        public void CalculateBounds()
        {
            if (Points == null || Points.Length == 0)
            {
                Bounds = new Bounds();
                return;
            }

            Vector3 min = Points[0];
            Vector3 max = Points[0];

            for (int i = 1; i < Points.Length; i++)
            {
                min = Vector3.Min(min, Points[i]);
                max = Vector3.Max(max, Points[i]);
            }

            Bounds = new Bounds((min + max) * 0.5f, max - min);
        }

        /// <summary>
        /// Downsample point cloud using voxel grid
        /// </summary>
        public PointCloudData Downsample(float voxelSize)
        {
            if (Points == null || Points.Length == 0) return this;

            var voxelMap = new Dictionary<Vector3Int, List<int>>();

            for (int i = 0; i < Points.Length; i++)
            {
                Vector3Int key = new Vector3Int(
                    Mathf.FloorToInt(Points[i].x / voxelSize),
                    Mathf.FloorToInt(Points[i].y / voxelSize),
                    Mathf.FloorToInt(Points[i].z / voxelSize)
                );

                if (!voxelMap.ContainsKey(key))
                    voxelMap[key] = new List<int>();
                voxelMap[key].Add(i);
            }

            var newPoints = new List<Vector3>();
            var newNormals = HasNormals ? new List<Vector3>() : null;
            var newColors = HasColors ? new List<Color>() : null;

            foreach (var kvp in voxelMap)
            {
                Vector3 avgPoint = Vector3.zero;
                Vector3 avgNormal = Vector3.zero;
                Color avgColor = Color.black;

                foreach (int idx in kvp.Value)
                {
                    avgPoint += Points[idx];
                    if (HasNormals) avgNormal += Normals[idx];
                    if (HasColors) avgColor += Colors[idx];
                }

                int count = kvp.Value.Count;
                newPoints.Add(avgPoint / count);
                if (HasNormals) newNormals.Add((avgNormal / count).normalized);
                if (HasColors) newColors.Add(avgColor / count);
            }

            return new PointCloudData(
                newPoints.ToArray(),
                newNormals?.ToArray(),
                newColors?.ToArray()
            )
            {
                SourceFile = SourceFile,
                LoadTime = DateTime.Now
            };
        }

        /// <summary>
        /// Transform all points
        /// </summary>
        public void Transform(Matrix4x4 matrix)
        {
            if (Points == null) return;

            for (int i = 0; i < Points.Length; i++)
            {
                Points[i] = matrix.MultiplyPoint3x4(Points[i]);
            }

            if (HasNormals)
            {
                Matrix4x4 normalMatrix = matrix.inverse.transpose;
                for (int i = 0; i < Normals.Length; i++)
                {
                    Normals[i] = normalMatrix.MultiplyVector(Normals[i]).normalized;
                }
            }

            CalculateBounds();
        }

        /// <summary>
        /// Get subset of points
        /// </summary>
        public PointCloudData GetSubset(int startIndex, int count)
        {
            count = Mathf.Min(count, Count - startIndex);
            if (count <= 0) return new PointCloudData();

            var subPoints = new Vector3[count];
            Array.Copy(Points, startIndex, subPoints, 0, count);

            Vector3[] subNormals = null;
            if (HasNormals)
            {
                subNormals = new Vector3[count];
                Array.Copy(Normals, startIndex, subNormals, 0, count);
            }

            Color[] subColors = null;
            if (HasColors)
            {
                subColors = new Color[count];
                Array.Copy(Colors, startIndex, subColors, 0, count);
            }

            return new PointCloudData(subPoints, subNormals, subColors);
        }

        /// <summary>
        /// Create from Unity mesh vertices
        /// </summary>
        public static PointCloudData FromMesh(Mesh mesh)
        {
            return new PointCloudData(
                mesh.vertices,
                mesh.normals,
                mesh.colors.Length > 0 ? mesh.colors : null
            );
        }
    }
}
