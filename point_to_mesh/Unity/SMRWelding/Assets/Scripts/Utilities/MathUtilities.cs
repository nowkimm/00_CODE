// =============================================================================
// MathUtilities.cs - Mathematical Utility Functions
// =============================================================================
using System;
using UnityEngine;

namespace SMRWelding.Utilities
{
    /// <summary>
    /// Mathematical utility functions for welding path calculations
    /// </summary>
    public static class MathUtilities
    {
        public const float DEG_TO_RAD = Mathf.PI / 180f;
        public const float RAD_TO_DEG = 180f / Mathf.PI;

        /// <summary>
        /// Convert double array (radians) to float array (degrees)
        /// </summary>
        public static float[] RadiansToDegrees(double[] radians)
        {
            if (radians == null) return null;
            float[] degrees = new float[radians.Length];
            for (int i = 0; i < radians.Length; i++)
                degrees[i] = (float)(radians[i] * RAD_TO_DEG);
            return degrees;
        }

        /// <summary>
        /// Convert float array (degrees) to double array (radians)
        /// </summary>
        public static double[] DegreesToRadians(float[] degrees)
        {
            if (degrees == null) return null;
            double[] radians = new double[degrees.Length];
            for (int i = 0; i < degrees.Length; i++)
                radians[i] = degrees[i] * DEG_TO_RAD;
            return radians;
        }

        /// <summary>
        /// Calculate arc length of a path
        /// </summary>
        public static float CalculatePathLength(Vector3[] path)
        {
            if (path == null || path.Length < 2) return 0;

            float length = 0;
            for (int i = 1; i < path.Length; i++)
                length += Vector3.Distance(path[i - 1], path[i]);
            return length;
        }

        /// <summary>
        /// Resample path to uniform spacing
        /// </summary>
        public static Vector3[] ResamplePath(Vector3[] path, float spacing)
        {
            if (path == null || path.Length < 2 || spacing <= 0)
                return path;

            float totalLength = CalculatePathLength(path);
            int newCount = Mathf.Max(2, Mathf.CeilToInt(totalLength / spacing) + 1);
            
            Vector3[] result = new Vector3[newCount];
            result[0] = path[0];
            result[newCount - 1] = path[path.Length - 1];

            float targetDist = spacing;
            int srcIdx = 0;
            float accumDist = 0;

            for (int i = 1; i < newCount - 1; i++)
            {
                while (srcIdx < path.Length - 1)
                {
                    float segLen = Vector3.Distance(path[srcIdx], path[srcIdx + 1]);
                    if (accumDist + segLen >= targetDist)
                    {
                        float t = (targetDist - accumDist) / segLen;
                        result[i] = Vector3.Lerp(path[srcIdx], path[srcIdx + 1], t);
                        targetDist += spacing;
                        break;
                    }
                    accumDist += segLen;
                    srcIdx++;
                }
            }

            return result;
        }

        /// <summary>
        /// Smooth path using moving average
        /// </summary>
        public static Vector3[] SmoothPath(Vector3[] path, int windowSize)
        {
            if (path == null || path.Length < 3 || windowSize < 2)
                return path;

            Vector3[] result = new Vector3[path.Length];
            int halfWindow = windowSize / 2;

            for (int i = 0; i < path.Length; i++)
            {
                Vector3 sum = Vector3.zero;
                int count = 0;

                for (int j = -halfWindow; j <= halfWindow; j++)
                {
                    int idx = Mathf.Clamp(i + j, 0, path.Length - 1);
                    sum += path[idx];
                    count++;
                }

                result[i] = sum / count;
            }

            // Preserve endpoints
            result[0] = path[0];
            result[path.Length - 1] = path[path.Length - 1];

            return result;
        }

        /// <summary>
        /// Calculate tangent vectors along path
        /// </summary>
        public static Vector3[] CalculateTangents(Vector3[] path)
        {
            if (path == null || path.Length < 2) return null;

            Vector3[] tangents = new Vector3[path.Length];

            // First point
            tangents[0] = (path[1] - path[0]).normalized;

            // Middle points (central difference)
            for (int i = 1; i < path.Length - 1; i++)
                tangents[i] = (path[i + 1] - path[i - 1]).normalized;

            // Last point
            tangents[path.Length - 1] = (path[path.Length - 1] - path[path.Length - 2]).normalized;

            return tangents;
        }

        /// <summary>
        /// Calculate normal vectors along path (perpendicular to tangent, pointing up)
        /// </summary>
        public static Vector3[] CalculateNormals(Vector3[] path, Vector3[] tangents = null)
        {
            if (path == null || path.Length < 2) return null;

            tangents ??= CalculateTangents(path);
            Vector3[] normals = new Vector3[path.Length];

            for (int i = 0; i < path.Length; i++)
            {
                // Use Frenet-Serret frame approximation
                Vector3 up = Vector3.up;
                Vector3 right = Vector3.Cross(up, tangents[i]).normalized;
                
                if (right.sqrMagnitude < 0.001f)
                {
                    right = Vector3.Cross(Vector3.forward, tangents[i]).normalized;
                }
                
                normals[i] = Vector3.Cross(tangents[i], right).normalized;
            }

            return normals;
        }

        /// <summary>
        /// Transform from local to world coordinates
        /// </summary>
        public static Matrix4x4 LocalToWorld(Vector3 position, Vector3 tangent, Vector3 normal)
        {
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
            
            Matrix4x4 matrix = Matrix4x4.identity;
            matrix.SetColumn(0, new Vector4(binormal.x, binormal.y, binormal.z, 0));
            matrix.SetColumn(1, new Vector4(normal.x, normal.y, normal.z, 0));
            matrix.SetColumn(2, new Vector4(tangent.x, tangent.y, tangent.z, 0));
            matrix.SetColumn(3, new Vector4(position.x, position.y, position.z, 1));
            
            return matrix;
        }

        /// <summary>
        /// Generate weave pattern offset
        /// </summary>
        public static Vector3 GenerateWeaveOffset(float t, float amplitude, float frequency, int pattern)
        {
            float phase = t * frequency * 2 * Mathf.PI;
            
            return pattern switch
            {
                1 => new Vector3(Mathf.Sin(phase) * amplitude, 0, 0), // Zigzag
                2 => new Vector3(Mathf.Cos(phase) * amplitude, Mathf.Sin(phase) * amplitude, 0), // Circular
                3 => new Vector3(Mathf.PingPong(phase, 1) * 2 - 1, 0, 0) * amplitude, // Triangle
                4 => new Vector3(Mathf.Sin(phase) * amplitude, Mathf.Sin(phase * 2) * amplitude * 0.5f, 0), // Figure8
                _ => Vector3.zero
            };
        }

        /// <summary>
        /// Normalize angle to [-PI, PI]
        /// </summary>
        public static double NormalizeAngle(double angle)
        {
            while (angle > Math.PI) angle -= 2 * Math.PI;
            while (angle < -Math.PI) angle += 2 * Math.PI;
            return angle;
        }

        /// <summary>
        /// Calculate distance between two joint configurations
        /// </summary>
        public static double JointDistance(double[] j1, double[] j2)
        {
            if (j1 == null || j2 == null || j1.Length != j2.Length)
                return double.MaxValue;

            double sum = 0;
            for (int i = 0; i < j1.Length; i++)
            {
                double diff = NormalizeAngle(j1[i] - j2[i]);
                sum += diff * diff;
            }
            return Math.Sqrt(sum);
        }

        /// <summary>
        /// Linear interpolation for joint angles
        /// </summary>
        public static double[] LerpJoints(double[] from, double[] to, double t)
        {
            if (from == null || to == null || from.Length != to.Length)
                return null;

            double[] result = new double[from.Length];
            for (int i = 0; i < from.Length; i++)
            {
                double diff = NormalizeAngle(to[i] - from[i]);
                result[i] = from[i] + diff * t;
            }
            return result;
        }
    }
}
