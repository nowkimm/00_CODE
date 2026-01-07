// =============================================================================
// SMRWeldingController.cs - Main Controller for SMR Welding System
// =============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SMRWelding.Native;

namespace SMRWelding
{
    /// <summary>
    /// Main controller for the SMR welding mesh generation and path planning system
    /// </summary>
    public class SMRWeldingController : MonoBehaviour
    {
        [Header("Point Cloud Settings")]
        [SerializeField] private string pointCloudPath = "";
        [SerializeField] private int normalEstimationK = 30;
        [SerializeField] private float voxelDownsampleSize = 0.005f;
        [SerializeField] private bool removeOutliers = true;
        [SerializeField] private int outlierNeighbors = 20;
        [SerializeField] private float outlierStdRatio = 2.0f;

        [Header("Mesh Generation")]
        [SerializeField] private int poissonDepth = 8;
        [SerializeField] private float densityThreshold = 0.01f;
        [SerializeField] private int targetTriangles = 50000;

        [Header("Robot Settings")]
        [SerializeField] private RobotType robotType = RobotType.UR5;

        [Header("Path Settings")]
        [SerializeField] private float pathStepSize = 0.005f;
        [SerializeField] private float standoffDistance = 0.015f;
        [SerializeField] private WeaveType weaveType = WeaveType.Zigzag;
        [SerializeField] private float weaveAmplitude = 0.002f;
        [SerializeField] private float weaveFrequency = 2.0f;

        [Header("Visualization")]
        [SerializeField] private MeshFilter meshDisplay;
        [SerializeField] private LineRenderer pathRenderer;
        [SerializeField] private Material meshMaterial;
        [SerializeField] private Color reachableColor = Color.green;
        [SerializeField] private Color unreachableColor = Color.red;

        // Native resources
        private PointCloudWrapper _pointCloud;
        private MeshWrapper _nativeMesh;
        private RobotWrapper _robot;
        private PathWrapper _weldPath;

        // State
        private bool _isProcessing;
        private Mesh _generatedMesh;
        private Vector3[] _pathPositions;
        private bool[] _pathReachability;
        private double[][] _jointTrajectory;

        // Events
        public event Action<string> OnStatusChanged;
        public event Action<float> OnProgressChanged;
        public event Action<Mesh> OnMeshGenerated;
        public event Action<Vector3[], bool[]> OnPathGenerated;

        public bool IsProcessing => _isProcessing;
        public Mesh GeneratedMesh => _generatedMesh;
        public double[][] JointTrajectory => _jointTrajectory;

        private void Start()
        {
            InitializeRobot();
        }

        private void OnDestroy()
        {
            CleanupResources();
        }

        /// <summary>
        /// Initialize robot with current settings
        /// </summary>
        public void InitializeRobot()
        {
            _robot?.Dispose();
            try
            {
                _robot = new RobotWrapper(robotType);
                UpdateStatus($"Robot initialized: {robotType}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize robot: {ex.Message}");
            }
        }

        /// <summary>
        /// Load point cloud from file
        /// </summary>
        public void LoadPointCloud(string path)
        {
            if (_isProcessing) return;
            StartCoroutine(LoadPointCloudAsync(path));
        }

        private IEnumerator LoadPointCloudAsync(string path)
        {
            _isProcessing = true;
            UpdateStatus("Loading point cloud...");
            UpdateProgress(0);

            yield return null;

            try
            {
                _pointCloud?.Dispose();
                _pointCloud = new PointCloudWrapper();

                if (path.EndsWith(".ply", StringComparison.OrdinalIgnoreCase))
                    _pointCloud.LoadPLY(path);
                else if (path.EndsWith(".pcd", StringComparison.OrdinalIgnoreCase))
                    _pointCloud.LoadPCD(path);
                else
                    throw new ArgumentException("Unsupported file format");

                UpdateStatus($"Loaded {_pointCloud.Count} points");
                UpdateProgress(100);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load point cloud: {ex.Message}");
                UpdateStatus($"Error: {ex.Message}");
            }

            _isProcessing = false;
        }

        /// <summary>
        /// Set point cloud from Unity Vector3 array
        /// </summary>
        public void SetPointCloud(Vector3[] points)
        {
            try
            {
                _pointCloud?.Dispose();
                _pointCloud = new PointCloudWrapper();
                _pointCloud.SetPoints(points);
                UpdateStatus($"Set {points.Length} points");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to set points: {ex.Message}");
            }
        }

        /// <summary>
        /// Process point cloud and generate mesh
        /// </summary>
        public void GenerateMesh()
        {
            if (_isProcessing || _pointCloud == null) return;
            StartCoroutine(GenerateMeshAsync());
        }

        private IEnumerator GenerateMeshAsync()
        {
            _isProcessing = true;

            // Step 1: Downsample
            UpdateStatus("Downsampling...");
            UpdateProgress(10);
            yield return null;

            try
            {
                if (voxelDownsampleSize > 0)
                    _pointCloud.DownsampleVoxel(voxelDownsampleSize);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Downsample warning: {ex.Message}");
            }

            // Step 2: Remove outliers
            if (removeOutliers)
            {
                UpdateStatus("Removing outliers...");
                UpdateProgress(20);
                yield return null;

                try
                {
                    _pointCloud.RemoveOutliers(outlierNeighbors, outlierStdRatio);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Outlier removal warning: {ex.Message}");
                }
            }

            // Step 3: Estimate normals
            UpdateStatus("Estimating normals...");
            UpdateProgress(30);
            yield return null;

            try
            {
                _pointCloud.EstimateNormalsKNN(normalEstimationK);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Normal estimation failed: {ex.Message}");
                _isProcessing = false;
                yield break;
            }

            // Step 4: Orient normals
            UpdateStatus("Orienting normals...");
            UpdateProgress(40);
            yield return null;

            try
            {
                _pointCloud.OrientNormals(Camera.main?.transform.position ?? Vector3.zero);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Normal orientation warning: {ex.Message}");
            }

            // Step 5: Poisson reconstruction
            UpdateStatus("Generating mesh (Poisson)...");
            UpdateProgress(50);
            yield return null;

            try
            {
                var settings = new PoissonSettings
                {
                    depth = poissonDepth,
                    scale = 1.1f,
                    linear_fit = false,
                    density_threshold = densityThreshold
                };

                _nativeMesh?.Dispose();
                _nativeMesh = MeshWrapper.CreateFromPoisson(_pointCloud, settings);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Mesh generation failed: {ex.Message}");
                _isProcessing = false;
                yield break;
            }

            // Step 6: Post-processing
            UpdateStatus("Post-processing mesh...");
            UpdateProgress(70);
            yield return null;

            try
            {
                _nativeMesh.RemoveLowDensity(densityThreshold);
                
                if (targetTriangles > 0 && _nativeMesh.TriangleCount > targetTriangles)
                    _nativeMesh.Simplify(targetTriangles);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Post-processing warning: {ex.Message}");
            }

            // Step 7: Convert to Unity mesh
            UpdateStatus("Converting to Unity mesh...");
            UpdateProgress(90);
            yield return null;

            try
            {
                _generatedMesh = _nativeMesh.ToUnityMesh();
                
                if (meshDisplay != null)
                {
                    meshDisplay.mesh = _generatedMesh;
                    if (meshMaterial != null)
                        meshDisplay.GetComponent<MeshRenderer>().material = meshMaterial;
                }

                OnMeshGenerated?.Invoke(_generatedMesh);
                UpdateStatus($"Mesh generated: {_generatedMesh.vertexCount} vertices, {_generatedMesh.triangles.Length / 3} triangles");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Unity mesh conversion failed: {ex.Message}");
            }

            UpdateProgress(100);
            _isProcessing = false;
        }

        /// <summary>
        /// Generate weld path from mesh edge
        /// </summary>
        public void GenerateWeldPath()
        {
            if (_isProcessing || _nativeMesh == null) return;
            StartCoroutine(GenerateWeldPathAsync());
        }

        private IEnumerator GenerateWeldPathAsync()
        {
            _isProcessing = true;
            UpdateStatus("Generating weld path...");
            UpdateProgress(0);
            yield return null;

            try
            {
                var pathParams = new PathParams
                {
                    step_size = pathStepSize,
                    standoff_distance = standoffDistance,
                    weave_type = WeaveType.None,
                    weave_amplitude = weaveAmplitude,
                    weave_frequency = weaveFrequency
                };

                _weldPath?.Dispose();
                _weldPath = PathWrapper.CreateFromMeshEdge(_nativeMesh, pathParams);
                UpdateStatus($"Path created: {_weldPath.Count} points");
                UpdateProgress(30);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Path generation failed: {ex.Message}");
                _isProcessing = false;
                yield break;
            }

            yield return null;

            // Apply weave pattern
            if (weaveType != WeaveType.None)
            {
                UpdateStatus("Applying weave pattern...");
                try { _weldPath.ApplyWeave(weaveType, weaveAmplitude, weaveFrequency); }
                catch (Exception ex) { Debug.LogWarning($"Weave warning: {ex.Message}"); }
            }

            // Resample and smooth
            try { _weldPath.Resample(pathStepSize); } catch { }
            try { _weldPath.Smooth(5); } catch { }

            // Convert to joint trajectory
            if (_robot != null)
            {
                UpdateStatus("Computing joint trajectory...");
                UpdateProgress(70);
                yield return null;

                try
                {
                    var (joints, reachable) = _weldPath.ToJointTrajectory(_robot, standoffDistance);
                    _jointTrajectory = joints;
                    _pathReachability = reachable;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Joint trajectory warning: {ex.Message}");
                    _pathReachability = new bool[_weldPath.Count];
                    for (int i = 0; i < _pathReachability.Length; i++) _pathReachability[i] = true;
                }
            }

            _pathPositions = _weldPath.GetPositions();
            VisualizePath();
            OnPathGenerated?.Invoke(_pathPositions, _pathReachability);
            UpdateStatus($"Path complete: {_weldPath.Count} points");
            UpdateProgress(100);
            _isProcessing = false;
        }

        private void VisualizePath()
        {
            if (pathRenderer == null || _pathPositions == null) return;
            pathRenderer.positionCount = _pathPositions.Length;
            pathRenderer.SetPositions(_pathPositions);
        }

        public void SaveMesh(string path)
        {
            if (_nativeMesh == null) return;
            try
            {
                if (path.EndsWith(".ply")) _nativeMesh.SavePLY(path);
                else if (path.EndsWith(".obj")) _nativeMesh.SaveOBJ(path);
                UpdateStatus($"Mesh saved: {path}");
            }
            catch (Exception ex) { Debug.LogError($"Save failed: {ex.Message}"); }
        }

        private void UpdateStatus(string status)
        {
            Debug.Log($"[SMRWelding] {status}");
            OnStatusChanged?.Invoke(status);
        }

        private void UpdateProgress(float progress) => OnProgressChanged?.Invoke(progress);

        private void CleanupResources()
        {
            _weldPath?.Dispose(); _nativeMesh?.Dispose();
            _pointCloud?.Dispose(); _robot?.Dispose();
        }
