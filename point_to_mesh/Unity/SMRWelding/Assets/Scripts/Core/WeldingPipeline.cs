// =============================================================================
// WeldingPipeline.cs - High-level Welding Pipeline Controller
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SMRWelding
{
    using Native;

    /// <summary>
    /// Pipeline state enumeration
    /// </summary>
    public enum PipelineState
    {
        Idle,
        LoadingPointCloud,
        ProcessingPointCloud,
        GeneratingMesh,
        GeneratingPath,
        ComputingTrajectory,
        Ready,
        Error
    }

    /// <summary>
    /// Pipeline progress event arguments
    /// </summary>
    public class PipelineProgressEventArgs : EventArgs
    {
        public PipelineState State { get; }
        public float Progress { get; }
        public string Message { get; }

        public PipelineProgressEventArgs(PipelineState state, float progress, string message)
        {
            State = state;
            Progress = progress;
            Message = message;
        }
    }

    /// <summary>
    /// High-level welding pipeline that orchestrates the complete workflow
    /// </summary>
    public class WeldingPipeline : IDisposable
    {
        // Native resources
        private PointCloudWrapper _pointCloud;
        private MeshWrapper _mesh;
        private RobotWrapper _robot;
        private PathWrapper _path;

        // Results
        private Mesh _unityMesh;
        private Vector3[] _pathPositions;
        private double[][] _jointTrajectory;
        private bool[] _reachability;

        // State
        private PipelineState _state = PipelineState.Idle;
        private bool _disposed;

        // Events
        public event EventHandler<PipelineProgressEventArgs> ProgressChanged;

        // Properties
        public PipelineState State => _state;
        public Mesh GeneratedMesh => _unityMesh;
        public Vector3[] PathPositions => _pathPositions;
        public double[][] JointTrajectory => _jointTrajectory;
        public bool[] Reachability => _reachability;
        public bool HasMesh => _unityMesh != null;
        public bool HasPath => _pathPositions != null && _pathPositions.Length > 0;
        public bool HasTrajectory => _jointTrajectory != null && _jointTrajectory.Length > 0;

        /// <summary>
        /// Pipeline configuration
        /// </summary>
        public class Config
        {
            // Point cloud processing
            public float VoxelSize { get; set; } = 0.002f;
            public int NormalKNN { get; set; } = 30;
            public int OutlierNeighbors { get; set; } = 20;
            public float OutlierStdRatio { get; set; } = 2.0f;

            // Mesh generation
            public int PoissonDepth { get; set; } = 8;
            public float DensityThreshold { get; set; } = 0.01f;
            public int SimplifyTarget { get; set; } = 0; // 0 = no simplification

            // Path generation
            public float PathStepSize { get; set; } = 0.005f;
            public float StandoffDistance { get; set; } = 0.015f;
            public WeaveType WeavePattern { get; set; } = WeaveType.None;
            public float WeaveAmplitude { get; set; } = 0.002f;
            public float WeaveFrequency { get; set; } = 2.0f;
            public int SmoothWindowSize { get; set; } = 5;

            // Robot
            public RobotType RobotType { get; set; } = RobotType.UR5;
        }

        private Config _config;

        public WeldingPipeline(Config config = null)
        {
            _config = config ?? new Config();
        }

        ~WeldingPipeline()
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
            if (!_disposed)
            {
                if (disposing)
                {
                    _pointCloud?.Dispose();
                    _mesh?.Dispose();
                    _robot?.Dispose();
                    _path?.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Run complete pipeline from point cloud file
        /// </summary>
        public void RunFromFile(string pointCloudPath)
        {
            try
            {
                // Step 1: Load point cloud
                SetState(PipelineState.LoadingPointCloud, 0, "Loading point cloud...");
                LoadPointCloud(pointCloudPath);
                SetState(PipelineState.LoadingPointCloud, 1, "Point cloud loaded");

                // Step 2: Process point cloud
                SetState(PipelineState.ProcessingPointCloud, 0, "Processing point cloud...");
                ProcessPointCloud();
                SetState(PipelineState.ProcessingPointCloud, 1, "Point cloud processed");

                // Step 3: Generate mesh
                SetState(PipelineState.GeneratingMesh, 0, "Generating mesh...");
                GenerateMesh();
                SetState(PipelineState.GeneratingMesh, 1, "Mesh generated");

                // Step 4: Generate path
                SetState(PipelineState.GeneratingPath, 0, "Generating weld path...");
                GeneratePath();
                SetState(PipelineState.GeneratingPath, 1, "Path generated");

                // Step 5: Compute trajectory
                SetState(PipelineState.ComputingTrajectory, 0, "Computing robot trajectory...");
                ComputeTrajectory();
                SetState(PipelineState.ComputingTrajectory, 1, "Trajectory computed");

                SetState(PipelineState.Ready, 1, "Pipeline complete");
            }
            catch (Exception ex)
            {
                SetState(PipelineState.Error, 0, $"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Run pipeline from Unity points
        /// </summary>
        public void RunFromPoints(Vector3[] points)
        {
            try
            {
                SetState(PipelineState.LoadingPointCloud, 0, "Setting points...");
                _pointCloud?.Dispose();
                _pointCloud = new PointCloudWrapper();
                _pointCloud.SetPoints(points);
                SetState(PipelineState.LoadingPointCloud, 1, "Points set");

                SetState(PipelineState.ProcessingPointCloud, 0, "Processing...");
                ProcessPointCloud();
                SetState(PipelineState.ProcessingPointCloud, 1, "Processed");

                SetState(PipelineState.GeneratingMesh, 0, "Generating mesh...");
                GenerateMesh();
                SetState(PipelineState.GeneratingMesh, 1, "Mesh generated");

                SetState(PipelineState.GeneratingPath, 0, "Generating path...");
                GeneratePath();
                SetState(PipelineState.GeneratingPath, 1, "Path generated");

                SetState(PipelineState.ComputingTrajectory, 0, "Computing trajectory...");
                ComputeTrajectory();
                SetState(PipelineState.ComputingTrajectory, 1, "Complete");

                SetState(PipelineState.Ready, 1, "Pipeline complete");
            }
            catch (Exception ex)
            {
                SetState(PipelineState.Error, 0, $"Error: {ex.Message}");
                throw;
            }
        }

        private void LoadPointCloud(string path)
        {
            _pointCloud?.Dispose();
            _pointCloud = new PointCloudWrapper();

            string ext = System.IO.Path.GetExtension(path).ToLower();
            if (ext == ".ply")
                _pointCloud.LoadPLY(path);
            else if (ext == ".pcd")
                _pointCloud.LoadPCD(path);
            else
                throw new ArgumentException($"Unsupported format: {ext}");

            Debug.Log($"Loaded {_pointCloud.Count} points from {path}");
        }

        private void ProcessPointCloud()
        {
            if (_pointCloud == null || _pointCloud.Count == 0)
                throw new InvalidOperationException("No point cloud loaded");

            // Downsample
            if (_config.VoxelSize > 0)
            {
                _pointCloud.DownsampleVoxel(_config.VoxelSize);
                Debug.Log($"After voxel downsample: {_pointCloud.Count} points");
            }

            // Remove outliers
            _pointCloud.RemoveOutliers(_config.OutlierNeighbors, _config.OutlierStdRatio);
            Debug.Log($"After outlier removal: {_pointCloud.Count} points");

            // Estimate normals
            _pointCloud.EstimateNormalsKNN(_config.NormalKNN);

            // Orient normals
            _pointCloud.OrientNormals(Vector3.zero);
        }

        private void GenerateMesh()
        {
            if (_pointCloud == null || !_pointCloud.HasNormals)
                throw new InvalidOperationException("Point cloud must have normals");

            _mesh?.Dispose();

            var settings = new PoissonSettings
            {
                depth = _config.PoissonDepth,
                scale = 1.1f,
                linear_fit = false,
                density_threshold = _config.DensityThreshold
            };

            _mesh = MeshWrapper.CreateFromPoisson(_pointCloud, settings);
            Debug.Log($"Generated mesh: {_mesh.VertexCount} vertices, {_mesh.TriangleCount} triangles");

            // Remove low density
            _mesh.RemoveLowDensity(_config.DensityThreshold);

            // Simplify if requested
            if (_config.SimplifyTarget > 0 && _mesh.TriangleCount > _config.SimplifyTarget)
            {
                _mesh.Simplify(_config.SimplifyTarget);
                Debug.Log($"After simplification: {_mesh.TriangleCount} triangles");
            }

            // Convert to Unity mesh
            _unityMesh = _mesh.ToUnityMesh();
        }

        private void GeneratePath()
        {
            if (_mesh == null)
                throw new InvalidOperationException("No mesh generated");

            _path?.Dispose();

            var pathParams = new PathParams
            {
                step_size = _config.PathStepSize,
                standoff_distance = _config.StandoffDistance,
                weave_type = _config.WeavePattern,
                weave_amplitude = _config.WeaveAmplitude,
                weave_frequency = _config.WeaveFrequency
            };

            _path = PathWrapper.CreateFromMeshEdge(_mesh, pathParams);
            Debug.Log($"Generated path with {_path.Count} points");

            // Apply weave if specified
            if (_config.WeavePattern != WeaveType.None)
            {
                _path.ApplyWeave(_config.WeavePattern, _config.WeaveAmplitude, _config.WeaveFrequency);
            }

            // Resample
            _path.Resample(_config.PathStepSize);

            // Smooth
            if (_config.SmoothWindowSize >= 3)
            {
                _path.Smooth(_config.SmoothWindowSize);
            }

            _pathPositions = _path.GetPositions();
            Debug.Log($"Final path: {_pathPositions.Length} points, length: {_path.GetTotalLength():F3}m");
        }

        private void ComputeTrajectory()
        {
            if (_path == null || _path.Count == 0)
                throw new InvalidOperationException("No path generated");

            _robot?.Dispose();
            _robot = new RobotWrapper(_config.RobotType);

            var (joints, reachable) = _path.ToJointTrajectory(_robot, _config.StandoffDistance);
            _jointTrajectory = joints;
            _reachability = reachable;

            int reachableCount = 0;
            foreach (bool r in reachable)
                if (r) reachableCount++;

            Debug.Log($"Trajectory computed: {reachableCount}/{reachable.Length} reachable points");
        }

        private void SetState(PipelineState state, float progress, string message)
        {
            _state = state;
            ProgressChanged?.Invoke(this, new PipelineProgressEventArgs(state, progress, message));
        }
    }
}
