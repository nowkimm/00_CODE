# SMR 용접 로봇 메쉬 생성 시스템

포인트 클라우드에서 메쉬를 생성하고 SMR(Small Modular Reactor) 용접 로봇 경로를 계획하는 Unity 기반 시스템입니다.

## 개요

이 프로젝트는 3D 스캔 데이터(포인트 클라우드)를 처리하여:
1. 노이즈 제거 및 다운샘플링
2. 표면 메쉬 재구성
3. 용접 경로 자동 생성
4. 로봇 궤적 계산 (역기구학)

## 시스템 아키텍처

```
┌─────────────────────────────────────────────────────────────┐
│                    Unity C# Application                      │
├─────────────────────────────────────────────────────────────┤
│  WeldingPipelineController  │  Visualizers  │  UI           │
├─────────────────────────────────────────────────────────────┤
│                    WeldingPipeline (Core)                    │
├─────────────────────────────────────────────────────────────┤
│  PointCloudWrapper │ MeshWrapper │ RobotWrapper │ PathWrapper│
├─────────────────────────────────────────────────────────────┤
│                    P/Invoke (NativeBindings)                 │
├─────────────────────────────────────────────────────────────┤
│                    C++ Native DLL                            │
│  point_cloud.cpp │ mesh_generator.cpp │ robot_kinematics.cpp │
└─────────────────────────────────────────────────────────────┘
```

## 요구 사항

### 빌드 요구 사항
- Visual Studio 2022 (MSVC v143)
- CMake 3.20+
- vcpkg (패키지 관리자)
- Unity 2022.3 LTS 이상

### 라이브러리 의존성
- Open3D 0.17+
- Eigen 3.4+
- (선택) CUDA 11.0+ (GPU 가속)

## 설치 방법

### 1. 저장소 클론
```bash
git clone https://github.com/your-org/smr-welding.git
cd smr-welding
```

### 2. vcpkg로 의존성 설치
```bash
# vcpkg 설치 (아직 없다면)
git clone https://github.com/microsoft/vcpkg.git C:\vcpkg
cd C:\vcpkg
.\bootstrap-vcpkg.bat

# 의존성 설치
vcpkg install open3d:x64-windows eigen3:x64-windows
```

### 3. 네이티브 플러그인 빌드
```powershell
# PowerShell에서
.\build_native.ps1 -BuildType Release

# 또는 배치 파일로
.\build_native.bat
```

### 4. Unity 프로젝트 열기
1. Unity Hub에서 `Unity/SMRWelding` 폴더 열기
2. 프로젝트 로드 후 `Assets/Scenes/Main.unity` 열기

## 사용 방법

### 기본 워크플로우

```csharp
// 1. 파이프라인 컨트롤러 참조
var controller = GetComponent<WeldingPipelineController>();

// 2. 포인트 클라우드 파일에서 실행
controller.RunFromFile("path/to/scan.ply");

// 3. 또는 코드에서 포인트 생성
Vector3[] points = GeneratePoints();
controller.RunFromPoints(points);

// 4. 이벤트 구독
controller.OnPipelineComplete.AddListener(() => {
    Debug.Log("Pipeline complete!");
    Mesh mesh = controller.GeneratedMesh;
    Vector3[] path = controller.PathPositions;
});
```

### 설정 커스터마이징

```csharp
// Inspector에서 또는 코드로 설정
controller.voxelSize = 0.002f;      // 다운샘플링 해상도
controller.poissonDepth = 8;         // 메쉬 품질 (8-12)
controller.pathStepSize = 0.005f;    // 경로 포인트 간격
controller.robotType = RobotType.UR5;
```

## 프로젝트 구조

```
point_to_mesh/
├── Docs/                           # 문서
│   ├── Research/                   # 연구 자료
│   ├── Design/                     # 설계 문서
│   └── Guides/                     # 사용 가이드
├── Native/                         # C++ 네이티브 플러그인
│   ├── include/
│   │   └── smr_welding_api.h       # API 헤더
│   ├── src/
│   │   ├── point_cloud.cpp         # 포인트 클라우드 처리
│   │   ├── mesh_generator.cpp      # 메쉬 생성
│   │   ├── robot_kinematics.cpp    # 로봇 역기구학
│   │   └── path_planner.cpp        # 경로 계획
│   └── CMakeLists.txt
├── Unity/SMRWelding/
│   └── Assets/
│       ├── Plugins/x86_64/         # DLL 위치
│       └── Scripts/
│           ├── Native/             # P/Invoke 래퍼
│           ├── Core/               # 파이프라인 코어
│           ├── Components/         # Unity 컴포넌트
│           ├── UI/                 # UI 컨트롤러
│           └── Tests/              # 테스트
├── build_native.bat                # 빌드 스크립트 (CMD)
├── build_native.ps1                # 빌드 스크립트 (PowerShell)
└── README.md
```

## API 참조

### WeldingPipeline

메인 파이프라인 클래스입니다.

| 메서드 | 설명 |
|--------|------|
| `RunFromFile(path)` | PLY/PCD 파일에서 파이프라인 실행 |
| `RunFromPoints(points)` | Vector3 배열에서 파이프라인 실행 |
| `Dispose()` | 리소스 해제 |

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `GeneratedMesh` | `Mesh` | 생성된 Unity 메쉬 |
| `PathPositions` | `Vector3[]` | 용접 경로 포인트 |
| `JointTrajectory` | `double[][]` | 로봇 조인트 궤적 |
| `Reachability` | `bool[]` | 각 포인트 도달 가능 여부 |

### 지원 로봇

| 로봇 | 작업 반경 | DOF |
|------|----------|-----|
| UR5 | 850mm | 6 |
| UR10 | 1300mm | 6 |
| KUKA KR6 R700 | 706mm | 6 |
| Doosan M1013 | 1300mm | 6 |

### 위빙 패턴

| 패턴 | 설명 |
|------|------|
| None | 직선 경로 |
| Zigzag | 지그재그 패턴 |
| Circular | 원형 패턴 |
| Triangle | 삼각형 패턴 |
| Figure8 | 8자 패턴 |

## 성능 지표

| 단계 | 10만 포인트 | 100만 포인트 |
|------|------------|--------------|
| 다운샘플링 | ~50ms | ~500ms |
| 노말 추정 | ~100ms | ~1s |
| 메쉬 생성 | ~200ms | ~2s |
| 경로 생성 | ~50ms | ~100ms |
| 역기구학 | ~10ms | ~100ms |

## 라이선스

MIT License

## 기여

버그 리포트나 기능 제안은 GitHub Issues를 이용해주세요.
