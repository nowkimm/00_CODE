// =============================================================================
// FileUtilities.cs - File I/O Utility Functions
// =============================================================================
using System;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;

namespace SMRWelding.Utilities
{
    /// <summary>
    /// File I/O utilities for point cloud and mesh data
    /// </summary>
    public static class FileUtilities
    {
        /// <summary>
        /// Supported point cloud formats
        /// </summary>
        public enum PointCloudFormat
        {
            PLY,
            PCD,
            XYZ,
            Unknown
        }

        /// <summary>
        /// Detect point cloud format from file extension
        /// </summary>
        public static PointCloudFormat DetectFormat(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".ply" => PointCloudFormat.PLY,
                ".pcd" => PointCloudFormat.PCD,
                ".xyz" => PointCloudFormat.XYZ,
                ".txt" => PointCloudFormat.XYZ,
                _ => PointCloudFormat.Unknown
            };
        }

        /// <summary>
        /// Load points from XYZ file (simple text format)
        /// </summary>
        public static Vector3[] LoadXYZ(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            var lines = File.ReadAllLines(filePath);
            var points = new System.Collections.Generic.List<Vector3>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("#") || line.StartsWith("//")) continue;

                var parts = line.Split(new[] { ' ', '\t', ',' }, 
                    StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length >= 3 &&
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                {
                    points.Add(new Vector3(x, y, z));
                }
            }

            return points.ToArray();
        }

        /// <summary>
        /// Save points to XYZ file
        /// </summary>
        public static void SaveXYZ(string filePath, Vector3[] points)
        {
            if (points == null || points.Length == 0)
                throw new ArgumentException("Points array is empty");

            var sb = new StringBuilder();
            sb.AppendLine("# XYZ point cloud exported from SMR Welding");
            sb.AppendLine($"# Points: {points.Length}");

            foreach (var p in points)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, 
                    "{0:F6} {1:F6} {2:F6}", p.x, p.y, p.z));
            }

            File.WriteAllText(filePath, sb.ToString());
        }

        /// <summary>
        /// Save mesh to OBJ file
        /// </summary>
        public static void SaveOBJ(string filePath, Mesh mesh)
        {
            if (mesh == null)
                throw new ArgumentNullException(nameof(mesh));

            var sb = new StringBuilder();
            sb.AppendLine("# OBJ file exported from SMR Welding");
            sb.AppendLine($"# Vertices: {mesh.vertexCount}");
            sb.AppendLine($"# Triangles: {mesh.triangles.Length / 3}");

            // Vertices
            foreach (var v in mesh.vertices)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "v {0:F6} {1:F6} {2:F6}", v.x, v.y, v.z));
            }

            // Normals
            if (mesh.normals != null && mesh.normals.Length == mesh.vertexCount)
            {
                foreach (var n in mesh.normals)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "vn {0:F6} {1:F6} {2:F6}", n.x, n.y, n.z));
                }
            }

            // UVs
            if (mesh.uv != null && mesh.uv.Length == mesh.vertexCount)
            {
                foreach (var uv in mesh.uv)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "vt {0:F6} {1:F6}", uv.x, uv.y));
                }
            }

            // Faces (1-indexed)
            var triangles = mesh.triangles;
            bool hasNormals = mesh.normals != null && mesh.normals.Length > 0;
            bool hasUVs = mesh.uv != null && mesh.uv.Length > 0;

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int i1 = triangles[i] + 1;
                int i2 = triangles[i + 1] + 1;
                int i3 = triangles[i + 2] + 1;

                if (hasNormals && hasUVs)
                    sb.AppendLine($"f {i1}/{i1}/{i1} {i2}/{i2}/{i2} {i3}/{i3}/{i3}");
                else if (hasNormals)
                    sb.AppendLine($"f {i1}//{i1} {i2}//{i2} {i3}//{i3}");
                else
                    sb.AppendLine($"f {i1} {i2} {i3}");
            }

            File.WriteAllText(filePath, sb.ToString());
        }

        /// <summary>
        /// Save trajectory to CSV file
        /// </summary>
        public static void SaveTrajectoryCSV(string filePath, double[][] trajectory, 
            Vector3[] positions = null)
        {
            if (trajectory == null || trajectory.Length == 0)
                throw new ArgumentException("Trajectory is empty");

            var sb = new StringBuilder();
            
            // Header
            if (positions != null)
                sb.AppendLine("Index,J1,J2,J3,J4,J5,J6,X,Y,Z");
            else
                sb.AppendLine("Index,J1,J2,J3,J4,J5,J6");

            for (int i = 0; i < trajectory.Length; i++)
            {
                var joints = trajectory[i];
                sb.Append($"{i}");

                foreach (var j in joints)
                    sb.Append(string.Format(CultureInfo.InvariantCulture, ",{0:F6}", j));

                if (positions != null && i < positions.Length)
                {
                    sb.Append(string.Format(CultureInfo.InvariantCulture,
                        ",{0:F6},{1:F6},{2:F6}", 
                        positions[i].x, positions[i].y, positions[i].z));
                }

                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString());
        }

        /// <summary>
        /// Load trajectory from CSV file
        /// </summary>
        public static double[][] LoadTrajectoryCSV(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            var lines = File.ReadAllLines(filePath);
            var trajectory = new System.Collections.Generic.List<double[]>();

            bool isHeader = true;
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                // Skip header
                if (isHeader)
                {
                    isHeader = false;
                    continue;
                }

                var parts = line.Split(',');
                if (parts.Length < 7) continue; // Need at least index + 6 joints

                var joints = new double[6];
                bool valid = true;
                
                for (int i = 0; i < 6; i++)
                {
                    if (!double.TryParse(parts[i + 1], NumberStyles.Float, 
                        CultureInfo.InvariantCulture, out joints[i]))
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid)
                    trajectory.Add(joints);
            }

            return trajectory.ToArray();
        }

        /// <summary>
        /// Ensure directory exists
        /// </summary>
        public static void EnsureDirectory(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// Get unique filename (append number if exists)
        /// </summary>
        public static string GetUniqueFilename(string basePath)
        {
            if (!File.Exists(basePath))
                return basePath;

            string dir = Path.GetDirectoryName(basePath);
            string name = Path.GetFileNameWithoutExtension(basePath);
            string ext = Path.GetExtension(basePath);

            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(dir ?? "", $"{name}_{counter}{ext}");
                counter++;
            } while (File.Exists(newPath) && counter < 1000);

            return newPath;
        }
    }
}
