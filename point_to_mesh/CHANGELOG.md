# Changelog

모든 주요 변경사항이 이 파일에 기록됩니다.

## [1.0.0] - 2026-01-07

### 🎉 최초 릴리스

#### C++ 네이티브 플러그인
- **smr_welding_api.h**: 50개 API 함수 정의 (526줄)
- **point_cloud.cpp**: PLY/PCD 로드, 다운샘플링, 노멀 계산 (517줄)
- **mesh_generator.cpp**: Poisson Reconstruction, 메쉬 단순화 (476줄)
- **robot_kinematics.cpp**: FK/IK, Jacobian, 조작성 (442줄)
- **path_planner.cpp**: 경로 생성, 위빙 패턴, 리샘플링 (433줄)
- **CMakeLists.txt**: vcpkg 기반 빌드 설정 (176줄)

#### Unity C# 코드 (28개 파일)
- **Native/**: P/Invoke 래퍼 6개 (NativeTypes, NativeBindings, Wrappers)
- **Core/**: WeldingPipeline, SimulationMode
- **데이터 모델**: PointCloudData, MeshData, WeldPath, RobotModel
- **Components/**: Controller, PathVisualizer, RobotVisualizer, PointCloudVisualizer
- **UI/**: WeldingUI (IMGUI 기반)
- **Editor/**: SceneSetup, SampleDataWindow, DemoSceneSetup, ConfigEditor
- **Utilities/**: FileUtilities, MathUtilities, SampleDataGenerator, ConfigManager, ResultExporter
- **Tests/**: PipelineTests (Play Mode)

#### Unity 프로젝트 설정
- Unity 2021.3.0f1 LTS 타겟
- allowUnsafeCode 활성화 (P/Invoke 지원)
- 커스텀 태그: PointCloud, WeldPath, Robot, Workpiece
- 커스텀 레이어: 8~11번
- 기본 씬: WeldingDemo.unity
- 기본 머티리얼: PointCloud.mat, WeldPath.mat

#### 샘플 데이터
- **pipe_seam.ply**: 파이프 용접 심 (원통형)
- **t_joint_seam.ply**: T-조인트 용접 심

#### 문서
- 연구 문서 4개 (Open3D, P/Invoke, IK, 경로계획)
- 설계 문서 7개 (아키텍처, C++ API, C# Wrapper)
- 가이드 5개 (샘플씬, 성능, 테스트, 로드맵, 빌드환경)

### 기술 사양
- **C++ API**: 50개 함수
- **의존성**: Open3D 0.18+, Eigen 3.4+
- **Unity**: 2021.3 LTS+
- **빌드**: Visual Studio 2022, CMake 3.20+, vcpkg

### 로봇 프리셋
- UR5 (6-DOF, 850mm 도달)
- UR10 (6-DOF, 1300mm 도달)
- KUKA KR6 R700 (6-DOF, 706mm 도달)
- Doosan M1013 (6-DOF, 1300mm 도달)
- Custom (사용자 정의 DH 파라미터)

### 위빙 패턴
- None (직선)
- Zigzag (지그재그)
- Circular (원형)
- Triangle (삼각형)
- Figure8 (8자형)

---

## [1.1.0] - 예정

### 계획된 기능
- 실시간 로봇 통신 (TCP/IP)
- 멀티스레드 처리 최적화
- GPU 가속 포인트 클라우드 렌더링
- 경로 충돌 감지

---

## [1.2.0] - 예정

### 계획된 기능
- ROS2 통합
- 실제 로봇 컨트롤러 인터페이스
- 용접 파라미터 최적화
- 품질 예측 시스템

---

## 파일 통계

| 카테고리 | 파일 수 | 코드 줄 수 |
|----------|---------|------------|
| C++ 코드 | 6 | 2,570 |
| C# 코드 | 28 | ~7,640 |
| 빌드 스크립트 | 2 | ~150 |
| 문서 | 17 | ~3,500 |
| Unity 설정 | 11 | - |
| 샘플/리소스 | 6 | - |
| **총계** | **70** | **~13,860** |

---

*SMR Welding System v1.0.0 - 2026-01-07*
