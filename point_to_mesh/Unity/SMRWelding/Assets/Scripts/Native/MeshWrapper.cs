// =============================================================================
// MeshWrapper.cs - High-level Mesh API
// =============================================================================
using System;
using UnityEngine;

namespace SMRWelding.Native
{
    /// <summary>
    /// High-level wrapper for native mesh operations
    /// </summary>
    public class MeshWrapper : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed;

        public IntPtr Handle => _handle;
        public bool IsValid => _handle != IntPtr.Zero;

        private MeshWrapper(IntPtr handle)
        {
            _handle = handle;
        }

        ~MeshWrapper()
        {
            Dispose(false);
        }

        /// <summary>
        /// Create mesh from point cloud using Poisson reconstruction
        /// </summary>
        public static MeshWrapper CreateFromPoisson(PointCloudWrapper pointCloud, PoissonSettings? settings = null)
        {
            if (pointCloud == null || !pointCloud.IsValid)
                throw new ArgumentException("Invalid point cloud");

            var actualSettings = settings ?? PoissonSettings.Default;
            IntPtr handle = NativeBindings.smr_mesh_create_poisson(pointCloud.Handle, ref actualSettings);
            
            if (handle == IntPtr.Zero)
                throw new SMRNativeException(SMRErrorCode.Unknown, 
                    $"Failed to create mesh: {NativeBindings.GetLastError()}");

            return new MeshWrapper(handle);
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
                NativeBindings.smr_mesh_destroy(_handle);
                _handle = IntPtr.Zero;
            }
            _disposed = true;
        }

        public int VertexCount
        {
            get
            {
                ThrowIfDisposed();
                return NativeBindings.smr_mesh_get_vertex_count(_handle);
            }
        }

        public int TriangleCount
        {
            get
            {
                ThrowIfDisposed();
                return NativeBindings.smr_mesh_get_triangle_count(_handle);
            }
        }

        /// <summary>
        /// Get vertices as Unity Vector3 array
        /// </summary>
        public Vector3[] GetVertices()
        {
            ThrowIfDisposed();
            int count = VertexCount;
            if (count <= 0) return Array.Empty<Vector3>();

            float[] data = new float[count * 3];
            var result = NativeBindings.smr_mesh_get_vertices(_handle, data);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);

            Vector3[] vertices = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                vertices[i] = new Vector3(data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
            }
            return vertices;
        }

        /// <summary>
        /// Get normals as Unity Vector3 array
        /// </summary>
        public Vector3[] GetNormals()
        {
            ThrowIfDisposed();
            int count = VertexCount;
            if (count <= 0) return Array.Empty<Vector3>();

            float[] data = new float[count * 3];
            var result = NativeBindings.smr_mesh_get_normals(_handle, data);
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
        /// Get triangle indices
        /// </summary>
        public int[] GetTriangles()
        {
            ThrowIfDisposed();
            int count = TriangleCount;
            if (count <= 0) return Array.Empty<int>();

            int[] triangles = new int[count * 3];
            var result = NativeBindings.smr_mesh_get_triangles(_handle, triangles);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);

            return triangles;
        }

        /// <summary>
        /// Convert to Unity Mesh
        /// </summary>
        public Mesh ToUnityMesh()
        {
            ThrowIfDisposed();

            var mesh = new Mesh();
            mesh.name = "SMR_GeneratedMesh";

            var vertices = GetVertices();
            var normals = GetNormals();
            var triangles = GetTriangles();

            // Unity has a vertex limit of 65535 for 16-bit indices
            if (vertices.Length > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Remove low-density vertices
        /// </summary>
        public void RemoveLowDensity(float quantile = 0.01f)
        {
            ThrowIfDisposed();
            var result = NativeBindings.smr_mesh_remove_low_density(_handle, quantile);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);
        }

        /// <summary>
        /// Simplify mesh to target triangle count
        /// </summary>
        public void Simplify(int targetTriangles)
        {
            ThrowIfDisposed();
            var result = NativeBindings.smr_mesh_simplify(_handle, targetTriangles);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);
        }

        /// <summary>
        /// Save mesh to PLY file
        /// </summary>
        public void SavePLY(string path)
        {
            ThrowIfDisposed();
            var result = NativeBindings.smr_mesh_save_ply(_handle, path);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result, $"Failed to save PLY: {NativeBindings.GetLastError()}");
        }

        /// <summary>
        /// Save mesh to OBJ file
        /// </summary>
        public void SaveOBJ(string path)
        {
            ThrowIfDisposed();
            var result = NativeBindings.smr_mesh_save_obj(_handle, path);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result, $"Failed to save OBJ: {NativeBindings.GetLastError()}");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed || _handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(MeshWrapper));
        }
    }
}
