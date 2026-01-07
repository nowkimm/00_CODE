# Unity C# Wrapper 설계 - Part 2

## 6. PointCloudProcessor.cs - 포인트 클라우드 처리

```csharp
using System;
using UnityEngine;
using SMRWelding.Native;

namespace SMRWelding.PointCloud
{
    /// <summary>
    /// 포인트 클라우드 처리기 (네이티브 플러그인 래퍼)
    /// </summary>
    public class PointCloudProcessor : NativeHandle
    {
        #region Properties

        /// <summary>
        /// 포인트 개수
        /// </summary>
        public int PointCount
        {
            get
            {
                ThrowIfInvalid();
                return NativePluginBindings.GetPointCloudCount(_handle);
            }
        }

        /// <summary>
        /// 법선 존재 여부
        /// </summary>
        public bool HasNormals
        {
            get
            {
                ThrowIfInvalid();
                return NativePluginBindings.HasNormals(_handle) != 0;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// 빈 포인트 클라우드 생성
        /// </summary>
        public PointCloudProcessor()
        {
            _handle = NativePluginBindings.CreatePointCloud();
            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create point cloud.");
        }

        /// <summary>
        /// 파일에서 로드
        /// </summary>
        public PointCloudProcessor(string filePath)
        {
            _handle = NativePluginBindings.CreatePointCloud();
            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create point cloud.");

            LoadFromFile(filePath);
        }

        /// <summary>
        /// 데이터에서 생성
        /// </summary>
        public PointCloudProcessor(PointCloudData data)
        {
            _handle = NativePluginBindings.CreatePointCloud();
            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create point cloud.");

            SetData(data);
        }

        #endregion

        #region Data Methods

        /// <summary>
        /// 파일에서 포인트 클라우드 로드
        /// </summary>
        public void LoadFromFile(string filePath)
        {
            ThrowIfInvalid();

            int result;
            string ext = System.IO.Path.GetExtension(filePath).ToLower();

            if (ext == ".ply")
                result = NativePluginBindings.LoadPointCloudPLY(_handle, filePath);
            else if (ext == ".pcd")
                result = NativePluginBindings.LoadPointCloudPCD(_handle, filePath);
            else
                throw new ArgumentException($"Unsupported file format: {ext}");

            if (result != 0)
                throw new InvalidOperationException($"Failed to load point cloud: {filePath}");
        }

        /// <summary>
        /// PointCloudData로 설정
        /// </summary>
        public void SetData(PointCloudData data)
        {
            ThrowIfInvalid();

            if (data == null || data.Count == 0)
                return;

            // 포인트 설정
            float[] points = NativeArrayHelper.Vector3ArrayToFloatArray(data.Points);
            int result = NativePluginBindings.SetPointCloudPoints(_handle, points, data.Count);
            if (result != 0)
                throw new InvalidOperationException("Failed to set point cloud points.");

            // 색상 설정 (있는 경우)
            if (data.HasColors)
            {
                float[] colors = NativeArrayHelper.ColorArrayToFloatArray(data.Colors);
                NativePluginBindings.SetPointCloudColors(_handle, colors, data.Count);
            }
        }

        /// <summary>
        /// PointCloudData로 가져오기
        /// </summary>
        public PointCloudData GetData()
        {
            ThrowIfInvalid();

            int count = PointCount;
            if (count == 0)
                return new PointCloudData();

            var data = new PointCloudData();

            // 포인트 가져오기
            float[] pointsFlat = new float[count * 3];
            NativePluginBindings.GetPointCloudPoints(_handle, pointsFlat, count);
            data.Points = NativeArrayHelper.FloatArrayToVector3Array(pointsFlat);

            // 법선 가져오기 (있는 경우)
            if (HasNormals)
            {
                float[] normalsFlat = new float[count * 3];
                NativePluginBindings.GetPointCloudNormals(_handle, normalsFlat, count);
                data.Normals = NativeArrayHelper.FloatArrayToVector3Array(normalsFlat);
            }

            return data;
        }

        #endregion

        #region Processing Methods

        /// <summary>
        /// KNN 기반 법선 추정
        /// </summary>
        public void EstimateNormalsKNN(int k = 30)
        {
            ThrowIfInvalid();
            int result = NativePluginBindings.EstimateNormalsKNN(_handle, k);
            if (result != 0)
                throw new InvalidOperationException("Failed to estimate normals.");
        }

        /// <summary>
        /// 반경 기반 법선 추정
        /// </summary>
        public void EstimateNormalsRadius(float radius)
        {
            ThrowIfInvalid();
            int result = NativePluginBindings.EstimateNormalsRadius(_handle, radius);
            if (result != 0)
                throw new InvalidOperationException("Failed to estimate normals.");
        }

        /// <summary>
        /// 법선 방향 일관성 정렬
        /// </summary>
        public void OrientNormals(int k = 10, Vector3? referencePoint = null)
        {
            ThrowIfInvalid();

            float[] refPt = null;
            if (referencePoint.HasValue)
            {
                refPt = new float[] 
                { 
                    referencePoint.Value.x, 
                    referencePoint.Value.y, 
                    referencePoint.Value.z 
                };
            }

            int result = NativePluginBindings.OrientNormalsConsistent(_handle, k, refPt);
            if (result != 0)
                throw new InvalidOperationException("Failed to orient normals.");
        }

        /// <summary>
        /// 복셀 다운샘플링
        /// </summary>
        public void DownsampleVoxel(float voxelSize)
        {
            ThrowIfInvalid();
            int result = NativePluginBindings.DownsampleVoxel(_handle, voxelSize);
            if (result != 0)
                throw new InvalidOperationException("Failed to downsample.");
        }

        /// <summary>
        /// 통계적 이상치 제거
        /// </summary>
        public void RemoveOutliers(int nbNeighbors = 20, float stdRatio = 2.0f)
        {
            ThrowIfInvalid();
            int result = NativePluginBindings.RemoveStatisticalOutliers(
                _handle, nbNeighbors, stdRatio);
            if (result != 0)
                throw new InvalidOperationException("Failed to remove outliers.");
        }

        #endregion

        #region Native Resource

        protected override void ReleaseNativeResource()
        {
            NativePluginBindings.DestroyPointCloud(_handle);
        }

        #endregion
    }
}
```

---

## 7. MeshGenerator.cs - 메쉬 생성기

```csharp
using System;
using UnityEngine;
using SMRWelding.Native;
using SMRWelding.PointCloud;

namespace SMRWelding.Mesh
{
    /// <summary>
    /// Poisson Surface Reconstruction 설정
    /// </summary>
    [Serializable]
    public class PoissonSettings
    {
        [Range(6, 12)]
        [Tooltip("Octree 깊이 (높을수록 정밀, 느림)")]
        public int Depth = 9;

        [Range(1.0f, 1.5f)]
        [Tooltip("스케일 팩터")]
        public float Scale = 1.1f;

        [Tooltip("선형 보간 사용")]
        public bool LinearFit = false;

        [Range(0.0f, 1.0f)]
        [Tooltip("저밀도 정점 제거 분위수")]
        public float DensityThreshold = 0.1f;
    }

    /// <summary>
    /// 메쉬 생성기 (네이티브 플러그인 래퍼)
    /// </summary>
    public class MeshGenerator : NativeHandle
    {
        #region Properties

        /// <summary>
        /// 정점 개수
        /// </summary>
        public int VertexCount
        {
            get
            {
                ThrowIfInvalid();
                return NativePluginBindings.GetMeshVertexCount(_handle);
            }
        }

        /// <summary>
        /// 삼각형 개수
        /// </summary>
        public int TriangleCount
        {
            get
            {
                ThrowIfInvalid();
                return NativePluginBindings.GetMeshTriangleCount(_handle);
            }
        }

        #endregion

        #region Generation Methods

        /// <summary>
        /// Poisson Surface Reconstruction 실행
        /// </summary>
        public void GenerateFromPointCloud(
            PointCloudProcessor pointCloud,
            PoissonSettings settings = null)
        {
            if (pointCloud == null || !pointCloud.IsValid)
                throw new ArgumentException("Invalid point cloud.");

            if (!pointCloud.HasNormals)
                throw new InvalidOperationException(
                    "Point cloud must have normals for Poisson reconstruction.");

            settings = settings ?? new PoissonSettings();

            // 기존 핸들 해제
            if (_handle != IntPtr.Zero)
            {
                NativePluginBindings.DestroyMesh(_handle);
                _handle = IntPtr.Zero;
            }

            // Poisson 메쉬 생성
            _handle = NativePluginBindings.CreatePoissonMesh(
                pointCloud.Handle,
                settings.Depth,
                settings.Scale,
                settings.LinearFit ? 1 : 0);

            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create Poisson mesh.");

            // 저밀도 정점 제거
            if (settings.DensityThreshold > 0.0f)
            {
                RemoveLowDensityVertices(settings.DensityThreshold);
            }
        }

        /// <summary>
        /// 저밀도 정점 제거
        /// </summary>
        public void RemoveLowDensityVertices(float quantile)
        {
            ThrowIfInvalid();
            int result = NativePluginBindings.RemoveLowDensityVertices(_handle, quantile);
            if (result != 0)
                Debug.LogWarning("Failed to remove low density vertices.");
        }

        /// <summary>
        /// 메쉬 단순화
        /// </summary>
        public void Simplify(int targetTriangleCount)
        {
            ThrowIfInvalid();
            int result = NativePluginBindings.SimplifyMesh(_handle, targetTriangleCount);
            if (result != 0)
                throw new InvalidOperationException("Failed to simplify mesh.");
        }

        #endregion

        #region Data Extraction

        /// <summary>
        /// Unity Mesh로 변환
        /// </summary>
        public UnityEngine.Mesh ToUnityMesh()
        {
            ThrowIfInvalid();

            int vertCount = VertexCount;
            int triCount = TriangleCount;

            if (vertCount == 0 || triCount == 0)
                return null;

            // 정점 데이터 가져오기
            float[] verticesFlat = new float[vertCount * 3];
            NativePluginBindings.GetMeshVertices(_handle, verticesFlat, vertCount);
            Vector3[] vertices = NativeArrayHelper.FloatArrayToVector3Array(verticesFlat);

            // 법선 데이터 가져오기
            float[] normalsFlat = new float[vertCount * 3];
            NativePluginBindings.GetMeshNormals(_handle, normalsFlat, vertCount);
            Vector3[] normals = NativeArrayHelper.FloatArrayToVector3Array(normalsFlat);

            // 삼각형 인덱스 가져오기
            int[] triangles = new int[triCount * 3];
            NativePluginBindings.GetMeshTriangles(_handle, triangles, triCount);

            // Unity Mesh 생성
            var unityMesh = new UnityEngine.Mesh();
            
            // 대규모 메쉬 지원
            if (vertCount > 65535)
                unityMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            unityMesh.vertices = vertices;
            unityMesh.normals = normals;
            unityMesh.triangles = triangles;
            unityMesh.RecalculateBounds();

            return unityMesh;
        }

        /// <summary>
        /// MeshData 구조체로 반환
        /// </summary>
        public MeshData GetMeshData()
        {
            ThrowIfInvalid();

            var data = new MeshData();
            int vertCount = VertexCount;
            int triCount = TriangleCount;

            // 정점
            float[] verticesFlat = new float[vertCount * 3];
            NativePluginBindings.GetMeshVertices(_handle, verticesFlat, vertCount);
            data.Vertices = NativeArrayHelper.FloatArrayToVector3Array(verticesFlat);

            // 법선
            float[] normalsFlat = new float[vertCount * 3];
            NativePluginBindings.GetMeshNormals(_handle, normalsFlat, vertCount);
            data.Normals = NativeArrayHelper.FloatArrayToVector3Array(normalsFlat);

            // 삼각형
            data.Triangles = new int[triCount * 3];
            NativePluginBindings.GetMeshTriangles(_handle, data.Triangles, triCount);

            return data;
        }

        #endregion

        #region File I/O

        /// <summary>
        /// PLY 파일로 저장
        /// </summary>
        public void SaveToPLY(string filePath)
        {
            ThrowIfInvalid();
            int result = NativePluginBindings.SaveMeshPLY(_handle, filePath);
            if (result != 0)
                throw new InvalidOperationException($"Failed to save mesh: {filePath}");
        }

        #endregion

        #region Native Resource

        protected override void ReleaseNativeResource()
        {
            NativePluginBindings.DestroyMesh(_handle);
        }

        #endregion
    }

    /// <summary>
    /// 메쉬 데이터 컨테이너
    /// </summary>
    [Serializable]
    public class MeshData
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public int[] Triangles;

        public int VertexCount => Vertices?.Length ?? 0;
        public int TriangleCount => (Triangles?.Length ?? 0) / 3;
    }
}
```

---

## 8. RobotModel.cs - 로봇 모델

```csharp
using System;
using UnityEngine;
using SMRWelding.Native;

namespace SMRWelding.Robot
{
    /// <summary>
    /// 로봇 타입 열거형
    /// </summary>
    public enum RobotType
    {
        UR5 = 0,
        UR10 = 1,
        KUKA_KR6_R700 = 2,
        DOOSAN_M1013 = 3,
        Custom = 99
    }

    /// <summary>
    /// 조인트 상태
    /// </summary>
    [Serializable]
    public struct JointState
    {
        public float[] Angles;

        public JointState(int jointCount = 6)
        {
            Angles = new float[jointCount];
        }

        public JointState(float[] angles)
        {
            Angles = angles ?? new float[6];
        }

        public float this[int index]
        {
            get => Angles[index];
            set => Angles[index] = value;
        }

        public int Count => Angles?.Length ?? 0;

        public static JointState Zero => new JointState(new float[6]);
    }

    /// <summary>
    /// 로봇 모델 (역기구학 래퍼)
    /// </summary>
    public class RobotModel : NativeHandle
    {
        #region Fields

        private RobotType _robotType;
        private int _jointCount = 6;

        #endregion

        #region Properties

        /// <summary>
        /// 로봇 타입
        /// </summary>
        public RobotType Type => _robotType;

        /// <summary>
        /// 조인트 개수
        /// </summary>
        public int JointCount => _jointCount;

        #endregion

        #region Constructors

        /// <summary>
        /// 프리셋 로봇 생성
        /// </summary>
        public RobotModel(RobotType type)
        {
            _robotType = type;
            _handle = NativePluginBindings.CreateRobot((int)type);

            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException($"Failed to create robot: {type}");
        }

        /// <summary>
        /// 커스텀 DH 파라미터로 생성
        /// </summary>
        public RobotModel(DHParams[] dhParams)
        {
            if (dhParams == null || dhParams.Length == 0)
                throw new ArgumentException("DH parameters required.");

            _robotType = RobotType.Custom;
            _jointCount = dhParams.Length;
            _handle = NativePluginBindings.CreateRobotCustom(dhParams);

            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create custom robot.");
        }

        #endregion

        #region Forward Kinematics

        /// <summary>
        /// 순기구학: 조인트 각도 → TCP 변환행렬
        /// </summary>
        public Matrix4x4 ForwardKinematics(JointState joints)
        {
            ThrowIfInvalid();

            float[] transform = new float[16];
            int result = NativePluginBindings.ForwardKinematics(
                _handle, joints.Angles, transform);

            if (result != 0)
                throw new InvalidOperationException("Forward kinematics failed.");

            return NativeArrayHelper.FloatArrayToMatrix4x4(transform);
        }

        /// <summary>
        /// 순기구학: TCP 위치만 반환
        /// </summary>
        public Vector3 GetTCPPosition(JointState joints)
        {
            Matrix4x4 transform = ForwardKinematics(joints);
            return transform.GetColumn(3);
        }

        #endregion

        #region Inverse Kinematics

        /// <summary>
        /// 역기구학: 모든 해 반환
        /// </summary>
        public JointState[] InverseKinematics(Matrix4x4 targetPose)
        {
            ThrowIfInvalid();

            float[] transform = NativeArrayHelper.Matrix4x4ToFloatArray(targetPose);
            float[] solutions = new float[8 * 6]; // 최대 8개 해
            int solutionCount;

            int result = NativePluginBindings.InverseKinematics(
                _handle, transform, solutions, out solutionCount);

            if (result != 0 || solutionCount == 0)
                return Array.Empty<JointState>();

            JointState[] jointStates = new JointState[solutionCount];
            for (int i = 0; i < solutionCount; i++)
            {
                jointStates[i] = new JointState();
                jointStates[i].Angles = new float[6];
                Array.Copy(solutions, i * 6, jointStates[i].Angles, 0, 6);
            }

            return jointStates;
        }

        /// <summary>
        /// 역기구학: 현재 조인트에 가장 가까운 해
        /// </summary>
        public JointState? InverseKinematicsNearest(
            Matrix4x4 targetPose, 
            JointState currentJoints)
        {
            ThrowIfInvalid();

            float[] transform = NativeArrayHelper.Matrix4x4ToFloatArray(targetPose);
            float[] outJoints = new float[6];

            int result = NativePluginBindings.InverseKinematicsNearest(
                _handle, transform, currentJoints.Angles, outJoints);

            if (result != 0)
                return null;

            return new JointState(outJoints);
        }

        /// <summary>
        /// 수치적 역기구학
        /// </summary>
        public JointState? InverseKinematicsNumerical(
            Matrix4x4 targetPose,
            JointState initialGuess,
            int maxIterations = 100,
            float tolerance = 1e-6f)
        {
            ThrowIfInvalid();

            float[] transform = NativeArrayHelper.Matrix4x4ToFloatArray(targetPose);
            float[] outJoints = new float[6];

            int result = NativePluginBindings.InverseKinematicsNumerical(
                _handle, transform, initialGuess.Angles, outJoints,
                maxIterations, tolerance);

            if (result != 0)
                return null;

            return new JointState(outJoints);
        }

        #endregion

        #region Analysis

        /// <summary>
        /// 야코비안 계산
        /// </summary>
        public float[,] ComputeJacobian(JointState joints)
        {
            ThrowIfInvalid();

            float[] jacobianFlat = new float[36];
            int result = NativePluginBindings.ComputeJacobian(
                _handle, joints.Angles, jacobianFlat);

            if (result != 0)
                throw new InvalidOperationException("Jacobian computation failed.");

            // 1D → 2D 변환
            float[,] jacobian = new float[6, 6];
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 6; j++)
                    jacobian[i, j] = jacobianFlat[i * 6 + j];

            return jacobian;
        }

        /// <summary>
        /// 조작성 지수 (0~1)
        /// </summary>
        public float GetManipulability(JointState joints)
        {
            ThrowIfInvalid();
            return NativePluginBindings.ComputeManipulability(_handle, joints.Angles);
        }

        /// <summary>
        /// 특이점 근접도 (0에 가까울수록 특이점)
        /// </summary>
        public float GetSingularityMeasure(JointState joints)
        {
            ThrowIfInvalid();
            return NativePluginBindings.ComputeSingularityMeasure(_handle, joints.Angles);
        }

        /// <summary>
        /// 조인트 한계 검사
        /// </summary>
        public bool CheckJointLimits(JointState joints)
        {
            ThrowIfInvalid();
            return NativePluginBindings.CheckJointLimits(_handle, joints.Angles) == 0;
        }

        #endregion

        #region Native Resource

        protected override void ReleaseNativeResource()
        {
            NativePluginBindings.DestroyRobot(_handle);
        }

        #endregion
    }
}
```

---

*Part 3에서 계속: WeldPathPlanner, 사용 예제, 비동기 처리*
