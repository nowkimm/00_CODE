// =============================================================================
// PathWrapper.cs - High-level Weld Path API
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SMRWelding.Native
{
    /// <summary>
    /// High-level wrapper for weld path operations
    /// </summary>
    public class PathWrapper : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed;

        public IntPtr Handle => _handle;
        public bool IsValid => _handle != IntPtr.Zero;

        private PathWrapper(IntPtr handle)
        {
            _handle = handle;
        }

        ~PathWrapper()
        {
            Dispose(false);
        }

        /// <summary>
        /// Create path from mesh edge (auto-detection)
        /// </summary>
        public static PathWrapper CreateFromMeshEdge(MeshWrapper mesh, PathParams? pathParams = null)
        {
            if (mesh == null || !mesh.IsValid)
                throw new ArgumentException("Invalid mesh");

            var actualParams = pathParams ?? PathParams.Default;
            IntPtr handle = NativeBindings.smr_path_create_from_edge(mesh.Handle, ref actualParams);
            
            if (handle == IntPtr.Zero)
                throw new SMRNativeException(SMRErrorCode.Unknown, 
                    $"Failed to create path: {NativeBindings.GetLastError()}");

            return new PathWrapper(handle);
        }

        /// <summary>
        /// Create path from explicit points
        /// </summary>
        public static PathWrapper CreateFromPoints(Vector3[] points, Vector3[] normals, PathParams? pathParams = null)
        {
            if (points == null || points.Length == 0)
                throw new ArgumentException("Points array is empty");
            if (normals == null || normals.Length != points.Length)
                throw new ArgumentException("Normals array must match points length");

            float[] pointsData = new float[points.Length * 3];
            float[] normalsData = new float[normals.Length * 3];

            for (int i = 0; i < points.Length; i++)
            {
                pointsData[i * 3] = points[i].x;
                pointsData[i * 3 + 1] = points[i].y;
                pointsData[i * 3 + 2] = points[i].z;
                normalsData[i * 3] = normals[i].x;
                normalsData[i * 3 + 1] = normals[i].y;
                normalsData[i * 3 + 2] = normals[i].z;
            }

            var actualParams = pathParams ?? PathParams.Default;
            IntPtr handle = NativeBindings.smr_path_create_from_points(
                pointsData, normalsData, points.Length, ref actualParams);
            
            if (handle == IntPtr.Zero)
                throw new SMRNativeException(SMRErrorCode.Unknown, 
                    $"Failed to create path: {NativeBindings.GetLastError()}");

            return new PathWrapper(handle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && _handle != IntPtr.Zero)
            {
                NativeBindings.smr_path_destroy(_handle);
                _handle = IntPtr.Zero;
            }
            _disposed = true;
        }

        /// <summary>
        /// Get number of weld points
        /// </summary>
        public int Count
        {
            get
            {
                ThrowIfDisposed();
                return NativeBindings.smr_path_get_count(_handle);
            }
        }

        /// <summary>
        /// Get all weld points
        /// </summary>
        public WeldPoint[] GetPoints()
        {
            ThrowIfDisposed();
            int count = Count;
            if (count <= 0) return Array.Empty<WeldPoint>();

            WeldPoint[] points = new WeldPoint[count];
            
            // Initialize arrays in structs
            for (int i = 0; i < count; i++)
            {
                points[i].position = new float[3];
                points[i].normal = new float[3];
                points[i].tangent = new float[3];
            }

            var result = NativeBindings.smr_path_get_points(_handle, points);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);

            return points;
        }

        /// <summary>
        /// Get positions as Unity Vector3 array
        /// </summary>
        public Vector3[] GetPositions()
        {
            var weldPoints = GetPoints();
            Vector3[] positions = new Vector3[weldPoints.Length];
            for (int i = 0; i < weldPoints.Length; i++)
            {
                positions[i] = weldPoints[i].Position;
            }
            return positions;
        }

        /// <summary>
        /// Apply weave pattern
        /// </summary>
        public void ApplyWeave(WeaveType type, float amplitude = 0.002f, float frequency = 2.0f)
        {
            ThrowIfDisposed();
            var result = NativeBindings.smr_path_apply_weave(_handle, type, amplitude, frequency);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);
        }

        /// <summary>
        /// Resample path with uniform step size
        /// </summary>
        public void Resample(float stepSize)
        {
            ThrowIfDisposed();
            if (stepSize <= 0)
                throw new ArgumentException("Step size must be positive");

            var result = NativeBindings.smr_path_resample(_handle, stepSize);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);
        }

        /// <summary>
        /// Smooth path using moving average
        /// </summary>
        public void Smooth(int windowSize = 5)
        {
            ThrowIfDisposed();
            if (windowSize < 3)
                throw new ArgumentException("Window size must be at least 3");

            var result = NativeBindings.smr_path_smooth(_handle, windowSize);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);
        }

        /// <summary>
        /// Convert path to robot joint trajectories
        /// </summary>
        public (double[][] joints, bool[] reachable) ToJointTrajectory(RobotWrapper robot, float standoff = 0.015f)
        {
            ThrowIfDisposed();
            if (robot == null || !robot.IsValid)
                throw new ArgumentException("Invalid robot");

            int count = Count;
            if (count <= 0) return (Array.Empty<double[]>(), Array.Empty<bool>());

            double[] jointsFlat = new double[count * 6];
            bool[] reachable = new bool[count];

            var result = NativeBindings.smr_path_to_joints(_handle, robot.Handle, standoff, jointsFlat, reachable);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);

            double[][] joints = new double[count][];
            for (int i = 0; i < count; i++)
            {
                joints[i] = new double[6];
                Array.Copy(jointsFlat, i * 6, joints[i], 0, 6);
            }

            return (joints, reachable);
        }

        /// <summary>
        /// Get total arc length
        /// </summary>
        public float GetTotalLength()
        {
            var points = GetPoints();
            if (points.Length == 0) return 0;
            return points[points.Length - 1].arc_length;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed || _handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(PathWrapper));
        }
    }
}
