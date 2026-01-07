# Unity C# Wrapper 설계 - Part 1

## 1. 개요

C++ 네이티브 플러그인을 Unity에서 사용하기 위한 C# 래퍼 클래스 설계입니다.

### 1.1 파일 구조
```
Scripts/
├── Native/
│   ├── NativePluginBindings.cs    # P/Invoke 선언
│   ├── NativeHandle.cs            # 핸들 관리 기본 클래스
│   └── NativeArrayHelper.cs       # 배열 마샬링 헬퍼
├── PointCloud/
│   ├── PointCloudData.cs          # 포인트 클라우드 데이터
│   └── PointCloudProcessor.cs     # 포인트 클라우드 처리
├── Mesh/
│   ├── MeshGenerator.cs           # 메쉬 생성기
│   └── MeshData.cs                # 메쉬 데이터
├── Robot/
│   ├── RobotModel.cs              # 로봇 모델
│   ├── InverseKinematics.cs       # IK 래퍼
│   └── JointState.cs              # 조인트 상태
└── Path/
    ├── WeldPathPlanner.cs         # 용접 경로 계획
    └── WeldPoint.cs               # 용접 포인트
```

---

## 2. NativePluginBindings.cs - P/Invoke 선언

```csharp
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace SMRWelding.Native
{
    /// <summary>
    /// C++ 네이티브 플러그인 P/Invoke 바인딩
    /// </summary>
    public static class NativePluginBindings
    {
        #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        private const string DLL_NAME = "SMRWeldingPlugin";
        #elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        private const string DLL_NAME = "libSMRWeldingPlugin";
        #else
        private const string DLL_NAME = "SMRWeldingPlugin";
        #endif

        #region Point Cloud API
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreatePointCloud();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyPointCloud(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int LoadPointCloudPLY(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string filepath);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int LoadPointCloudPCD(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string filepath);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetPointCloudPoints(
            IntPtr handle,
            [In] float[] points,
            int count);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SetPointCloudColors(
            IntPtr handle,
            [In] float[] colors,
            int count);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetPointCloudCount(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetPointCloudPoints(
            IntPtr handle,
            [Out] float[] outPoints,
            int maxCount);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetPointCloudNormals(
            IntPtr handle,
            [Out] float[] outNormals,
            int maxCount);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int EstimateNormalsKNN(IntPtr handle, int k);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int EstimateNormalsRadius(IntPtr handle, float radius);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int OrientNormalsConsistent(
            IntPtr handle,
            int k,
            [In] float[] referencePoint);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int DownsampleVoxel(IntPtr handle, float voxelSize);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int RemoveStatisticalOutliers(
            IntPtr handle,
            int nbNeighbors,
            float stdRatio);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int HasNormals(IntPtr handle);

        #endregion

        #region Mesh Generation API

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreatePoissonMesh(
            IntPtr pointCloudHandle,
            int depth,
            float scale,
            int linearFit);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyMesh(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetMeshVertexCount(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetMeshTriangleCount(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetMeshVertices(
            IntPtr handle,
            [Out] float[] outVertices,
            int maxCount);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetMeshNormals(
            IntPtr handle,
            [Out] float[] outNormals,
            int maxCount);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetMeshTriangles(
            IntPtr handle,
            [Out] int[] outTriangles,
            int maxCount);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int RemoveLowDensityVertices(
            IntPtr handle,
            float quantile);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SimplifyMesh(
            IntPtr handle,
            int targetTriangleCount);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SaveMeshPLY(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string filepath);

        #endregion

        #region Robot IK API

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateRobot(int robotType);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreateRobotCustom([In] DHParams[] dhParams);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyRobot(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ForwardKinematics(
            IntPtr handle,
            [In] float[] joints,
            [Out] float[] outTransform);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int InverseKinematics(
            IntPtr handle,
            [In] float[] transform,
            [Out] float[] outSolutions,
            out int solutionCount);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int InverseKinematicsNearest(
            IntPtr handle,
            [In] float[] transform,
            [In] float[] currentJoints,
            [Out] float[] outJoints);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int InverseKinematicsNumerical(
            IntPtr handle,
            [In] float[] transform,
            [In] float[] initialGuess,
            [Out] float[] outJoints,
            int maxIterations,
            float tolerance);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ComputeJacobian(
            IntPtr handle,
            [In] float[] joints,
            [Out] float[] outJacobian);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern float ComputeManipulability(
            IntPtr handle,
            [In] float[] joints);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern float ComputeSingularityMeasure(
            IntPtr handle,
            [In] float[] joints);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int CheckJointLimits(
            IntPtr handle,
            [In] float[] joints);

        #endregion

        #region Path Planning API

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr CreatePathFromPoints(
            [In] WeldPointNative[] points,
            int count);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ExtractWeldPathFromEdge(
            IntPtr meshHandle,
            [In] int[] edgeVertexIndices,
            int edgeCount);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void DestroyPath(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetPathPointCount(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern float GetPathTotalLength(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetPathPoints(
            IntPtr handle,
            [Out] WeldPointNative[] outPoints,
            int maxCount);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ConvertPathToJoints(
            IntPtr pathHandle,
            IntPtr robotHandle,
            [In] ref PathParamsNative pathParams,
            [In] float[] initialJoints,
            [Out] float[] outJointPath,
            int maxPoints);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ApplyWeavePattern(
            IntPtr handle,
            int weaveType,
            float width,
            float frequency);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ResamplePath(IntPtr handle, float stepSize);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int SmoothPath(
            IntPtr handle,
            int windowSize,
            int iterations);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern float CheckPathReachability(
            IntPtr pathHandle,
            IntPtr robotHandle,
            [In] ref PathParamsNative pathParams);

        #endregion
    }

    #region Native Structures

    /// <summary>
    /// DH 파라미터 (네이티브 구조체)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DHParams
    {
        public float d;
        public float a;
        public float alpha;
        public float q_home;
        public float q_min;
        public float q_max;
    }

    /// <summary>
    /// 용접 포인트 (네이티브 구조체)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WeldPointNative
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] position;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] normal;
        
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] tangent;
        
        public float arcLength;
    }

    /// <summary>
    /// 경로 파라미터 (네이티브 구조체)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PathParamsNative
    {
        public float stepSize;
        public float standoffDistance;
        public float approachAngle;
        public float travelAngle;
        public int weaveType;
        public float weaveWidth;
        public float weaveFrequency;
    }

    #endregion
}
```

---

## 3. NativeHandle.cs - 핸들 관리 기본 클래스

```csharp
using System;

namespace SMRWelding.Native
{
    /// <summary>
    /// 네이티브 핸들을 관리하는 기본 클래스 (IDisposable 패턴)
    /// </summary>
    public abstract class NativeHandle : IDisposable
    {
        protected IntPtr _handle = IntPtr.Zero;
        private bool _disposed = false;

        /// <summary>
        /// 네이티브 핸들
        /// </summary>
        public IntPtr Handle => _handle;

        /// <summary>
        /// 유효한 핸들인지 확인
        /// </summary>
        public bool IsValid => _handle != IntPtr.Zero;

        /// <summary>
        /// Dispose 완료 여부
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// 파생 클래스에서 구현: 네이티브 리소스 해제
        /// </summary>
        protected abstract void ReleaseNativeResource();

        /// <summary>
        /// 핸들 유효성 검사 (예외 발생)
        /// </summary>
        protected void ThrowIfInvalid()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);
            if (_handle == IntPtr.Zero)
                throw new InvalidOperationException("Native handle is not initialized.");
        }

        /// <summary>
        /// IDisposable 구현
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_handle != IntPtr.Zero)
                {
                    ReleaseNativeResource();
                    _handle = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

        ~NativeHandle()
        {
            Dispose(false);
        }
    }
}
```

---

## 4. NativeArrayHelper.cs - 배열 마샬링 헬퍼

```csharp
using System;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace SMRWelding.Native
{
    /// <summary>
    /// 네이티브 배열 변환 헬퍼
    /// </summary>
    public static class NativeArrayHelper
    {
        /// <summary>
        /// Vector3 배열을 float[] 배열로 변환
        /// </summary>
        public static float[] Vector3ArrayToFloatArray(Vector3[] vectors)
        {
            if (vectors == null || vectors.Length == 0)
                return Array.Empty<float>();

            float[] result = new float[vectors.Length * 3];
            for (int i = 0; i < vectors.Length; i++)
            {
                result[i * 3 + 0] = vectors[i].x;
                result[i * 3 + 1] = vectors[i].y;
                result[i * 3 + 2] = vectors[i].z;
            }
            return result;
        }

        /// <summary>
        /// float[] 배열을 Vector3 배열로 변환
        /// </summary>
        public static Vector3[] FloatArrayToVector3Array(float[] floats)
        {
            if (floats == null || floats.Length == 0)
                return Array.Empty<Vector3>();

            int count = floats.Length / 3;
            Vector3[] result = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = new Vector3(
                    floats[i * 3 + 0],
                    floats[i * 3 + 1],
                    floats[i * 3 + 2]);
            }
            return result;
        }

        /// <summary>
        /// Color 배열을 float[] 배열로 변환 (RGB만)
        /// </summary>
        public static float[] ColorArrayToFloatArray(Color[] colors)
        {
            if (colors == null || colors.Length == 0)
                return Array.Empty<float>();

            float[] result = new float[colors.Length * 3];
            for (int i = 0; i < colors.Length; i++)
            {
                result[i * 3 + 0] = colors[i].r;
                result[i * 3 + 1] = colors[i].g;
                result[i * 3 + 2] = colors[i].b;
            }
            return result;
        }

        /// <summary>
        /// float[] 배열을 Color 배열로 변환
        /// </summary>
        public static Color[] FloatArrayToColorArray(float[] floats)
        {
            if (floats == null || floats.Length == 0)
                return Array.Empty<Color>();

            int count = floats.Length / 3;
            Color[] result = new Color[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = new Color(
                    floats[i * 3 + 0],
                    floats[i * 3 + 1],
                    floats[i * 3 + 2],
                    1.0f);
            }
            return result;
        }

        /// <summary>
        /// 4x4 변환 행렬을 Unity Matrix4x4로 변환
        /// </summary>
        public static Matrix4x4 FloatArrayToMatrix4x4(float[] matrix)
        {
            if (matrix == null || matrix.Length < 16)
                return Matrix4x4.identity;

            // Row-major에서 Unity의 Column-major로 변환
            Matrix4x4 result = new Matrix4x4();
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    result[row, col] = matrix[row * 4 + col];
                }
            }
            return result;
        }

        /// <summary>
        /// Unity Matrix4x4를 float[] 배열로 변환
        /// </summary>
        public static float[] Matrix4x4ToFloatArray(Matrix4x4 matrix)
        {
            float[] result = new float[16];
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    result[row * 4 + col] = matrix[row, col];
                }
            }
            return result;
        }

        /// <summary>
        /// NativeArray를 관리 배열로 복사
        /// </summary>
        public static T[] ToManagedArray<T>(NativeArray<T> nativeArray) where T : struct
        {
            T[] result = new T[nativeArray.Length];
            nativeArray.CopyTo(result);
            return result;
        }

        /// <summary>
        /// 관리 배열에서 NativeArray 생성
        /// </summary>
        public static NativeArray<T> ToNativeArray<T>(
            T[] managedArray, 
            Allocator allocator = Allocator.TempJob) where T : struct
        {
            NativeArray<T> nativeArray = new NativeArray<T>(
                managedArray.Length, allocator, NativeArrayOptions.UninitializedMemory);
            nativeArray.CopyFrom(managedArray);
            return nativeArray;
        }
    }
}
```

---

## 5. PointCloudData.cs - 포인트 클라우드 데이터

```csharp
using System;
using UnityEngine;

namespace SMRWelding.PointCloud
{
    /// <summary>
    /// 포인트 클라우드 데이터 컨테이너
    /// </summary>
    [Serializable]
    public class PointCloudData
    {
        /// <summary>
        /// 포인트 위치 배열
        /// </summary>
        public Vector3[] Points { get; set; }

        /// <summary>
        /// 법선 벡터 배열 (nullable)
        /// </summary>
        public Vector3[] Normals { get; set; }

        /// <summary>
        /// 색상 배열 (nullable)
        /// </summary>
        public Color[] Colors { get; set; }

        /// <summary>
        /// 포인트 개수
        /// </summary>
        public int Count => Points?.Length ?? 0;

        /// <summary>
        /// 법선 데이터 존재 여부
        /// </summary>
        public bool HasNormals => Normals != null && Normals.Length == Count;

        /// <summary>
        /// 색상 데이터 존재 여부
        /// </summary>
        public bool HasColors => Colors != null && Colors.Length == Count;

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public PointCloudData()
        {
            Points = Array.Empty<Vector3>();
        }

        /// <summary>
        /// 포인트 배열로 초기화
        /// </summary>
        public PointCloudData(Vector3[] points)
        {
            Points = points ?? Array.Empty<Vector3>();
        }

        /// <summary>
        /// 바운딩 박스 계산
        /// </summary>
        public Bounds CalculateBounds()
        {
            if (Count == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            Vector3 min = Points[0];
            Vector3 max = Points[0];

            for (int i = 1; i < Count; i++)
            {
                min = Vector3.Min(min, Points[i]);
                max = Vector3.Max(max, Points[i]);
            }

            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;
            return new Bounds(center, size);
        }

        /// <summary>
        /// 중심점 계산
        /// </summary>
        public Vector3 CalculateCentroid()
        {
            if (Count == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < Count; i++)
            {
                sum += Points[i];
            }
            return sum / Count;
        }

        /// <summary>
        /// 깊은 복사
        /// </summary>
        public PointCloudData Clone()
        {
            var clone = new PointCloudData();
            
            if (Points != null)
            {
                clone.Points = new Vector3[Points.Length];
                Array.Copy(Points, clone.Points, Points.Length);
            }

            if (Normals != null)
            {
                clone.Normals = new Vector3[Normals.Length];
                Array.Copy(Normals, clone.Normals, Normals.Length);
            }

            if (Colors != null)
            {
                clone.Colors = new Color[Colors.Length];
                Array.Copy(Colors, clone.Colors, Colors.Length);
            }

            return clone;
        }
    }
}
```

---

*Part 2에서 계속: PointCloudProcessor, MeshGenerator, RobotModel, WeldPathPlanner 클래스*
