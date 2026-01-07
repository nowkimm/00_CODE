// =============================================================================
// ResultExporter.cs - Export Pipeline Results to Various Formats
// =============================================================================
using System;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;

namespace SMRWelding.Utilities
{
    /// <summary>
    /// Exports pipeline results (paths, trajectories, meshes) to files
    /// </summary>
    public static class ResultExporter
    {
        #region Path Export

        /// <summary>
        /// Export welding path to CSV format
        /// </summary>
        public static void ExportPathToCSV(Vector3[] positions, Vector3[] normals, string filePath)
        {
            if (positions == null || positions.Length == 0)
            {
                Debug.LogError("ResultExporter: No path data to export");
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Index,X,Y,Z,NormalX,NormalY,NormalZ");

                bool hasNormals = normals != null && normals.Length == positions.Length;

                for (int i = 0; i < positions.Length; i++)
                {
                    var p = positions[i];
                    if (hasNormals)
                    {
                        var n = normals[i];
                        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "{0},{1:F6},{2:F6},{3:F6},{4:F6},{5:F6},{6:F6}",
                            i, p.x, p.y, p.z, n.x, n.y, n.z));
                    }
                    else
                    {
                        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                            "{0},{1:F6},{2:F6},{3:F6},0,0,1", i, p.x, p.y, p.z));
                    }
                }

                File.WriteAllText(filePath, sb.ToString());
                Debug.Log($"ResultExporter: Path exported to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"ResultExporter: Failed to export path - {e.Message}");
            }
        }

        /// <summary>
        /// Export welding path to JSON format
        /// </summary>
        public static void ExportPathToJSON(Vector3[] positions, Vector3[] normals, string filePath)
        {
            if (positions == null || positions.Length == 0)
            {
                Debug.LogError("ResultExporter: No path data to export");
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"weldingPath\": {");
                sb.AppendLine($"    \"pointCount\": {positions.Length},");
                sb.AppendLine("    \"points\": [");

                bool hasNormals = normals != null && normals.Length == positions.Length;

                for (int i = 0; i < positions.Length; i++)
                {
                    var p = positions[i];
                    sb.Append("      { ");
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "\"x\": {0:F6}, \"y\": {1:F6}, \"z\": {2:F6}", p.x, p.y, p.z);

                    if (hasNormals)
                    {
                        var n = normals[i];
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            ", \"nx\": {0:F6}, \"ny\": {1:F6}, \"nz\": {2:F6}", n.x, n.y, n.z);
                    }

                    sb.Append(" }");
                    if (i < positions.Length - 1) sb.Append(",");
                    sb.AppendLine();
                }

                sb.AppendLine("    ]");
                sb.AppendLine("  }");
                sb.AppendLine("}");

                File.WriteAllText(filePath, sb.ToString());
                Debug.Log($"ResultExporter: Path exported to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"ResultExporter: Failed to export path - {e.Message}");
            }
        }

        #endregion

        #region Trajectory Export

        /// <summary>
        /// Export robot trajectory to CSV (joint angles)
        /// </summary>
        public static void ExportTrajectoryToCSV(float[][] jointAngles, string filePath)
        {
            if (jointAngles == null || jointAngles.Length == 0)
            {
                Debug.LogError("ResultExporter: No trajectory data to export");
                return;
            }

            try
            {
                var sb = new StringBuilder();
                int numJoints = jointAngles[0].Length;

                // Header
                sb.Append("Index,Time");
                for (int j = 0; j < numJoints; j++)
                    sb.Append($",Joint{j + 1}_rad,Joint{j + 1}_deg");
                sb.AppendLine();

                // Data
                float timeStep = 0.01f; // 100Hz
                for (int i = 0; i < jointAngles.Length; i++)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0},{1:F4}", i, i * timeStep);
                    for (int j = 0; j < jointAngles[i].Length; j++)
                    {
                        float rad = jointAngles[i][j];
                        float deg = rad * Mathf.Rad2Deg;
                        sb.AppendFormat(CultureInfo.InvariantCulture, ",{0:F6},{1:F4}", rad, deg);
                    }
                    sb.AppendLine();
                }

                File.WriteAllText(filePath, sb.ToString());
                Debug.Log($"ResultExporter: Trajectory exported to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"ResultExporter: Failed to export trajectory - {e.Message}");
            }
        }

        /// <summary>
        /// Export trajectory to Universal Robot script format
        /// </summary>
        public static void ExportToURScript(float[][] jointAngles, string filePath, 
            float velocity = 1.0f, float acceleration = 1.0f)
        {
            if (jointAngles == null || jointAngles.Length == 0)
            {
                Debug.LogError("ResultExporter: No trajectory data to export");
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# SMR Welding Robot Program");
                sb.AppendLine($"# Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"# Points: {jointAngles.Length}");
                sb.AppendLine();
                sb.AppendLine("def smr_welding_program():");
                sb.AppendLine($"  set_tcp(p[0,0,0.15,0,0,0])");
                sb.AppendLine($"  set_payload(2.0)");
                sb.AppendLine();

                for (int i = 0; i < jointAngles.Length; i++)
                {
                    var j = jointAngles[i];
                    if (j.Length >= 6)
                    {
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "  movej([{0:F6},{1:F6},{2:F6},{3:F6},{4:F6},{5:F6}], a={6}, v={7})",
                            j[0], j[1], j[2], j[3], j[4], j[5], acceleration, velocity);
                        sb.AppendLine();
                    }
                }

                sb.AppendLine("end");
                sb.AppendLine();
                sb.AppendLine("smr_welding_program()");

                File.WriteAllText(filePath, sb.ToString());
                Debug.Log($"ResultExporter: URScript exported to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"ResultExporter: Failed to export URScript - {e.Message}");
            }
        }

        /// <summary>
        /// Export trajectory to KUKA KRL format
        /// </summary>
        public static void ExportToKRL(float[][] jointAngles, string filePath)
        {
            if (jointAngles == null || jointAngles.Length == 0)
            {
                Debug.LogError("ResultExporter: No trajectory data to export");
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("&ACCESS RVP");
                sb.AppendLine("&REL 1");
                sb.AppendLine("DEF SMR_WELDING()");
                sb.AppendLine();
                sb.AppendLine("; SMR Welding Program");
                sb.AppendLine($"; Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"; Points: {jointAngles.Length}");
                sb.AppendLine();
                sb.AppendLine("$VEL.CP = 0.2");
                sb.AppendLine("$APO.CDIS = 5");
                sb.AppendLine();

                for (int i = 0; i < jointAngles.Length; i++)
                {
                    var j = jointAngles[i];
                    if (j.Length >= 6)
                    {
                        // Convert to degrees for KUKA
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "PTP {{A1 {0:F2}, A2 {1:F2}, A3 {2:F2}, A4 {3:F2}, A5 {4:F2}, A6 {5:F2}}}",
                            j[0] * Mathf.Rad2Deg, j[1] * Mathf.Rad2Deg, j[2] * Mathf.Rad2Deg,
                            j[3] * Mathf.Rad2Deg, j[4] * Mathf.Rad2Deg, j[5] * Mathf.Rad2Deg);
                        sb.AppendLine();
                    }
                }

                sb.AppendLine();
                sb.AppendLine("END");

                File.WriteAllText(filePath, sb.ToString());
                Debug.Log($"ResultExporter: KRL exported to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"ResultExporter: Failed to export KRL - {e.Message}");
            }
        }

        #endregion

        #region Mesh Export

        /// <summary>
        /// Export Unity mesh to OBJ format
        /// </summary>
        public static void ExportMeshToOBJ(Mesh mesh, string filePath)
        {
            if (mesh == null)
            {
                Debug.LogError("ResultExporter: No mesh to export");
                return;
            }

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# SMR Welding Generated Mesh");
                sb.AppendLine($"# Vertices: {mesh.vertexCount}");
                sb.AppendLine($"# Triangles: {mesh.triangles.Length / 3}");
                sb.AppendLine();

                // Vertices
                foreach (var v in mesh.vertices)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "v {0:F6} {1:F6} {2:F6}\n", v.x, v.y, v.z);
                }

                // Normals
                if (mesh.normals != null && mesh.normals.Length > 0)
                {
                    foreach (var n in mesh.normals)
                    {
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "vn {0:F6} {1:F6} {2:F6}\n", n.x, n.y, n.z);
                    }
                }

                // UVs
                if (mesh.uv != null && mesh.uv.Length > 0)
                {
                    foreach (var uv in mesh.uv)
                    {
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "vt {0:F6} {1:F6}\n", uv.x, uv.y);
                    }
                }

                // Faces
                var tris = mesh.triangles;
                bool hasNormals = mesh.normals != null && mesh.normals.Length > 0;
                bool hasUVs = mesh.uv != null && mesh.uv.Length > 0;

                for (int i = 0; i < tris.Length; i += 3)
                {
                    int i1 = tris[i] + 1;
                    int i2 = tris[i + 1] + 1;
                    int i3 = tris[i + 2] + 1;

                    if (hasNormals && hasUVs)
                        sb.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", i1, i2, i3);
                    else if (hasNormals)
                        sb.AppendFormat("f {0}//{0} {1}//{1} {2}//{2}\n", i1, i2, i3);
                    else
                        sb.AppendFormat("f {0} {1} {2}\n", i1, i2, i3);
                }

                File.WriteAllText(filePath, sb.ToString());
                Debug.Log($"ResultExporter: Mesh exported to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"ResultExporter: Failed to export mesh - {e.Message}");
            }
        }

        /// <summary>
        /// Export mesh to STL format (binary)
        /// </summary>
        public static void ExportMeshToSTL(Mesh mesh, string filePath)
        {
            if (mesh == null)
            {
                Debug.LogError("ResultExporter: No mesh to export");
                return;
            }

            try
            {
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                int triCount = triangles.Length / 3;

                using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
                {
                    // Header (80 bytes)
                    byte[] header = new byte[80];
                    var headerText = Encoding.ASCII.GetBytes("SMR Welding STL Export");
                    Array.Copy(headerText, header, Math.Min(headerText.Length, 80));
                    writer.Write(header);

                    // Triangle count
                    writer.Write((uint)triCount);

                    // Triangles
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        var v0 = vertices[triangles[i]];
                        var v1 = vertices[triangles[i + 1]];
                        var v2 = vertices[triangles[i + 2]];

                        // Calculate normal
                        var normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                        // Normal
                        writer.Write(normal.x);
                        writer.Write(normal.y);
                        writer.Write(normal.z);

                        // Vertices
                        writer.Write(v0.x); writer.Write(v0.y); writer.Write(v0.z);
                        writer.Write(v1.x); writer.Write(v1.y); writer.Write(v1.z);
                        writer.Write(v2.x); writer.Write(v2.y); writer.Write(v2.z);

                        // Attribute byte count
                        writer.Write((ushort)0);
                    }
                }

                Debug.Log($"ResultExporter: STL exported to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"ResultExporter: Failed to export STL - {e.Message}");
            }
        }

        #endregion

        #region Report Export

        /// <summary>
        /// Export complete pipeline report
        /// </summary>
        public static void ExportReport(PipelineReport report, string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("========================================");
                sb.AppendLine("   SMR WELDING PIPELINE REPORT");
                sb.AppendLine("========================================");
                sb.AppendLine();
                sb.AppendLine($"Generated: {report.Timestamp:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                sb.AppendLine("--- INPUT ---");
                sb.AppendLine($"Point Cloud Points: {report.InputPointCount:N0}");
                sb.AppendLine($"Input File: {report.InputFile}");
                sb.AppendLine();
                sb.AppendLine("--- PROCESSING ---");
                sb.AppendLine($"Filtered Points: {report.FilteredPointCount:N0}");
                sb.AppendLine($"Voxel Size: {report.VoxelSize:F4} m");
                sb.AppendLine($"Processing Time: {report.ProcessingTimeMs:F1} ms");
                sb.AppendLine();
                sb.AppendLine("--- MESH ---");
                sb.AppendLine($"Vertices: {report.MeshVertexCount:N0}");
                sb.AppendLine($"Triangles: {report.MeshTriangleCount:N0}");
                sb.AppendLine($"Mesh Generation Time: {report.MeshGenerationTimeMs:F1} ms");
                sb.AppendLine();
                sb.AppendLine("--- PATH ---");
                sb.AppendLine($"Path Points: {report.PathPointCount:N0}");
                sb.AppendLine($"Path Length: {report.PathLengthMeters:F3} m");
                sb.AppendLine($"Path Planning Time: {report.PathPlanningTimeMs:F1} ms");
                sb.AppendLine();
                sb.AppendLine("--- ROBOT ---");
                sb.AppendLine($"Robot Type: {report.RobotType}");
                sb.AppendLine($"Trajectory Points: {report.TrajectoryPointCount:N0}");
                sb.AppendLine($"IK Success Rate: {report.IKSuccessRate:P1}");
                sb.AppendLine($"Trajectory Calculation Time: {report.TrajectoryTimeMs:F1} ms");
                sb.AppendLine();
                sb.AppendLine("--- TOTALS ---");
                sb.AppendLine($"Total Processing Time: {report.TotalTimeMs:F1} ms");
                sb.AppendLine($"Status: {(report.Success ? "SUCCESS" : "FAILED")}");
                if (!string.IsNullOrEmpty(report.ErrorMessage))
                    sb.AppendLine($"Error: {report.ErrorMessage}");
                sb.AppendLine();
                sb.AppendLine("========================================");

                File.WriteAllText(filePath, sb.ToString());
                Debug.Log($"ResultExporter: Report exported to {filePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"ResultExporter: Failed to export report - {e.Message}");
            }
        }

        #endregion
    }

    /// <summary>
    /// Pipeline execution report data
    /// </summary>
    [Serializable]
    public class PipelineReport
    {
        public DateTime Timestamp = DateTime.Now;
        public string InputFile = "";
        public int InputPointCount;
        public int FilteredPointCount;
        public float VoxelSize;
        public float ProcessingTimeMs;
        public int MeshVertexCount;
        public int MeshTriangleCount;
        public float MeshGenerationTimeMs;
        public int PathPointCount;
        public float PathLengthMeters;
        public float PathPlanningTimeMs;
        public string RobotType = "UR5";
        public int TrajectoryPointCount;
        public float IKSuccessRate = 1.0f;
        public float TrajectoryTimeMs;
        public float TotalTimeMs;
        public bool Success = true;
        public string ErrorMessage = "";
    }
}
