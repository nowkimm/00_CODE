# Unity 샘플 씬 구성 가이드

## 1. 프로젝트 구조

### 1.1 폴더 구조
```
Assets/
├── SMRWelding/
│   ├── Plugins/
│   │   └── x86_64/
│   │       └── SMRWeldingPlugin.dll
│   ├── Scripts/
│   │   ├── Native/
│   │   │   ├── NativePluginBindings.cs
│   │   │   ├── NativeHandle.cs
│   │   │   └── NativeArrayHelper.cs
│   │   ├── PointCloud/
│   │   │   ├── PointCloudData.cs
│   │   │   └── PointCloudProcessor.cs
│   │   ├── Mesh/
│   │   │   ├── MeshGenerator.cs
│   │   │   └── MeshData.cs
│   │   ├── Robot/
│   │   │   ├── RobotModel.cs
│   │   │   ├── JointState.cs
│   │   │   └── RobotVisualizer.cs
│   │   ├── Path/
│   │   │   ├── WeldPoint.cs
│   │   │   ├── WeldPathPlanner.cs
│   │   │   └── PathVisualizer.cs
│   │   └── UI/
│   │       ├── WeldingPipelineController.cs
│   │       └── UIManager.cs
│   ├── Prefabs/
│   │   ├── PointCloudRenderer.prefab
│   │   ├── RobotUR10.prefab
│   │   └── WeldPath.prefab
│   ├── Materials/
│   │   ├── PointCloud.mat
│   │   ├── MeshSurface.mat
│   │   └── WeldPath.mat
│   ├── Shaders/
│   │   └── PointCloudShader.shader
│   └── Scenes/
│       └── WeldingDemo.unity
└── StreamingAssets/
    └── SampleData/
        └── weld_scan.ply
```

---

## 2. 핵심 컴포넌트

### 2.1 WeldingPipelineController.cs

```csharp
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using SMRWelding.PointCloud;
using SMRWelding.Mesh;
using SMRWelding.Robot;
using SMRWelding.Path;
using SMRWelding.Async;

namespace SMRWelding.Demo
{
    /// <summary>
    /// 메인 파이프라인 컨트롤러
    /// </summary>
    public class WeldingPipelineController : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Input")]
        [SerializeField] private string _pointCloudPath = "";
        [SerializeField] private bool _useStreamingAssets = true;

        [Header("Robot Settings")]
        [SerializeField] private RobotType _robotType = RobotType.UR10;
        [SerializeField] private Transform _robotBase;

        [Header("Reconstruction Settings")]
        [SerializeField] private PoissonSettings _poissonSettings = new PoissonSettings();
        [SerializeField] private float _voxelSize = 0.002f;
        [SerializeField] private int _normalKNN = 30;

        [Header("Path Settings")]
        [SerializeField] private PathParameters _pathParameters = new PathParameters();

        [Header("Output")]
        [SerializeField] private MeshFilter _outputMeshFilter;
        [SerializeField] private MeshRenderer _outputMeshRenderer;
        [SerializeField] private LineRenderer _pathLineRenderer;

        [Header("Events")]
        public UnityEvent<float> OnProgressChanged;
        public UnityEvent<string> OnStatusChanged;
        public UnityEvent OnPipelineComplete;
        public UnityEvent<string> OnError;

        #endregion

        #region Private Fields

        private PointCloudProcessor _pointCloud;
        private MeshGenerator _meshGenerator;
        private RobotModel _robot;
        private WeldPathPlanner _pathPlanner;
        private JointState[] _jointPath;

        private bool _isProcessing = false;
        private float _progress = 0f;

        #endregion

        #region Properties

        public bool IsProcessing => _isProcessing;
        public float Progress => _progress;
        public int PointCount => _pointCloud?.PointCount ?? 0;
        public int VertexCount => _meshGenerator?.VertexCount ?? 0;
        public int PathPointCount => _pathPlanner?.PointCount ?? 0;
        public JointState[] JointPath => _jointPath;

        #endregion

        #region Unity Lifecycle

        void OnDestroy()
        {
            DisposeResources();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 전체 파이프라인 실행
        /// </summary>
        public void RunPipeline()
        {
            if (_isProcessing)
            {
                Debug.LogWarning("Pipeline is already running.");
                return;
            }

            StartCoroutine(RunPipelineCoroutine());
        }

        /// <summary>
        /// 포인트 클라우드 로드
        /// </summary>
        public void LoadPointCloud(string path = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                path = _useStreamingAssets 
                    ? System.IO.Path.Combine(
                        Application.streamingAssetsPath, 
                        _pointCloudPath)
                    : _pointCloudPath;
            }

            try
            {
                _pointCloud?.Dispose();
                _pointCloud = new PointCloudProcessor(path);
                OnStatusChanged?.Invoke($"Loaded {_pointCloud.PointCount} points");
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Failed to load: {e.Message}");
            }
        }

        /// <summary>
        /// 로봇 모델 초기화
        /// </summary>
        public void InitializeRobot(RobotType type)
        {
            try
            {
                _robot?.Dispose();
                _robot = new RobotModel(type);
                _robotType = type;
                OnStatusChanged?.Invoke($"Robot initialized: {type}");
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Robot init failed: {e.Message}");
            }
        }

        /// <summary>
        /// 수동 조인트 설정 및 FK 테스트
        /// </summary>
        public Vector3 TestForwardKinematics(float[] joints)
        {
            if (_robot == null || !_robot.IsValid)
                return Vector3.zero;

            var jointState = new JointState(joints);
            Matrix4x4 tcp = _robot.ForwardKinematics(jointState);
            return tcp.GetColumn(3);
        }

        #endregion

        #region Pipeline Coroutine

        private IEnumerator RunPipelineCoroutine()
        {
            _isProcessing = true;
            _progress = 0f;

            // 1. Load Point Cloud
            yield return StartCoroutine(LoadPointCloudStep());
            if (!_isProcessing) yield break;

            // 2. Preprocess
            yield return StartCoroutine(PreprocessStep());
            if (!_isProcessing) yield break;

            // 3. Generate Mesh
            yield return StartCoroutine(GenerateMeshStep());
            if (!_isProcessing) yield break;

            // 4. Initialize Robot
            yield return StartCoroutine(InitializeRobotStep());
            if (!_isProcessing) yield break;

            // 5. Generate Path (Optional)
            // yield return StartCoroutine(GeneratePathStep());

            _progress = 1f;
            OnProgressChanged?.Invoke(_progress);
            OnStatusChanged?.Invoke("Pipeline complete!");
            OnPipelineComplete?.Invoke();
            _isProcessing = false;
        }

        private IEnumerator LoadPointCloudStep()
        {
            OnStatusChanged?.Invoke("Loading point cloud...");
            _progress = 0.1f;
            OnProgressChanged?.Invoke(_progress);

            string path = _useStreamingAssets
                ? System.IO.Path.Combine(Application.streamingAssetsPath, _pointCloudPath)
                : _pointCloudPath;

            yield return null;

            try
            {
                _pointCloud?.Dispose();
                _pointCloud = new PointCloudProcessor(path);
                OnStatusChanged?.Invoke($"Loaded {_pointCloud.PointCount} points");
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Load failed: {e.Message}");
                _isProcessing = false;
            }
        }

        private IEnumerator PreprocessStep()
        {
            OnStatusChanged?.Invoke("Preprocessing...");
            _progress = 0.2f;
            OnProgressChanged?.Invoke(_progress);

            yield return null;

            try
            {
                // Downsample
                _pointCloud.DownsampleVoxel(_voxelSize);
                OnStatusChanged?.Invoke($"Downsampled to {_pointCloud.PointCount} points");
                _progress = 0.25f;
                OnProgressChanged?.Invoke(_progress);
                yield return null;

                // Remove outliers
                _pointCloud.RemoveOutliers(20, 2.0f);
                _progress = 0.3f;
                OnProgressChanged?.Invoke(_progress);
                yield return null;

                // Estimate normals
                _pointCloud.EstimateNormalsKNN(_normalKNN);
                _progress = 0.35f;
                OnProgressChanged?.Invoke(_progress);
                yield return null;

                // Orient normals
                _pointCloud.OrientNormals(10);
                _progress = 0.4f;
                OnProgressChanged?.Invoke(_progress);

                OnStatusChanged?.Invoke("Preprocessing complete");
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Preprocess failed: {e.Message}");
                _isProcessing = false;
            }
        }

        private IEnumerator GenerateMeshStep()
        {
            OnStatusChanged?.Invoke("Generating mesh...");
            _progress = 0.5f;
            OnProgressChanged?.Invoke(_progress);

            yield return null;

            try
            {
                _meshGenerator?.Dispose();
                _meshGenerator = new MeshGenerator();
                _meshGenerator.GenerateFromPointCloud(_pointCloud, _poissonSettings);

                _progress = 0.7f;
                OnProgressChanged?.Invoke(_progress);

                OnStatusChanged?.Invoke(
                    $"Mesh generated: {_meshGenerator.VertexCount} vertices, " +
                    $"{_meshGenerator.TriangleCount} triangles");

                // Unity Mesh 적용
                if (_outputMeshFilter != null)
                {
                    _outputMeshFilter.mesh = _meshGenerator.ToUnityMesh();
                }

                _progress = 0.8f;
                OnProgressChanged?.Invoke(_progress);
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Mesh generation failed: {e.Message}");
                _isProcessing = false;
            }
        }

        private IEnumerator InitializeRobotStep()
        {
            OnStatusChanged?.Invoke("Initializing robot...");
            _progress = 0.85f;
            OnProgressChanged?.Invoke(_progress);

            yield return null;

            try
            {
                _robot?.Dispose();
                _robot = new RobotModel(_robotType);
                OnStatusChanged?.Invoke($"Robot ready: {_robotType}");

                _progress = 0.9f;
                OnProgressChanged?.Invoke(_progress);
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Robot init failed: {e.Message}");
                _isProcessing = false;
            }
        }

        #endregion

        #region Resource Management

        private void DisposeResources()
        {
            _pathPlanner?.Dispose();
            _robot?.Dispose();
            _meshGenerator?.Dispose();
            _pointCloud?.Dispose();

            _pathPlanner = null;
            _robot = null;
            _meshGenerator = null;
            _pointCloud = null;
        }

        #endregion
    }
}
```

---

## 3. 포인트 클라우드 렌더러

### 3.1 PointCloudRenderer.cs

```csharp
using UnityEngine;
using SMRWelding.PointCloud;

namespace SMRWelding.Demo
{
    /// <summary>
    /// 포인트 클라우드 시각화
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class PointCloudRenderer : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _pointSize = 0.005f;
        [SerializeField] private Material _pointMaterial;
        [SerializeField] private bool _showNormals = false;
        [SerializeField] private float _normalLength = 0.01f;

        private MeshFilter _meshFilter;
        private PointCloudData _data;

        void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
        }

        /// <summary>
        /// 포인트 클라우드 데이터 설정
        /// </summary>
        public void SetData(PointCloudData data)
        {
            _data = data;
            UpdateVisualization();
        }

        /// <summary>
        /// 포인트 클라우드 시각화 업데이트
        /// </summary>
        public void UpdateVisualization()
        {
            if (_data == null || _data.Count == 0)
                return;

            // 포인트마다 쿼드 생성 (간단한 방법)
            // 대규모 포인트클라우드는 GPU 인스턴싱 또는 컴퓨트 셰이더 권장
            var mesh = GeneratePointMesh(_data);
            _meshFilter.mesh = mesh;
        }

        private Mesh GeneratePointMesh(PointCloudData data)
        {
            int pointCount = Mathf.Min(data.Count, 65000); // 메쉬 정점 제한

            Vector3[] vertices = new Vector3[pointCount * 4];
            int[] triangles = new int[pointCount * 6];
            Color[] colors = new Color[pointCount * 4];
            Vector2[] uvs = new Vector2[pointCount * 4];

            float halfSize = _pointSize * 0.5f;

            for (int i = 0; i < pointCount; i++)
            {
                Vector3 pos = data.Points[i];
                Color col = data.HasColors ? data.Colors[i] : Color.white;

                // 빌보드 쿼드 (카메라 방향)
                int vi = i * 4;
                vertices[vi + 0] = pos + new Vector3(-halfSize, -halfSize, 0);
                vertices[vi + 1] = pos + new Vector3(halfSize, -halfSize, 0);
                vertices[vi + 2] = pos + new Vector3(halfSize, halfSize, 0);
                vertices[vi + 3] = pos + new Vector3(-halfSize, halfSize, 0);

                colors[vi + 0] = col;
                colors[vi + 1] = col;
                colors[vi + 2] = col;
                colors[vi + 3] = col;

                uvs[vi + 0] = new Vector2(0, 0);
                uvs[vi + 1] = new Vector2(1, 0);
                uvs[vi + 2] = new Vector2(1, 1);
                uvs[vi + 3] = new Vector2(0, 1);

                int ti = i * 6;
                triangles[ti + 0] = vi + 0;
                triangles[ti + 1] = vi + 2;
                triangles[ti + 2] = vi + 1;
                triangles[ti + 3] = vi + 0;
                triangles[ti + 4] = vi + 3;
                triangles[ti + 5] = vi + 2;
            }

            var mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.colors = colors;
            mesh.uv = uvs;
            mesh.RecalculateBounds();

            return mesh;
        }

        void OnDrawGizmosSelected()
        {
            if (!_showNormals || _data == null || !_data.HasNormals)
                return;

            Gizmos.color = Color.cyan;
            int step = Mathf.Max(1, _data.Count / 1000); // 1000개 샘플링

            for (int i = 0; i < _data.Count; i += step)
            {
                Vector3 pos = transform.TransformPoint(_data.Points[i]);
                Vector3 normal = transform.TransformDirection(_data.Normals[i]);
                Gizmos.DrawLine(pos, pos + normal * _normalLength);
            }
        }
    }
}
```

---

## 4. 로봇 시각화

### 4.1 RobotVisualizer.cs

```csharp
using UnityEngine;
using SMRWelding.Robot;

namespace SMRWelding.Demo
{
    /// <summary>
    /// 로봇 조인트 시각화
    /// </summary>
    public class RobotVisualizer : MonoBehaviour
    {
        [Header("Robot")]
        [SerializeField] private RobotType _robotType = RobotType.UR10;
        
        [Header("Joint Transforms")]
        [SerializeField] private Transform[] _jointTransforms = new Transform[6];
        [SerializeField] private Vector3[] _jointAxes = new Vector3[6];

        [Header("TCP")]
        [SerializeField] private Transform _tcpTransform;
        [SerializeField] private float _tcpGizmoSize = 0.05f;

        [Header("Debug")]
        [SerializeField] private bool _showJointGizmos = true;

        private RobotModel _robot;
        private JointState _currentJoints = JointState.Zero;
        private float[] _jointAngles = new float[6];

        void Start()
        {
            InitializeDefaultAxes();
        }

        void OnDestroy()
        {
            _robot?.Dispose();
        }

        /// <summary>
        /// 조인트 각도 설정 (라디안)
        /// </summary>
        public void SetJointAngles(float[] angles)
        {
            if (angles == null || angles.Length != 6)
                return;

            System.Array.Copy(angles, _jointAngles, 6);
            _currentJoints = new JointState(angles);
            UpdateJointVisualization();
        }

        /// <summary>
        /// 개별 조인트 설정
        /// </summary>
        public void SetJoint(int index, float angleRadians)
        {
            if (index < 0 || index >= 6) return;

            _jointAngles[index] = angleRadians;
            _currentJoints = new JointState(_jointAngles);
            UpdateJointVisualization();
        }

        /// <summary>
        /// 조인트 시각화 업데이트
        /// </summary>
        public void UpdateJointVisualization()
        {
            for (int i = 0; i < 6; i++)
            {
                if (_jointTransforms[i] == null) continue;

                float angleDeg = _jointAngles[i] * Mathf.Rad2Deg;
                _jointTransforms[i].localRotation = 
                    Quaternion.AngleAxis(angleDeg, _jointAxes[i]);
            }

            // TCP 위치 업데이트 (RobotModel 사용 시)
            if (_robot != null && _robot.IsValid && _tcpTransform != null)
            {
                Matrix4x4 tcpMatrix = _robot.ForwardKinematics(_currentJoints);
                _tcpTransform.localPosition = tcpMatrix.GetColumn(3);
                _tcpTransform.localRotation = tcpMatrix.rotation;
            }
        }

        private void InitializeDefaultAxes()
        {
            // UR 로봇 기본 조인트 축 (Z-up 기준)
            _jointAxes[0] = Vector3.up;      // Base
            _jointAxes[1] = Vector3.forward; // Shoulder
            _jointAxes[2] = Vector3.forward; // Elbow
            _jointAxes[3] = Vector3.forward; // Wrist 1
            _jointAxes[4] = Vector3.up;      // Wrist 2
            _jointAxes[5] = Vector3.forward; // Wrist 3
        }

        void OnDrawGizmos()
        {
            if (!_showJointGizmos) return;

            // 조인트 위치 표시
            Gizmos.color = Color.yellow;
            foreach (var joint in _jointTransforms)
            {
                if (joint != null)
                    Gizmos.DrawWireSphere(joint.position, 0.02f);
            }

            // TCP 좌표계 표시
            if (_tcpTransform != null)
            {
                Vector3 pos = _tcpTransform.position;
                Gizmos.color = Color.red;
                Gizmos.DrawLine(pos, pos + _tcpTransform.right * _tcpGizmoSize);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, pos + _tcpTransform.up * _tcpGizmoSize);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(pos, pos + _tcpTransform.forward * _tcpGizmoSize);
            }
        }
    }
}
```

---

## 5. 경로 시각화

### 5.1 PathVisualizer.cs

```csharp
using UnityEngine;
using System.Collections.Generic;
using SMRWelding.Path;
using SMRWelding.Robot;

namespace SMRWelding.Demo
{
    /// <summary>
    /// 용접 경로 시각화
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PathVisualizer : MonoBehaviour
    {
        [Header("Visualization")]
        [SerializeField] private float _lineWidth = 0.002f;
        [SerializeField] private Gradient _reachabilityGradient;
        [SerializeField] private bool _showNormals = false;
        [SerializeField] private float _normalLength = 0.01f;

        [Header("Animation")]
        [SerializeField] private bool _animatePath = false;
        [SerializeField] private float _animationSpeed = 0.1f;

        private LineRenderer _lineRenderer;
        private WeldPoint[] _pathPoints;
        private float[] _manipulabilities;
        private int _currentAnimIndex = 0;
        private float _animTimer = 0f;

        void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;

            InitializeGradient();
        }

        /// <summary>
        /// 경로 설정
        /// </summary>
        public void SetPath(WeldPoint[] points, float[] manipulabilities = null)
        {
            _pathPoints = points;
            _manipulabilities = manipulabilities;
            UpdateVisualization();
        }

        /// <summary>
        /// 로봇 조작성으로 경로 색상 설정
        /// </summary>
        public void SetManipulabilities(float[] values)
        {
            _manipulabilities = values;
            UpdateVisualization();
        }

        void Update()
        {
            if (_animatePath && _pathPoints != null && _pathPoints.Length > 0)
            {
                _animTimer += Time.deltaTime * _animationSpeed;
                _currentAnimIndex = Mathf.FloorToInt(_animTimer) % _pathPoints.Length;
            }
        }

        private void UpdateVisualization()
        {
            if (_pathPoints == null || _pathPoints.Length == 0)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            int count = _pathPoints.Length;
            _lineRenderer.positionCount = count;

            Vector3[] positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                positions[i] = transform.TransformPoint(_pathPoints[i].Position);
            }
            _lineRenderer.SetPositions(positions);

            // 조작성 기반 색상
            if (_manipulabilities != null && _manipulabilities.Length == count)
            {
                Gradient gradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[count];
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];

                for (int i = 0; i < count; i++)
                {
                    float t = (float)i / (count - 1);
                    float manip = Mathf.Clamp01(_manipulabilities[i]);
                    colorKeys[i] = new GradientColorKey(
                        _reachabilityGradient.Evaluate(manip), t);
                }

                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);

                gradient.SetKeys(colorKeys, alphaKeys);
                _lineRenderer.colorGradient = gradient;
            }
        }

        private void InitializeGradient()
        {
            if (_reachabilityGradient == null)
            {
                _reachabilityGradient = new Gradient();
                _reachabilityGradient.SetKeys(
                    new GradientColorKey[] 
                    {
                        new GradientColorKey(Color.red, 0f),
                        new GradientColorKey(Color.yellow, 0.5f),
                        new GradientColorKey(Color.green, 1f)
                    },
                    new GradientAlphaKey[] 
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(1f, 1f)
                    }
                );
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!_showNormals || _pathPoints == null)
                return;

            Gizmos.color = Color.cyan;
            int step = Mathf.Max(1, _pathPoints.Length / 50);

            for (int i = 0; i < _pathPoints.Length; i += step)
            {
                Vector3 pos = transform.TransformPoint(_pathPoints[i].Position);
                Vector3 normal = transform.TransformDirection(_pathPoints[i].Normal);
                Gizmos.DrawLine(pos, pos + normal * _normalLength);
            }
        }
    }
}
```

---

## 6. UI Manager

### 6.1 UIManager.cs

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SMRWelding.Demo
{
    /// <summary>
    /// 데모 UI 관리
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WeldingPipelineController _pipeline;

        [Header("Buttons")]
        [SerializeField] private Button _runButton;
        [SerializeField] private Button _loadButton;
        [SerializeField] private Button _resetButton;

        [Header("Progress")]
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TMP_Text _statusText;

        [Header("Info")]
        [SerializeField] private TMP_Text _pointCountText;
        [SerializeField] private TMP_Text _vertexCountText;
        [SerializeField] private TMP_Text _pathCountText;

        void Start()
        {
            // 버튼 이벤트 연결
            _runButton?.onClick.AddListener(OnRunClicked);
            _loadButton?.onClick.AddListener(OnLoadClicked);
            _resetButton?.onClick.AddListener(OnResetClicked);

            // 파이프라인 이벤트 연결
            if (_pipeline != null)
            {
                _pipeline.OnProgressChanged.AddListener(OnProgressChanged);
                _pipeline.OnStatusChanged.AddListener(OnStatusChanged);
                _pipeline.OnPipelineComplete.AddListener(OnComplete);
                _pipeline.OnError.AddListener(OnError);
            }
        }

        void Update()
        {
            UpdateInfoDisplay();
            UpdateButtonStates();
        }

        private void OnRunClicked()
        {
            _pipeline?.RunPipeline();
        }

        private void OnLoadClicked()
        {
            _pipeline?.LoadPointCloud();
        }

        private void OnResetClicked()
        {
            if (_progressSlider != null)
                _progressSlider.value = 0;
            if (_statusText != null)
                _statusText.text = "Ready";
        }

        private void OnProgressChanged(float progress)
        {
            if (_progressSlider != null)
                _progressSlider.value = progress;
        }

        private void OnStatusChanged(string status)
        {
            if (_statusText != null)
                _statusText.text = status;
        }

        private void OnComplete()
        {
            Debug.Log("Pipeline completed!");
        }

        private void OnError(string error)
        {
            if (_statusText != null)
                _statusText.text = $"<color=red>Error: {error}</color>";
        }

        private void UpdateInfoDisplay()
        {
            if (_pipeline == null) return;

            if (_pointCountText != null)
                _pointCountText.text = $"Points: {_pipeline.PointCount:N0}";

            if (_vertexCountText != null)
                _vertexCountText.text = $"Vertices: {_pipeline.VertexCount:N0}";

            if (_pathCountText != null)
                _pathCountText.text = $"Path Points: {_pipeline.PathPointCount:N0}";
        }

        private void UpdateButtonStates()
        {
            bool processing = _pipeline?.IsProcessing ?? false;

            if (_runButton != null)
                _runButton.interactable = !processing;
            if (_loadButton != null)
                _loadButton.interactable = !processing;
        }
    }
}
```

---

## 7. 씬 구성

### 7.1 Hierarchy 구조
```
WeldingDemo
├── Main Camera
├── Directional Light
├── EventSystem
├── --- Systems ---
│   ├── WeldingPipelineController
│   └── UIManager
├── --- Visualization ---
│   ├── PointCloudRenderer
│   ├── MeshOutput (MeshFilter, MeshRenderer)
│   ├── PathVisualizer (LineRenderer)
│   └── RobotVisualizer
│       ├── Base
│       ├── J1, J2, J3, J4, J5, J6
│       └── TCP
└── --- UI ---
    └── Canvas
        ├── Panel_Controls
        │   ├── Btn_Run
        │   ├── Btn_Load
        │   └── Btn_Reset
        ├── Panel_Progress
        │   ├── Slider_Progress
        │   └── Text_Status
        └── Panel_Info
            ├── Text_PointCount
            ├── Text_VertexCount
            └── Text_PathCount
```

---

*다음: 05_Performance_Tuning.md*
