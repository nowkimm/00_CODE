// =============================================================================
// WeldingPipelineController.cs - Unity MonoBehaviour Pipeline Controller
// =============================================================================
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace SMRWelding.Components
{
    using Native;

    /// <summary>
    /// Unity MonoBehaviour controller for the welding pipeline
    /// </summary>
    public class WeldingPipelineController : MonoBehaviour
    {
        [Header("Pipeline Configuration")]
        [SerializeField] private RobotType robotType = RobotType.UR5;
        
        [Header("Point Cloud Settings")]
        [SerializeField] private float voxelSize = 0.002f;
        [SerializeField] private int normalKNN = 30;
        [SerializeField] private int outlierNeighbors = 20;
        [SerializeField] private float outlierStdRatio = 2.0f;

        [Header("Mesh Settings")]
        [SerializeField] private int poissonDepth = 8;
        [SerializeField] private float densityThreshold = 0.01f;
        [SerializeField] private int simplifyTarget = 0;

        [Header("Path Settings")]
        [SerializeField] private float pathStepSize = 0.005f;
        [SerializeField] private float standoffDistance = 0.015f;
        [SerializeField] private WeaveType weavePattern = WeaveType.None;
        [SerializeField] private float weaveAmplitude = 0.002f;
        [SerializeField] private float weaveFrequency = 2.0f;
        [SerializeField] private int smoothWindowSize = 5;

        [Header("Visualization")]
        [SerializeField] private MeshFilter meshOutput;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private Material meshMaterial;
        [SerializeField] private PathVisualizer pathVisualizer;

        [Header("Events")]
        public UnityEvent<string> OnStatusChanged;
        public UnityEvent<float> OnProgressChanged;
        public UnityEvent OnPipelineComplete;
        public UnityEvent<string> OnError;

        private WeldingPipeline _pipeline;
        private bool _isRunning;

        public bool IsRunning => _isRunning;
        public PipelineState CurrentState => _pipeline?.State ?? PipelineState.Idle;
        public Mesh GeneratedMesh => _pipeline?.GeneratedMesh;
        public Vector3[] PathPositions => _pipeline?.PathPositions;
        public double[][] JointTrajectory => _pipeline?.JointTrajectory;

        private void Awake()
        {
            CreatePipeline();
        }

        private void OnDestroy()
        {
            _pipeline?.Dispose();
        }

        private void CreatePipeline()
        {
            _pipeline?.Dispose();

            var config = new WeldingPipeline.Config
            {
                VoxelSize = voxelSize,
                NormalKNN = normalKNN,
                OutlierNeighbors = outlierNeighbors,
                OutlierStdRatio = outlierStdRatio,
                PoissonDepth = poissonDepth,
                DensityThreshold = densityThreshold,
                SimplifyTarget = simplifyTarget,
                PathStepSize = pathStepSize,
                StandoffDistance = standoffDistance,
                WeavePattern = weavePattern,
                WeaveAmplitude = weaveAmplitude,
                WeaveFrequency = weaveFrequency,
                SmoothWindowSize = smoothWindowSize,
                RobotType = robotType
            };

            _pipeline = new WeldingPipeline(config);
            _pipeline.ProgressChanged += OnPipelineProgress;
        }

        private void OnPipelineProgress(object sender, PipelineProgressEventArgs e)
        {
            // Ensure we're on main thread for Unity events
            UnityMainThreadDispatcher.Enqueue(() =>
            {
                OnStatusChanged?.Invoke(e.Message);
                OnProgressChanged?.Invoke(e.Progress);

                if (e.State == PipelineState.Error)
                {
                    OnError?.Invoke(e.Message);
                }
            });
        }

        /// <summary>
        /// Run pipeline from file (coroutine for async operation)
        /// </summary>
        public void RunFromFile(string pointCloudPath)
        {
            if (_isRunning)
            {
                Debug.LogWarning("Pipeline already running");
                return;
            }

            StartCoroutine(RunPipelineCoroutine(() => _pipeline.RunFromFile(pointCloudPath)));
        }

        /// <summary>
        /// Run pipeline from Unity points
        /// </summary>
        public void RunFromPoints(Vector3[] points)
        {
            if (_isRunning)
            {
                Debug.LogWarning("Pipeline already running");
                return;
            }

            StartCoroutine(RunPipelineCoroutine(() => _pipeline.RunFromPoints(points)));
        }

        private IEnumerator RunPipelineCoroutine(Action pipelineAction)
        {
            _isRunning = true;
            CreatePipeline();

            // Run pipeline on background thread
            bool success = false;
            Exception error = null;

            var task = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    pipelineAction();
                    success = true;
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });

            while (!task.IsCompleted)
            {
                yield return null;
            }

            _isRunning = false;

            if (success)
            {
                // Update visualization on main thread
                UpdateVisualization();
                OnPipelineComplete?.Invoke();
            }
            else
            {
                OnError?.Invoke(error?.Message ?? "Unknown error");
            }
        }

        private void UpdateVisualization()
        {
            // Update mesh
            if (meshOutput != null && _pipeline.HasMesh)
            {
                meshOutput.mesh = _pipeline.GeneratedMesh;
                
                if (meshRenderer != null && meshMaterial != null)
                {
                    meshRenderer.material = meshMaterial;
                }
            }

            // Update path
            if (pathVisualizer != null && _pipeline.HasPath)
            {
                pathVisualizer.SetPath(_pipeline.PathPositions, _pipeline.Reachability);
            }
        }

        /// <summary>
        /// Save generated mesh to file
        /// </summary>
        public void SaveMesh(string path)
        {
            if (_pipeline?.GeneratedMesh == null)
            {
                Debug.LogWarning("No mesh to save");
                return;
            }

            // Save using native wrapper
            // Note: This requires access to the native MeshWrapper
            Debug.Log($"Mesh save requested to: {path}");
        }

        /// <summary>
        /// Reset pipeline
        /// </summary>
        public void Reset()
        {
            if (_isRunning)
            {
                Debug.LogWarning("Cannot reset while running");
                return;
            }

            _pipeline?.Dispose();
            _pipeline = null;

            if (meshOutput != null)
                meshOutput.mesh = null;

            if (pathVisualizer != null)
                pathVisualizer.Clear();

            CreatePipeline();
            OnStatusChanged?.Invoke("Ready");
        }

        // Editor helper for testing
        [ContextMenu("Test with Sample Data")]
        private void TestWithSampleData()
        {
            // Generate sample point cloud (hemisphere)
            int n = 1000;
            Vector3[] points = new Vector3[n];
            float radius = 0.5f;

            for (int i = 0; i < n; i++)
            {
                float u = UnityEngine.Random.value;
                float v = UnityEngine.Random.value;
                float theta = 2 * Mathf.PI * u;
                float phi = Mathf.Acos(2 * v - 1) / 2; // hemisphere

                points[i] = new Vector3(
                    radius * Mathf.Sin(phi) * Mathf.Cos(theta),
                    radius * Mathf.Sin(phi) * Mathf.Sin(theta),
                    radius * Mathf.Cos(phi)
                );
            }

            RunFromPoints(points);
        }
    }

    /// <summary>
    /// Simple dispatcher for Unity main thread callbacks
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private static readonly System.Collections.Generic.Queue<Action> _queue = new();

        public static void Enqueue(Action action)
        {
            if (_instance == null)
            {
                var go = new GameObject("MainThreadDispatcher");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }

            lock (_queue)
            {
                _queue.Enqueue(action);
            }
        }

        private void Update()
        {
            lock (_queue)
            {
                while (_queue.Count > 0)
                {
                    _queue.Dequeue()?.Invoke();
                }
            }
        }
    }
}
