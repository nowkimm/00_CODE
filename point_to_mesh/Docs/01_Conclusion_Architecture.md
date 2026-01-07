# 결론 및 시스템 아키텍처

## 1. 연구 결과 종합

### 1.1 핵심 기술 스택 선정

| 구성요소 | 선정 기술 | 근거 |
|----------|-----------|------|
| 메쉬 생성 | Open3D Poisson | 노이즈 강건성, watertight 결과, 용접 품질 적합 |
| Unity 통합 | P/Invoke + 핸들 패턴 | 대용량 데이터 zero-copy, IL2CPP 호환 |
| 역기구학 | 해석적 IK (Closed-form) | 실시간 성능 (<0.1ms), 결정적 결과 |
| 경로 계획 | 메쉬 에지 기반 + 위빙 | 자동 용접선 추출, 산업 표준 패턴 |
| 로봇 통신 | UR RTDE / Doosan Real-time | Unity 통합 우수, 시뮬레이터 제공 |

### 1.2 권장 파라미터

**Open3D Poisson:**
```python
depth = 9         # 용접 품질 (8-10)
scale = 1.2       # 경계 확장
linear_fit = True # 정확한 표면
```

**역기구학:**
- 방법: 구형 손목 분리법 또는 UR 해석적 IK
- 댐핑: λ = 0.01~0.05 (특이점 근처)
- 해 선택: 현재 형상 유지 + 관절 한계 우선

**경로 계획:**
- 용접선: Dihedral angle > 30° 에지
- 작업각: 15° (기본)
- 진행각: 10° (push 방향)

---

## 2. 시스템 아키텍처

### 2.1 전체 아키텍처

```
┌─────────────────────────────────────────────────────────────────┐
│                         Unity Application                        │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │ Point Cloud │  │ Mesh Render │  │ Robot Viz   │              │
│  │ Importer    │  │ Component   │  │ Component   │              │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘              │
│         │                │                │                      │
│  ┌──────▼────────────────▼────────────────▼──────┐              │
│  │              C# Wrapper Layer                  │              │
│  │  - MeshGenerator.cs                            │              │
│  │  - RobotController.cs                          │              │
│  │  - PathPlanner.cs                              │              │
│  └──────────────────────┬────────────────────────┘              │
│                         │ P/Invoke                               │
├─────────────────────────┼────────────────────────────────────────┤
│  ┌──────────────────────▼────────────────────────┐              │
│  │           Native Plugin (C++/DLL)              │              │
│  │  ┌─────────────┐  ┌─────────────┐             │              │
│  │  │ Open3D Core │  │ Robot IK    │             │              │
│  │  │ - Poisson   │  │ - Analytical│             │              │
│  │  │ - Normals   │  │ - Jacobian  │             │              │
│  │  └─────────────┘  └─────────────┘             │              │
│  └───────────────────────────────────────────────┘              │
└─────────────────────────────────────────────────────────────────┘
                              │
                              │ TCP/UDP
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Robot Controller                              │
│  - KUKA RSI (4ms, UDP/XML)                                      │
│  - UR RTDE (2ms, TCP/Binary)                                    │
│  - Doosan Real-time (1ms, UDP)                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 데이터 흐름

```
1. 포인트클라우드 입력
   PLY/PCD File → Unity → Native Plugin
   
2. 메쉬 생성
   Native: PointCloud → Normal Estimation → Poisson → Triangle Mesh
   
3. Unity 렌더링
   Native Mesh Handle → C# Copy → Unity Mesh → GPU
   
4. 경로 추출
   Mesh Edges → Weld Seam Detection → Path Waypoints
   
5. IK 계산
   Waypoint Poses → Inverse Kinematics → Joint Angles
   
6. 로봇 전송
   Joint Trajectory → Robot Protocol → Real Robot
```

---

## 3. 모듈 설계

### 3.1 Native Plugin API

```cpp
// ========== 핸들 기반 API ==========

// 포인트클라우드
extern "C" {
    EXPORT_API PointCloudHandle CreatePointCloud();
    EXPORT_API void DestroyPointCloud(PointCloudHandle handle);
    EXPORT_API int LoadPointCloudPLY(PointCloudHandle handle, const char* path);
    EXPORT_API int SetPointCloudData(PointCloudHandle handle, 
                                      const float* points, int count);
    EXPORT_API int EstimateNormals(PointCloudHandle handle, 
                                    float radius, int max_nn);
}

// 메쉬 생성
extern "C" {
    EXPORT_API MeshHandle CreatePoissonMesh(PointCloudHandle pcd,
                                            int depth, float scale, 
                                            bool linear_fit);
    EXPORT_API void DestroyMesh(MeshHandle handle);
    EXPORT_API int GetMeshVertexCount(MeshHandle handle);
    EXPORT_API int GetMeshTriangleCount(MeshHandle handle);
    EXPORT_API void GetMeshVertices(MeshHandle handle, float* buffer);
    EXPORT_API void GetMeshNormals(MeshHandle handle, float* buffer);
    EXPORT_API void GetMeshTriangles(MeshHandle handle, int* buffer);
}

// 역기구학
extern "C" {
    EXPORT_API RobotHandle CreateRobot(RobotType type);
    EXPORT_API void DestroyRobot(RobotHandle handle);
    EXPORT_API int SolveIK(RobotHandle robot, 
                           const float* target_pose,    // [x,y,z,qx,qy,qz,qw]
                           const float* current_joints,
                           float* solution,
                           int* num_solutions);
    EXPORT_API float ComputeManipulability(RobotHandle robot, 
                                           const float* joints);
}

// 경로 계획
extern "C" {
    EXPORT_API PathHandle ExtractWeldPath(MeshHandle mesh, 
                                          float dihedral_threshold);
    EXPORT_API void DestroyPath(PathHandle handle);
    EXPORT_API int GetPathWaypointCount(PathHandle handle);
    EXPORT_API void GetPathWaypoints(PathHandle handle, float* poses);
    EXPORT_API void ApplyWeavePattern(PathHandle handle, 
                                       WeaveType type,
                                       float amplitude, float frequency);
}
```

### 3.2 C# Wrapper 구조

```csharp
namespace SMR.Welding
{
    // ========== 포인트클라우드 ==========
    public class PointCloud : IDisposable
    {
        private IntPtr _handle;
        
        public PointCloud() { _handle = Native.CreatePointCloud(); }
        public void Dispose() { Native.DestroyPointCloud(_handle); }
        
        public void LoadPLY(string path);
        public void SetPoints(Vector3[] points);
        public void EstimateNormals(float radius = 0.1f, int maxNN = 30);
    }
    
    // ========== 메쉬 생성 ==========
    public class MeshGenerator : IDisposable
    {
        public Mesh GeneratePoisson(PointCloud pcd, 
                                    int depth = 9, 
                                    float scale = 1.2f,
                                    bool linearFit = true);
    }
    
    // ========== 로봇 컨트롤러 ==========
    public class RobotController : IDisposable
    {
        public RobotType Type { get; }
        public float[] JointAngles { get; private set; }
        
        public bool SolveIK(Pose targetPose, out float[] solution);
        public float GetManipulability();
        public bool IsNearSingularity(float threshold = 0.01f);
    }
    
    // ========== 경로 계획 ==========
    public class WeldPathPlanner : IDisposable
    {
        public WeldPath ExtractFromMesh(Mesh mesh, float dihedralThreshold = 30f);
        public void ApplyWeave(WeldPath path, WeaveType type, 
                               float amplitude, float frequency);
        public JointTrajectory ComputeTrajectory(WeldPath path, 
                                                  RobotController robot);
    }
}
```

---

## 4. 구현 우선순위

### Phase 1: 메쉬 생성 (주 1-2)
1. Open3D 정적 라이브러리 빌드 (CMake)
2. C++ Plugin: PointCloud, Poisson API
3. C# Wrapper: PointCloud, MeshGenerator
4. Unity: 포인트클라우드 로더, 메쉬 렌더링

### Phase 2: 역기구학 (주 3-4)
1. C++ Plugin: Robot IK (UR 해석적)
2. C++ Plugin: Jacobian, Manipulability
3. C# Wrapper: RobotController
4. Unity: 로봇 시각화, 관절 제어

### Phase 3: 경로 계획 (주 5-6)
1. C++ Plugin: WeldPath 추출
2. C++ Plugin: 위빙 패턴
3. C# Wrapper: WeldPathPlanner
4. Unity: 경로 시각화, 시뮬레이션

### Phase 4: 로봇 통신 (주 7-8)
1. C#: UR RTDE 클라이언트
2. C#: Doosan 클라이언트
3. Unity: 실시간 제어 UI
4. 통합 테스트

---

## 5. 파일 구조

```
point_to_mesh/
├── Docs/
│   ├── 00_Research_Summary_Part1.md
│   ├── 00_Research_Summary_Part2.md
│   ├── 00_Research_Summary_Part3.md
│   ├── 00_Research_Summary_Part4.md
│   ├── 01_Conclusion_Architecture.md
│   ├── 02_Cpp_Plugin_Design_Part1.md
│   ├── 02_Cpp_Plugin_Design_Part2.md
│   ├── 03_Unity_CSharp_Wrapper.md
│   └── ...
├── NativePlugin/
│   ├── CMakeLists.txt
│   ├── include/
│   │   ├── exports.h
│   │   ├── point_cloud.h
│   │   ├── mesh_generator.h
│   │   ├── robot_ik.h
│   │   └── path_planner.h
│   └── src/
│       ├── point_cloud.cpp
│       ├── mesh_generator.cpp
│       ├── robot_ik.cpp
│       └── path_planner.cpp
├── UnityProject/
│   ├── Assets/
│   │   ├── Plugins/
│   │   │   └── x86_64/
│   │   │       └── MeshGenerator.dll
│   │   ├── Scripts/
│   │   │   ├── Native/
│   │   │   │   └── NativeBindings.cs
│   │   │   ├── MeshGeneration/
│   │   │   │   ├── PointCloud.cs
│   │   │   │   └── MeshGenerator.cs
│   │   │   ├── Robotics/
│   │   │   │   ├── RobotController.cs
│   │   │   │   └── WeldPathPlanner.cs
│   │   │   └── Communication/
│   │   │       ├── URRTDEClient.cs
│   │   │       └── DoosanClient.cs
│   │   └── Scenes/
│   │       └── WeldingSimulation.unity
│   └── ProjectSettings/
└── project_plan.md
```

---

## 6. 리스크 및 대응

| 리스크 | 영향 | 대응 방안 |
|--------|------|-----------|
| Open3D 빌드 실패 | 높음 | vcpkg 활용, 사전 빌드 바이너리 |
| IL2CPP 호환성 | 중간 | blittable types, 정적 콜백 |
| 실시간 성능 부족 | 중간 | LOD, 비동기 처리, GPU 가속 |
| 로봇 통신 지연 | 높음 | UDP 우선, 버퍼링, 예측 |
| 특이점 발생 | 중간 | Adaptive damping, 경로 재계획 |

---

## 7. 다음 단계

1. **02_Cpp_Plugin_Design**: C++ 플러그인 상세 설계 문서
2. **03_Unity_CSharp_Wrapper**: C# 래퍼 상세 설계 문서
3. **실제 구현**: NativePlugin 프로젝트 생성 및 코딩

---
*작성일: 2025-01-07*
