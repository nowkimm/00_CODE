// =============================================================================
// PointCloudWrapper.cs - High-level Point Cloud API
// =============================================================================
using System;
using UnityEngine;

namespace SMRWelding.Native
{
    /// <summary>
    /// High-level wrapper for native point cloud operations
    /// </summary>
    public class PointCloudWrapper : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed;

        public IntPtr Handle => _handle;
        public bool IsValid => _handle != IntPtr.Zero;

        public PointCloudWrapper()
        {
            _handle = NativeBindings.smr_pointcloud_create();
            if (_handle == IntPtr.Zero)
                throw new SMRNativeException(SMRErrorCode.OutOfMemory, "Failed to create point cloud");
        }

        ~PointCloudWrapper()
        {
            Dispose(false);
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
                NativeBindings.smr_pointcloud_destroy(_handle);
                _handle = IntPtr.Zero;
            }
            _disposed = true;
        }

        /// <summary>
        /// Load point cloud from PLY file
        /// </summary>
        public void LoadPLY(string path)
        {
            ThrowIfDisposed();
            var result = NativeBindings.smr_pointcloud_load_ply(_handle, path);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result, $"Failed to load PLY: {NativeBindings.GetLastError()}");
        }

        /// <summary>
        /// Load point cloud from PCD file
        /// </summary>
        public void LoadPCD(string path)
        {
            ThrowIfDisposed();
            var result = NativeBindings.smr_pointcloud_load_pcd(_handle, path);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result, $"Failed to load PCD: {NativeBindings.GetLastError()}");
        }

        /// <summary>
        /// Set points from Unity Vector3 array
        /// </summary>
        public void SetPoints(Vector3[] points)
        {
            ThrowIfDisposed();
            float[] data = new float[points.Length * 3];
            for (int i = 0; i < points.Length; i++)
            {
                data[i * 3] = points[i].x;
                data[i * 3 + 1] = points[i].y;
                data[i * 3 + 2] = points[i].z;
            }
            var result = NativeBindings.smr_pointcloud_set_points(_handle, data, points.Length);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);
        }

        /// <summary>
        /// Get number of points
        /// </summary>
        public int Count
        {
            get
            {
                ThrowIfDisposed();
                return NativeBindings.smr_pointcloud_get_count(_handle);
            }
        }

        /// <summary>
        /// Get points as Unity Vector3 array
        /// </summary>
        public Vector3[] GetPoints()
        {
            ThrowIfDisposed();
            int count = Count;
            if (count <= 0) return Array.Empty<Vector3>();

            float[] data = new float[count * 3];
            var result = NativeBindings.smr_pointcloud_get_points(_handle, data);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);

            Vector3[] points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                points[i] = new Vector3(data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
            }
            return points;
        }

        /// <summary>
        /// Check if normals are available
        /// </summary>
        public bool HasNormals
        {
            get
            {
                ThrowIfDisposed();
                return NativeBindings.smr_pointcloud_has_normals(_handle);
            }
        }

        /// <summary>
        /// Get normals as Unity Vector3 array
        /// </summary>
        public Vector3[] GetNormals()
        {
            ThrowIfDisposed();
            int count = Count;
            if (count <= 0 || !HasNormals) return Array.Empty<Vector3>();

            float[] data = new float[count * 3];
            var result = NativeBindings.smr_pointcloud_get_normals(_handle, data);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);

            Vector3[] normals = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                normals[i] = new Vector3(data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
            }
            return normals;
        }

        /// <summary>
        /// Estimate normals using K nearest neighbors
        /// </summary>
        public void EstimateNormalsKNN(int k = 30)
        {
            ThrowIfDisposed();
            var result = NativeBindings.smr_pointcloud_estimate_normals_knn(_handle, k);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);
        }

        /// <summary>
        /// Estimate normals using radius search
        /// </summary>
        public void EstimateNormalsRadius(float radius)
        {
            ThrowIfDisposed();
            var result = NativeBindings.smr_pointcloud_estimate_normals_radius(_handle, radius);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);
        }

        /// <summary>
        /// Orient normals towards camera position
        /// </summary>
        public void OrientNormals(Vector3 cameraPosition)
        {
            ThrowIfDisposed();
            float[] pos = { cameraPosition.x, cameraPosition.y, cameraPosition.z };
            var result = NativeBindings.smr_pointcloud_orient_normals(_handle, pos);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);
        }

        /// <summary>
        /// Downsample using voxel grid
        /// </summary>
        public void DownsampleVoxel(float voxelSize)
        {
            ThrowIfDisposed();
            var result = NativeBindings.smr_pointcloud_downsample_voxel(_handle, voxelSize);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);
        }

        /// <summary>
        /// Remove statistical outliers
        /// </summary>
        public void RemoveOutliers(int nbNeighbors = 20, float stdRatio = 2.0f)
        {
            ThrowIfDisposed();
            var result = NativeBindings.smr_pointcloud_remove_outliers(_handle, nbNeighbors, stdRatio);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed || _handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(PointCloudWrapper));
        }
    }
}
