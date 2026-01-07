// =============================================================================
// WeldPath.cs - Welding Path Data Structure
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SMRWelding.Path
{
    /// <summary>
    /// Weaving pattern types
    /// </summary>
    public enum WeavingPattern
    {
        None = 0,
        Zigzag = 1,
        Circular = 2,
        Triangle = 3,
        Figure8 = 4
    }

    /// <summary>
    /// Single waypoint in welding path
    /// </summary>
    [Serializable]
    public struct WeldWaypoint
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector3 Tangent;
        public float Parameter;     // 0-1 along path
        public float WeldSpeed;     // mm/s
        public float WireSpeed;     // mm/s
        public bool IsKeyPoint;

        public Quaternion Rotation => Quaternion.LookRotation(Tangent, Normal);

        public WeldWaypoint(Vector3 position, Vector3 normal, Vector3 tangent)
        {
            Position = position;
            Normal = normal;
            Tangent = tangent;
            Parameter = 0f;
            WeldSpeed = 10f;
            WireSpeed = 100f;
            IsKeyPoint = false;
        }

        public Matrix4x4 GetMatrix()
        {
            return Matrix4x4.TRS(Position, Rotation, Vector3.one);
        }
    }

    /// <summary>
    /// Complete welding path data
    /// </summary>
    [Serializable]
    public class WeldPath
    {
        public List<WeldWaypoint> Waypoints = new List<WeldWaypoint>();
        public WeavingPattern Pattern = WeavingPattern.None;
        public float WeavingAmplitude = 0.002f;  // 2mm
        public float WeavingFrequency = 10f;     // Hz
        public float TotalLength;
        public float EstimatedTime;
        public DateTime CreationTime;

        public int Count => Waypoints.Count;
        public bool IsValid => Waypoints.Count >= 2;

        public WeldPath()
        {
            CreationTime = DateTime.Now;
        }

        /// <summary>
        /// Add waypoint to path
        /// </summary>
        public void AddWaypoint(WeldWaypoint waypoint)
        {
            Waypoints.Add(waypoint);
            RecalculateParameters();
        }

        /// <summary>
        /// Add waypoint with position and normal
        /// </summary>
        public void AddWaypoint(Vector3 position, Vector3 normal)
        {
            Vector3 tangent = Vector3.forward;
            if (Waypoints.Count > 0)
            {
                tangent = (position - Waypoints[Waypoints.Count - 1].Position).normalized;
                if (tangent.sqrMagnitude < 0.001f)
                    tangent = Waypoints[Waypoints.Count - 1].Tangent;
            }

            AddWaypoint(new WeldWaypoint(position, normal, tangent));
        }

        /// <summary>
        /// Recalculate path parameters
        /// </summary>
        public void RecalculateParameters()
        {
            if (Waypoints.Count < 2)
            {
                TotalLength = 0;
                return;
            }

            TotalLength = 0;
            float[] distances = new float[Waypoints.Count];
            distances[0] = 0;

            for (int i = 1; i < Waypoints.Count; i++)
            {
                float segmentLength = Vector3.Distance(
                    Waypoints[i].Position,
                    Waypoints[i - 1].Position
                );
                TotalLength += segmentLength;
                distances[i] = TotalLength;
            }

            // Update parameters and tangents
            for (int i = 0; i < Waypoints.Count; i++)
            {
                var wp = Waypoints[i];
                wp.Parameter = TotalLength > 0 ? distances[i] / TotalLength : 0;

                // Calculate tangent
                if (i < Waypoints.Count - 1)
                {
                    wp.Tangent = (Waypoints[i + 1].Position - wp.Position).normalized;
                }
                else if (i > 0)
                {
                    wp.Tangent = (wp.Position - Waypoints[i - 1].Position).normalized;
                }

                Waypoints[i] = wp;
            }

            // Estimate time
            float avgSpeed = 10f; // mm/s default
            if (Waypoints.Count > 0)
                avgSpeed = Waypoints[0].WeldSpeed;
            EstimatedTime = TotalLength * 1000f / avgSpeed; // seconds
        }

        /// <summary>
        /// Get interpolated waypoint at parameter t (0-1)
        /// </summary>
        public WeldWaypoint GetWaypointAt(float t)
        {
            if (Waypoints.Count == 0) return new WeldWaypoint();
            if (Waypoints.Count == 1) return Waypoints[0];

            t = Mathf.Clamp01(t);

            // Find segment
            int idx = 0;
            for (int i = 0; i < Waypoints.Count - 1; i++)
            {
                if (t >= Waypoints[i].Parameter && t <= Waypoints[i + 1].Parameter)
                {
                    idx = i;
                    break;
                }
            }

            var wp0 = Waypoints[idx];
            var wp1 = Waypoints[Mathf.Min(idx + 1, Waypoints.Count - 1)];

            float segmentT = 0;
            float segmentLength = wp1.Parameter - wp0.Parameter;
            if (segmentLength > 0.0001f)
                segmentT = (t - wp0.Parameter) / segmentLength;

            return new WeldWaypoint
            {
                Position = Vector3.Lerp(wp0.Position, wp1.Position, segmentT),
                Normal = Vector3.Slerp(wp0.Normal, wp1.Normal, segmentT).normalized,
                Tangent = Vector3.Slerp(wp0.Tangent, wp1.Tangent, segmentT).normalized,
                Parameter = t,
                WeldSpeed = Mathf.Lerp(wp0.WeldSpeed, wp1.WeldSpeed, segmentT),
                WireSpeed = Mathf.Lerp(wp0.WireSpeed, wp1.WireSpeed, segmentT)
            };
        }

        /// <summary>
        /// Apply weaving pattern to path
        /// </summary>
        public WeldPath ApplyWeaving(WeavingPattern pattern, float amplitude, float frequency)
        {
            if (pattern == WeavingPattern.None || Waypoints.Count < 2)
                return this;

            var weavedPath = new WeldPath
            {
                Pattern = pattern,
                WeavingAmplitude = amplitude,
                WeavingFrequency = frequency
            };

            float stepSize = 0.001f; // 1mm steps
            int steps = Mathf.Max(2, Mathf.CeilToInt(TotalLength / stepSize));
            float phase = 0;

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                var baseWp = GetWaypointAt(t);

                // Calculate weave offset
                Vector3 offset = Vector3.zero;
                Vector3 lateral = Vector3.Cross(baseWp.Tangent, baseWp.Normal).normalized;

                switch (pattern)
                {
                    case WeavingPattern.Zigzag:
                        offset = lateral * amplitude * Mathf.Sin(phase);
                        break;
                    case WeavingPattern.Circular:
                        offset = lateral * amplitude * Mathf.Cos(phase) +
                                 baseWp.Normal * amplitude * Mathf.Sin(phase) * 0.5f;
                        break;
                    case WeavingPattern.Triangle:
                        float tri = Mathf.PingPong(phase / Mathf.PI, 1f) * 2f - 1f;
                        offset = lateral * amplitude * tri;
                        break;
                    case WeavingPattern.Figure8:
                        offset = lateral * amplitude * Mathf.Sin(phase) +
                                 baseWp.Normal * amplitude * Mathf.Sin(2 * phase) * 0.3f;
                        break;
                }

                var weavedWp = baseWp;
                weavedWp.Position += offset;
                weavedPath.Waypoints.Add(weavedWp);

                phase += frequency * stepSize * 2 * Mathf.PI;
            }

            weavedPath.RecalculateParameters();
            return weavedPath;
        }

        /// <summary>
        /// Resample path with uniform spacing
        /// </summary>
        public WeldPath Resample(float spacing)
        {
            if (Waypoints.Count < 2) return this;

            var resampled = new WeldPath();
            int numPoints = Mathf.Max(2, Mathf.CeilToInt(TotalLength / spacing) + 1);

            for (int i = 0; i < numPoints; i++)
            {
                float t = (float)i / (numPoints - 1);
                resampled.Waypoints.Add(GetWaypointAt(t));
            }

            resampled.RecalculateParameters();
            return resampled;
        }

        /// <summary>
        /// Get positions as array (for visualization)
        /// </summary>
        public Vector3[] GetPositions()
        {
            var positions = new Vector3[Waypoints.Count];
            for (int i = 0; i < Waypoints.Count; i++)
                positions[i] = Waypoints[i].Position;
            return positions;
        }
    }
}
