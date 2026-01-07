// =============================================================================
// SampleDataGenerator.cs - Generate Test Data for Pipeline Testing
// =============================================================================
using System;
using System.IO;
using UnityEngine;

namespace SMRWelding.Utilities
{
    /// <summary>
    /// Generates sample point cloud data for testing
    /// </summary>
    public static class SampleDataGenerator
    {
        /// <summary>
        /// Generate hemisphere point cloud (simulates dome scan)
        /// </summary>
        public static Vector3[] GenerateHemisphere(int pointCount, float radius = 0.5f, float noise = 0.002f)
        {
            var points = new Vector3[pointCount];
            
            for (int i = 0; i < pointCount; i++)
            {
                float u = UnityEngine.Random.value;
                float v = UnityEngine.Random.value;

                float theta = 2 * Mathf.PI * u;
                float phi = Mathf.Acos(v); // hemisphere

                float x = radius * Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = radius * Mathf.Cos(phi);
                float z = radius * Mathf.Sin(phi) * Mathf.Sin(theta);

                // Add noise
                x += UnityEngine.Random.Range(-noise, noise);
                y += UnityEngine.Random.Range(-noise, noise);
                z += UnityEngine.Random.Range(-noise, noise);

                points[i] = new Vector3(x, y, z);
            }

            return points;
        }

        /// <summary>
        /// Generate cylinder point cloud (simulates pipe scan)
        /// </summary>
        public static Vector3[] GenerateCylinder(int pointCount, float radius = 0.3f, float height = 1.0f, float noise = 0.002f)
        {
            var points = new Vector3[pointCount];
            
            for (int i = 0; i < pointCount; i++)
            {
                float theta = UnityEngine.Random.value * 2 * Mathf.PI;
                float h = UnityEngine.Random.value * height - height / 2;

                float x = radius * Mathf.Cos(theta);
                float y = h;
                float z = radius * Mathf.Sin(theta);

                // Add noise
                x += UnityEngine.Random.Range(-noise, noise);
                y += UnityEngine.Random.Range(-noise, noise);
                z += UnityEngine.Random.Range(-noise, noise);

                points[i] = new Vector3(x, y, z);
            }

            return points;
        }

        /// <summary>
        /// Generate torus point cloud (simulates pipe elbow)
        /// </summary>
        public static Vector3[] GenerateTorus(int pointCount, float majorRadius = 0.4f, float minorRadius = 0.1f, float noise = 0.002f)
        {
            var points = new Vector3[pointCount];
            
            for (int i = 0; i < pointCount; i++)
            {
                float u = UnityEngine.Random.value * 2 * Mathf.PI;
                float v = UnityEngine.Random.value * 2 * Mathf.PI;

                float x = (majorRadius + minorRadius * Mathf.Cos(v)) * Mathf.Cos(u);
                float y = minorRadius * Mathf.Sin(v);
                float z = (majorRadius + minorRadius * Mathf.Cos(v)) * Mathf.Sin(u);

                x += UnityEngine.Random.Range(-noise, noise);
                y += UnityEngine.Random.Range(-noise, noise);
                z += UnityEngine.Random.Range(-noise, noise);

                points[i] = new Vector3(x, y, z);
            }

            return points;
        }

        /// <summary>
        /// Generate planar weld seam point cloud
        /// </summary>
        public static Vector3[] GenerateWeldSeam(int pointCount, float length = 1.0f, float width = 0.05f, float noise = 0.001f)
        {
            var points = new Vector3[pointCount];
            
            for (int i = 0; i < pointCount; i++)
            {
                float t = (float)i / pointCount;
                float x = t * length - length / 2;
                float y = UnityEngine.Random.Range(-width / 2, width / 2);
                float z = Mathf.Sin(t * Mathf.PI) * 0.02f; // slight curve

                x += UnityEngine.Random.Range(-noise, noise);
                y += UnityEngine.Random.Range(-noise, noise);
                z += UnityEngine.Random.Range(-noise, noise);

                points[i] = new Vector3(x, y, z);
            }

            return points;
        }

        /// <summary>
        /// Generate SMR vessel segment (complex shape)
        /// </summary>
        public static Vector3[] GenerateSMRVesselSegment(int pointCount, float outerRadius = 0.5f, float thickness = 0.05f, float height = 0.8f)
        {
            var points = new Vector3[pointCount];
            int halfCount = pointCount / 2;

            // Outer surface
            for (int i = 0; i < halfCount; i++)
            {
                float theta = UnityEngine.Random.value * 2 * Mathf.PI;
                float h = UnityEngine.Random.value * height - height / 2;

                float x = outerRadius * Mathf.Cos(theta);
                float y = h;
                float z = outerRadius * Mathf.Sin(theta);

                x += UnityEngine.Random.Range(-0.002f, 0.002f);
                y += UnityEngine.Random.Range(-0.002f, 0.002f);
                z += UnityEngine.Random.Range(-0.002f, 0.002f);

                points[i] = new Vector3(x, y, z);
            }

            // Inner surface
            float innerRadius = outerRadius - thickness;
            for (int i = halfCount; i < pointCount; i++)
            {
                float theta = UnityEngine.Random.value * 2 * Mathf.PI;
                float h = UnityEngine.Random.value * height - height / 2;

                float x = innerRadius * Mathf.Cos(theta);
                float y = h;
                float z = innerRadius * Mathf.Sin(theta);

                x += UnityEngine.Random.Range(-0.002f, 0.002f);
                y += UnityEngine.Random.Range(-0.002f, 0.002f);
                z += UnityEngine.Random.Range(-0.002f, 0.002f);

                points[i] = new Vector3(x, y, z);
            }

            return points;
        }

        /// <summary>
        /// Calculate normals for point cloud (simple estimation)
        /// </summary>
        public static Vector3[] EstimateNormals(Vector3[] points, Vector3 center)
        {
            var normals = new Vector3[points.Length];
            
            for (int i = 0; i < points.Length; i++)
            {
                normals[i] = (points[i] - center).normalized;
            }

            return normals;
        }

        /// <summary>
        /// Save point cloud to PLY file
        /// </summary>
        public static void SaveToPLY(string path, Vector3[] points, Vector3[] normals = null, Color[] colors = null)
        {
            using (var writer = new StreamWriter(path))
            {
                // Header
                writer.WriteLine("ply");
                writer.WriteLine("format ascii 1.0");
                writer.WriteLine($"element vertex {points.Length}");
                writer.WriteLine("property float x");
                writer.WriteLine("property float y");
                writer.WriteLine("property float z");

                bool hasNormals = normals != null && normals.Length == points.Length;
                bool hasColors = colors != null && colors.Length == points.Length;

                if (hasNormals)
                {
                    writer.WriteLine("property float nx");
                    writer.WriteLine("property float ny");
                    writer.WriteLine("property float nz");
                }

                if (hasColors)
                {
                    writer.WriteLine("property uchar red");
                    writer.WriteLine("property uchar green");
                    writer.WriteLine("property uchar blue");
                }

                writer.WriteLine("end_header");

                // Data
                for (int i = 0; i < points.Length; i++)
                {
                    var p = points[i];
                    string line = $"{p.x:F6} {p.y:F6} {p.z:F6}";

                    if (hasNormals)
                    {
                        var n = normals[i];
                        line += $" {n.x:F6} {n.y:F6} {n.z:F6}";
                    }

                    if (hasColors)
                    {
                        var c = colors[i];
                        int r = Mathf.Clamp((int)(c.r * 255), 0, 255);
                        int g = Mathf.Clamp((int)(c.g * 255), 0, 255);
                        int b = Mathf.Clamp((int)(c.b * 255), 0, 255);
                        line += $" {r} {g} {b}";
                    }

                    writer.WriteLine(line);
                }
            }

            Debug.Log($"Saved {points.Length} points to {path}");
        }

        /// <summary>
        /// Generate gradient colors based on height
        /// </summary>
        public static Color[] GenerateHeightColors(Vector3[] points, Color lowColor, Color highColor)
        {
            var colors = new Color[points.Length];
            
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            foreach (var p in points)
            {
                minY = Mathf.Min(minY, p.y);
                maxY = Mathf.Max(maxY, p.y);
            }

            float range = maxY - minY;
            if (range < 0.001f) range = 1f;

            for (int i = 0; i < points.Length; i++)
            {
                float t = (points[i].y - minY) / range;
                colors[i] = Color.Lerp(lowColor, highColor, t);
            }

            return colors;
        }
    }
}
