# Unity C# Wrapper 설계 - Part 3

## 9. WeldPoint.cs - 용접 포인트

```csharp
using System;
using UnityEngine;

namespace SMRWelding.Path
{
    /// <summary>
    /// 위빙 패턴 타입
    /// </summary>
    public enum WeaveType
    {
        None = 0,
        Zigzag = 1,
        Circular = 2,
        Triangle = 3,
        Figure8 = 4
    }

    /// <summary>
    /// 용접 포인트 데이터
    /// </summary>
    [Serializable]
    public struct WeldPoint
    {
        /// <summary>
        /// 위치 (월드 좌표)
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// 표면 법선
        /// </summary>
        public Vector3 Normal;

        /// <summary>
        /// 경로 탄젠트 (진행 방향)
        /// </summary>
        public Vector3 Tangent;

        /// <summary>
        /// 시작점부터의 호장 길이
        /// </summary>
        public float ArcLength;

        public WeldPoint(Vector3 position, Vector3 normal, Vector3 tangent, float arcLength = 0f)
        {
            Position = position;
            Normal = normal;
            Tangent = tangent;
            ArcLength = arcLength;
        }

        /// <summary>
        /// TCP 변환 행렬 생성 (Z-forward, Y-up 기준)
        /// </summary>
        public Matrix4x4 GetTCPTransform(float standoffDistance, float approachAngle)
        {
            // Z축: 법선 반대 방향 (토치가 표면을 향함)
            Vector3 zAxis = -Normal.normalized;
            
            // X축: 탄젠트 방향
            Vector3 xAxis = Tangent.normalized;
            
            // Y축: Z x X
            Vector3 yAxis = Vector3.Cross(zAxis, xAxis).normalized;
            
            // X축 재계산 (직교성 보장)
            xAxis = Vector3.Cross(yAxis, zAxis).normalized;

            // 접근 각도 적용 (토치 기울기)
            if (Mathf.Abs(approachAngle) > 0.001f)
            {
                Quaternion tilt = Quaternion.AngleAxis(
                    approachAngle * Mathf.Rad2Deg, xAxis);
                zAxis = tilt * zAxis;
                yAxis = tilt * yAxis;
            }

            // 스탠드오프 거리 적용
            Vector3 tcpPosition = Position + Normal * standoffDistance;

            Matrix4x4 transform = Matrix4x4.identity;
            transform.SetColumn(0, new Vector4(xAxis.x, xAxis.y, xAxis.z, 0));
            transform.SetColumn(1, new Vector4(yAxis.x, yAxis.y, yAxis.z, 0));
            transform.SetColumn(2, new Vector4(zAxis.x, zAxis.y, zAxis.z, 0));
            transform.SetColumn(3, new Vector4(tcpPosition.x, tcpPosition.y, tcpPosition.z, 1));

            return transform;
        }
    }

    /// <summary>
    /// 경로 생성 파라미터
    /// </summary>
    [Serializable]
    public class PathParameters
    {
        [Tooltip("경로 샘플링 간격 (m)")]
        public float StepSize = 0.001f; // 1mm

        [Tooltip("표면에서 토치까지 거리 (m)")]
        public float StandoffDistance = 0.015f; // 15mm

        [Tooltip("접근 각도 (라디안)")]
        public float ApproachAngle = 0.0f;

        [Tooltip("진행 각도 (라디안)")]
        public float TravelAngle = 0.0f;

        [Header("위빙 설정")]
        public WeaveType WeaveType = WeaveType.None;

        [Tooltip("위빙 폭 (m)")]
        public float WeaveWidth = 0.003f; // 3mm

        [Tooltip("위빙 주파수 (Hz)")]
        public float WeaveFrequency = 2.0f;

        /// <summary>
        /// 네이티브 구조체로 변환
        /// </summary>
        internal SMRWelding.Native.PathParamsNative ToNative()
        {
            return new SMRWelding.Native.PathParamsNative
            {
                stepSize = StepSize,
                standoffDistance = StandoffDistance,
                approachAngle = ApproachAngle,
                travelAngle = TravelAngle,
                weaveType = (int)WeaveType,
                weaveWidth = WeaveWidth,
                weaveFrequency = WeaveFrequency
            };
        }
    }
}
```

---

## 10. WeldPathPlanner.cs - 용접 경로 계획

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using SMRWelding.Native;
using SMRWelding.Robot;
using SMRWelding.Mesh;

namespace SMRWelding.Path
{
    /// <summary>
    /// 용접 경로 계획기
    /// </summary>
    public class WeldPathPlanner : NativeHandle
    {
        #region Properties

        /// <summary>
        /// 경로 포인트 개수
        /// </summary>
        public int PointCount
        {
            get
            {
                if (!IsValid) return 0;
                return NativePluginBindings.GetPathPointCount(_handle);
            }
        }

        /// <summary>
        /// 총 경로 길이 (m)
        /// </summary>
        public float TotalLength
        {
            get
            {
                if (!IsValid) return 0f;
                return NativePluginBindings.GetPathTotalLength(_handle);
            }
        }

        #endregion

        #region Path Creation

        /// <summary>
        /// 사용자 정의 포인트에서 경로 생성
        /// </summary>
        public void CreateFromPoints(WeldPoint[] points)
        {
            if (points == null || points.Length == 0)
                throw new ArgumentException("Points array is empty.");

            // 기존 핸들 해제
            if (_handle != IntPtr.Zero)
            {
                NativePluginBindings.DestroyPath(_handle);
                _handle = IntPtr.Zero;
            }

            // WeldPoint → WeldPointNative 변환
            WeldPointNative[] nativePoints = new WeldPointNative[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                nativePoints[i] = new WeldPointNative
                {
                    position = new float[] 
                    { 
                        points[i].Position.x, 
                        points[i].Position.y, 
                        points[i].Position.z 
                    },
                    normal = new float[] 
                    { 
                        points[i].Normal.x, 
                        points[i].Normal.y, 
                        points[i].Normal.z 
                    },
                    tangent = new float[] 
                    { 
                        points[i].Tangent.x, 
                        points[i].Tangent.y, 
                        points[i].Tangent.z 
                    },
                    arcLength = points[i].ArcLength
                };
            }

            _handle = NativePluginBindings.CreatePathFromPoints(
                nativePoints, points.Length);

            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create path.");
        }

        /// <summary>
        /// 메쉬 엣지에서 경로 추출
        /// </summary>
        public void ExtractFromMeshEdge(MeshGenerator mesh, int[] edgeVertexIndices)
        {
            if (mesh == null || !mesh.IsValid)
                throw new ArgumentException("Invalid mesh.");

            if (_handle != IntPtr.Zero)
            {
                NativePluginBindings.DestroyPath(_handle);
                _handle = IntPtr.Zero;
            }

            _handle = NativePluginBindings.ExtractWeldPathFromEdge(
                mesh.Handle, edgeVertexIndices, edgeVertexIndices.Length / 2);

            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to extract path from edge.");
        }

        #endregion

        #region Path Modification

        /// <summary>
        /// 위빙 패턴 적용
        /// </summary>
        public void ApplyWeavePattern(WeaveType type, float width, float frequency)
        {
            ThrowIfInvalid();

            int result = NativePluginBindings.ApplyWeavePattern(
                _handle, (int)type, width, frequency);

            if (result != 0)
                throw new InvalidOperationException("Failed to apply weave pattern.");
        }

        /// <summary>
        /// 경로 리샘플링
        /// </summary>
        public void Resample(float stepSize)
        {
            ThrowIfInvalid();

            int result = NativePluginBindings.ResamplePath(_handle, stepSize);
            if (result != 0)
                throw new InvalidOperationException("Failed to resample path.");
        }

        /// <summary>
        /// 경로 스무딩
        /// </summary>
        public void Smooth(int windowSize = 5, int iterations = 1)
        {
            ThrowIfInvalid();

            int result = NativePluginBindings.SmoothPath(_handle, windowSize, iterations);
            if (result != 0)
                throw new InvalidOperationException("Failed to smooth path.");
        }

        #endregion

        #region Data Extraction

        /// <summary>
        /// 경로 포인트 가져오기
        /// </summary>
        public WeldPoint[] GetPoints()
        {
            if (!IsValid) return Array.Empty<WeldPoint>();

            int count = PointCount;
            if (count == 0) return Array.Empty<WeldPoint>();

            WeldPointNative[] nativePoints = new WeldPointNative[count];
            NativePluginBindings.GetPathPoints(_handle, nativePoints, count);

            WeldPoint[] points = new WeldPoint[count];
            for (int i = 0; i < count; i++)
            {
                points[i] = new WeldPoint(
                    new Vector3(
                        nativePoints[i].position[0],
                        nativePoints[i].position[1],
                        nativePoints[i].position[2]),
                    new Vector3(
                        nativePoints[i].normal[0],
                        nativePoints[i].normal[1],
                        nativePoints[i].normal[2]),
                    new Vector3(
                        nativePoints[i].tangent[0],
                        nativePoints[i].tangent[1],
                        nativePoints[i].tangent[2]),
                    nativePoints[i].arcLength
                );
            }

            return points;
        }

        /// <summary>
        /// TCP 변환 행렬 배열 생성
        /// </summary>
        public Matrix4x4[] GetTCPTransforms(PathParameters parameters)
        {
            WeldPoint[] points = GetPoints();
            Matrix4x4[] transforms = new Matrix4x4[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                transforms[i] = points[i].GetTCPTransform(
                    parameters.StandoffDistance,
                    parameters.ApproachAngle);
            }

            return transforms;
        }

        #endregion

        #region Joint Path Conversion

        /// <summary>
        /// 경로를 조인트 경로로 변환
        /// </summary>
        public JointState[] ConvertToJointPath(
            RobotModel robot,
            PathParameters parameters,
            JointState initialJoints)
        {
            ThrowIfInvalid();

            if (robot == null || !robot.IsValid)
                throw new ArgumentException("Invalid robot model.");

            int pointCount = PointCount;
            if (pointCount == 0)
                return Array.Empty<JointState>();

            PathParamsNative nativeParams = parameters.ToNative();
            float[] jointPathFlat = new float[pointCount * 6];

            int validPoints = NativePluginBindings.ConvertPathToJoints(
                _handle,
                robot.Handle,
                ref nativeParams,
                initialJoints.Angles,
                jointPathFlat,
                pointCount);

            if (validPoints <= 0)
                throw new InvalidOperationException("Failed to convert path to joints.");

            JointState[] jointPath = new JointState[validPoints];
            for (int i = 0; i < validPoints; i++)
            {
                jointPath[i] = new JointState();
                jointPath[i].Angles = new float[6];
                Array.Copy(jointPathFlat, i * 6, jointPath[i].Angles, 0, 6);
            }

            return jointPath;
        }

        /// <summary>
        /// 경로 도달성 검사
        /// </summary>
        public float CheckReachability(RobotModel robot, PathParameters parameters)
        {
            ThrowIfInvalid();

            if (robot == null || !robot.IsValid)
                throw new ArgumentException("Invalid robot model.");

            PathParamsNative nativeParams = parameters.ToNative();
            return NativePluginBindings.CheckPathReachability(
                _handle, robot.Handle, ref nativeParams);
        }

        #endregion

        #region Native Resource

        protected override void ReleaseNativeResource()
        {
            NativePluginBindings.DestroyPath(_handle);
        }

        #endregion
    }
}
```

---

## 11. 비동기 처리 래퍼

```csharp
using System;
using System.Threading.Tasks;
using UnityEngine;
using SMRWelding.Native;
using SMRWelding.PointCloud;
using SMRWelding.Mesh;
using SMRWelding.Robot;
using SMRWelding.Path;

namespace SMRWelding.Async
{
    /// <summary>
    /// 비동기 메쉬 생성 래퍼
    /// </summary>
    public static class AsyncMeshGenerator
    {
        /// <summary>
        /// 비동기 Poisson 재구성
        /// </summary>
        public static async Task<MeshGenerator> GenerateMeshAsync(
            PointCloudData pointCloud,
            PoissonSettings settings,
            IProgress<float> progress = null)
        {
            return await Task.Run(() =>
            {
                progress?.Report(0.1f);

                // 포인트 클라우드 처리
                using (var processor = new PointCloudProcessor(pointCloud))
                {
                    progress?.Report(0.2f);

                    // 법선 추정
                    if (!processor.HasNormals)
                    {
                        processor.EstimateNormalsKNN(30);
                        processor.OrientNormals(10);
                    }

                    progress?.Report(0.4f);

                    // 메쉬 생성
                    var meshGen = new MeshGenerator();
                    meshGen.GenerateFromPointCloud(processor, settings);

                    progress?.Report(1.0f);

                    return meshGen;
                }
            });
        }

        /// <summary>
        /// 비동기 경로 변환
        /// </summary>
        public static async Task<JointState[]> ConvertPathToJointsAsync(
            WeldPathPlanner path,
            RobotModel robot,
            PathParameters parameters,
            JointState initialJoints,
            IProgress<float> progress = null)
        {
            return await Task.Run(() =>
            {
                progress?.Report(0.1f);
                var result = path.ConvertToJointPath(robot, parameters, initialJoints);
                progress?.Report(1.0f);
                return result;
            });
        }
    }
}
```

---

## 12. 사용 예제

### 12.1 기본 사용 예제

```csharp
using UnityEngine;
using SMRWelding.PointCloud;
using SMRWelding.Mesh;
using SMRWelding.Robot;
using SMRWelding.Path;

public class WeldingPipelineExample : MonoBehaviour
{
    [Header("Settings")]
    public string pointCloudPath = "Assets/Data/weld_scan.ply";
    public RobotType robotType = RobotType.UR10;
    
    [Header("Poisson Settings")]
    public PoissonSettings poissonSettings = new PoissonSettings();
    
    [Header("Path Settings")]
    public PathParameters pathParameters = new PathParameters();

    [Header("Output")]
    public MeshFilter outputMeshFilter;

    private PointCloudProcessor _pointCloud;
    private MeshGenerator _meshGenerator;
    private RobotModel _robot;
    private WeldPathPlanner _pathPlanner;

    void Start()
    {
        // 1. 포인트 클라우드 로드
        _pointCloud = new PointCloudProcessor(pointCloudPath);
        Debug.Log($"Loaded {_pointCloud.PointCount} points");

        // 2. 전처리
        _pointCloud.DownsampleVoxel(0.002f); // 2mm 복셀
        _pointCloud.RemoveOutliers(20, 2.0f);
        _pointCloud.EstimateNormalsKNN(30);
        _pointCloud.OrientNormals(10);

        // 3. Poisson 메쉬 생성
        _meshGenerator = new MeshGenerator();
        _meshGenerator.GenerateFromPointCloud(_pointCloud, poissonSettings);
        Debug.Log($"Generated mesh: {_meshGenerator.VertexCount} vertices");

        // 4. Unity 메쉬로 변환 및 표시
        if (outputMeshFilter != null)
        {
            outputMeshFilter.mesh = _meshGenerator.ToUnityMesh();
        }

        // 5. 로봇 모델 생성
        _robot = new RobotModel(robotType);

        // 6. 테스트 FK
        var testJoints = new JointState(new float[] 
        { 
            0, -Mathf.PI/4, Mathf.PI/2, 0, Mathf.PI/2, 0 
        });
        Matrix4x4 tcp = _robot.ForwardKinematics(testJoints);
        Debug.Log($"TCP Position: {(Vector3)tcp.GetColumn(3)}");
    }

    void OnDestroy()
    {
        // 리소스 정리
        _pathPlanner?.Dispose();
        _robot?.Dispose();
        _meshGenerator?.Dispose();
        _pointCloud?.Dispose();
    }
}
```

### 12.2 경로 생성 예제

```csharp
using UnityEngine;
using System.Collections.Generic;
using SMRWelding.Robot;
using SMRWelding.Path;

public class PathGenerationExample : MonoBehaviour
{
    public RobotType robotType = RobotType.UR10;
    public PathParameters pathParams = new PathParameters();
    
    [Header("Visualization")]
    public LineRenderer pathLine;
    public Color reachableColor = Color.green;
    public Color unreachableColor = Color.red;

    private RobotModel _robot;
    private WeldPathPlanner _pathPlanner;

    void Start()
    {
        _robot = new RobotModel(robotType);
        _pathPlanner = new WeldPathPlanner();

        // 샘플 원형 용접 경로 생성
        CreateCircularWeldPath(
            center: new Vector3(0.5f, 0, 0.3f),
            radius: 0.05f,
            normal: Vector3.up,
            numPoints: 100
        );

        // 도달성 검사
        float reachability = _pathPlanner.CheckReachability(_robot, pathParams);
        Debug.Log($"Path reachability: {reachability * 100:F1}%");

        // 조인트 경로 변환
        var initialJoints = JointState.Zero;
        try
        {
            var jointPath = _pathPlanner.ConvertToJointPath(
                _robot, pathParams, initialJoints);
            Debug.Log($"Generated {jointPath.Length} joint configurations");

            // 경로 시각화
            VisualizePath(jointPath);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Path conversion failed: {e.Message}");
        }
    }

    void CreateCircularWeldPath(
        Vector3 center, 
        float radius, 
        Vector3 normal, 
        int numPoints)
    {
        var points = new List<WeldPoint>();
        Vector3 up = Vector3.up;
        
        if (Vector3.Dot(normal, up) > 0.9f)
            up = Vector3.forward;
        
        Vector3 tangent1 = Vector3.Cross(normal, up).normalized;
        Vector3 tangent2 = Vector3.Cross(normal, tangent1).normalized;

        float arcLength = 0f;
        Vector3 prevPos = center + tangent1 * radius;

        for (int i = 0; i <= numPoints; i++)
        {
            float angle = (i / (float)numPoints) * Mathf.PI * 2f;
            Vector3 pos = center + 
                (tangent1 * Mathf.Cos(angle) + tangent2 * Mathf.Sin(angle)) * radius;
            
            Vector3 tangent = new Vector3(
                -Mathf.Sin(angle), 0, Mathf.Cos(angle)).normalized;

            if (i > 0)
                arcLength += Vector3.Distance(prevPos, pos);
            prevPos = pos;

            points.Add(new WeldPoint(pos, normal, tangent, arcLength));
        }

        _pathPlanner.CreateFromPoints(points.ToArray());
    }

    void VisualizePath(JointState[] jointPath)
    {
        if (pathLine == null) return;

        var positions = new List<Vector3>();
        var colors = new List<Color>();

        foreach (var joints in jointPath)
        {
            Matrix4x4 tcp = _robot.ForwardKinematics(joints);
            positions.Add(tcp.GetColumn(3));

            float manipulability = _robot.GetManipulability(joints);
            colors.Add(Color.Lerp(unreachableColor, reachableColor, manipulability));
        }

        pathLine.positionCount = positions.Count;
        pathLine.SetPositions(positions.ToArray());
    }

    void OnDestroy()
    {
        _pathPlanner?.Dispose();
        _robot?.Dispose();
    }
}
```

---

## 13. 요약

| 클래스 | 역할 | 네이티브 리소스 |
|--------|------|-----------------|
| NativeHandle | IDisposable 기본 클래스 | - |
| NativeArrayHelper | 배열 변환 헬퍼 | - |
| PointCloudProcessor | 포인트 클라우드 처리 | PointCloudHandle |
| MeshGenerator | Poisson 메쉬 생성 | MeshHandle |
| RobotModel | 로봇 FK/IK | RobotHandle |
| WeldPathPlanner | 용접 경로 계획 | PathHandle |

### 핵심 패턴
- **IDisposable**: 모든 네이티브 리소스 관리
- **NativeArrayHelper**: Unity ↔ C++ 데이터 변환
- **Task.Run**: 무거운 연산 비동기 처리
- **Progress\<T>**: 진행률 보고

---

*다음: 04_Unity_Sample_Scene.md*
