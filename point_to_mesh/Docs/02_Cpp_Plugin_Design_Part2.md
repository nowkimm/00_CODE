# C++ 플러그인 설계 Part 2: 로봇/경로 API 및 구현

## 4. 로봇 역기구학 API

### 4.1 robot_ik.h

```cpp
// include/robot_ik.h
#pragma once

#include "exports.h"

#ifdef __cplusplus
extern "C" {
#endif

// ===== 6x1 조인트 배열 (라디안) =====
typedef struct {
    double q[6];
} JointAngles;

// ===== 4x4 변환행렬 (row-major) =====
typedef struct {
    double m[16];  // [m00,m01,m02,m03, m10,m11,m12,m13, ...]
} TransformMatrix;

// ===== 6x6 야코비안 (row-major) =====
typedef struct {
    double j[36];
} Jacobian6x6;

// ===== 로봇 DH 파라미터 =====
typedef struct {
    double d[6];      // 링크 오프셋
    double a[6];      // 링크 길이
    double alpha[6];  // 링크 비틀림 (라디안)
    double q_home[6]; // 홈 위치
    double q_min[6];  // 조인트 하한
    double q_max[6];  // 조인트 상한
} DHParams;

// ===== 로봇 생성/해제 =====

/**
 * 사전 정의된 로봇 생성
 * @param type RobotType 열거형
 * @return 로봇 핸들
 */
EXPORT_API RobotHandle CALLING_CONVENTION 
CreateRobot(int type);

/**
 * 커스텀 DH 파라미터로 로봇 생성
 * @param params DH 파라미터 구조체
 * @return 로봇 핸들
 */
EXPORT_API RobotHandle CALLING_CONVENTION 
CreateRobotCustom(const DHParams* params);

/**
 * 로봇 해제
 */
EXPORT_API void CALLING_CONVENTION 
DestroyRobot(RobotHandle handle);

// ===== 순기구학 (FK) =====

/**
 * 순기구학: 조인트 → TCP 변환행렬
 * @param handle 로봇 핸들
 * @param joints 조인트 각도 (6개, 라디안)
 * @param outTransform 출력 4x4 행렬
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
ForwardKinematics(RobotHandle handle,
                  const JointAngles* joints,
                  TransformMatrix* outTransform);

/**
 * 특정 링크까지의 순기구학
 * @param handle 로봇 핸들
 * @param joints 조인트 각도
 * @param linkIndex 링크 인덱스 (0~5)
 * @param outTransform 출력 4x4 행렬
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
ForwardKinematicsToLink(RobotHandle handle,
                        const JointAngles* joints,
                        int linkIndex,
                        TransformMatrix* outTransform);

// ===== 역기구학 (IK) =====

/**
 * 해석적 역기구학 (6-DOF, 최대 8개 해)
 * @param handle 로봇 핸들
 * @param target 목표 TCP 변환행렬
 * @param solutions 출력 해 배열 (최대 8개)
 * @param solutionCount 출력 해 개수
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
InverseKinematics(RobotHandle handle,
                  const TransformMatrix* target,
                  JointAngles* solutions,
                  int* solutionCount);

/**
 * 현재 조인트 기준 가장 가까운 IK 해
 * @param handle 로봇 핸들
 * @param target 목표 TCP
 * @param currentJoints 현재 조인트 (가중치 기준)
 * @param weights 조인트별 가중치 (nullptr: 균등)
 * @param outJoints 출력 최적해
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
InverseKinematicsNearest(RobotHandle handle,
                         const TransformMatrix* target,
                         const JointAngles* currentJoints,
                         const double* weights,
                         JointAngles* outJoints);

/**
 * 수치적 IK (반복법)
 * @param handle 로봇 핸들
 * @param target 목표 TCP
 * @param seed 초기 추정값
 * @param maxIterations 최대 반복
 * @param tolerance 위치 오차 허용치
 * @param outJoints 출력 해
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
InverseKinematicsNumerical(RobotHandle handle,
                           const TransformMatrix* target,
                           const JointAngles* seed,
                           int maxIterations,
                           double tolerance,
                           JointAngles* outJoints);

// ===== 야코비안 =====

/**
 * 기하학적 야코비안 계산
 * @param handle 로봇 핸들
 * @param joints 현재 조인트
 * @param outJacobian 출력 6x6 야코비안
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
ComputeJacobian(RobotHandle handle,
                const JointAngles* joints,
                Jacobian6x6* outJacobian);

/**
 * 조작성(Manipulability) 계산
 * @param handle 로봇 핸들
 * @param joints 현재 조인트
 * @return 조작성 값 (0~1), 오류 시 -1
 */
EXPORT_API double CALLING_CONVENTION 
ComputeManipulability(RobotHandle handle,
                      const JointAngles* joints);

/**
 * 특이점 근접도 계산
 * @param handle 로봇 핸들
 * @param joints 현재 조인트
 * @return det(J*J^T), 0에 가까우면 특이점
 */
EXPORT_API double CALLING_CONVENTION 
ComputeSingularityMeasure(RobotHandle handle,
                          const JointAngles* joints);

// ===== 조인트 검증 =====

/**
 * 조인트 한계 검사
 * @param handle 로봇 핸들
 * @param joints 조인트 각도
 * @return 1: 유효, 0: 한계 초과
 */
EXPORT_API int CALLING_CONVENTION 
CheckJointLimits(RobotHandle handle,
                 const JointAngles* joints);

/**
 * 조인트 각도를 한계 내로 클램프
 * @param handle 로봇 핸들
 * @param joints 입출력 조인트
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
ClampJointLimits(RobotHandle handle,
                 JointAngles* joints);

#ifdef __cplusplus
}
#endif
```

---

## 5. 경로 계획 API

### 5.1 path_planner.h

```cpp
// include/path_planner.h
#pragma once

#include "exports.h"
#include "robot_ik.h"

#ifdef __cplusplus
extern "C" {
#endif

// ===== 용접 경로 포인트 =====
typedef struct {
    double position[3];   // TCP 위치 (x, y, z)
    double normal[3];     // 표면 법선
    double tangent[3];    // 진행 방향 접선
    double arcLength;     // 시작점으로부터 호 길이
} WeldPoint;

// ===== 경로 생성 파라미터 =====
typedef struct {
    float stepSize;       // 포인트 간격 (mm)
    float standoffDist;   // 표면으로부터 거리 (mm)
    float approachAngle;  // 접근 각도 (라디안)
    float travelAngle;    // 진행 각도 (라디안)
    int weaveType;        // WeaveType 열거형
    float weaveWidth;     // 위빙 폭 (mm)
    float weaveFreq;      // 위빙 주파수 (Hz)
} PathParams;

// ===== 경로 생성/해제 =====

/**
 * 메쉬 엣지에서 용접 경로 추출
 * @param meshHandle 메쉬 핸들
 * @param params 경로 파라미터
 * @return 경로 핸들
 */
EXPORT_API PathHandle CALLING_CONVENTION 
ExtractWeldPathFromEdge(MeshHandle meshHandle,
                        const PathParams* params);

/**
 * 메쉬 심(Seam)에서 용접 경로 추출
 * @param meshHandle 메쉬 핸들
 * @param startPoint 시작점 좌표
 * @param params 경로 파라미터
 * @return 경로 핸들
 */
EXPORT_API PathHandle CALLING_CONVENTION 
ExtractWeldPathFromSeam(MeshHandle meshHandle,
                        const float* startPoint,
                        const PathParams* params);

/**
 * 사용자 정의 포인트 배열에서 경로 생성
 * @param points WeldPoint 배열
 * @param count 포인트 개수
 * @return 경로 핸들
 */
EXPORT_API PathHandle CALLING_CONVENTION 
CreatePathFromPoints(const WeldPoint* points, int count);

/**
 * 경로 해제
 */
EXPORT_API void CALLING_CONVENTION 
DestroyPath(PathHandle handle);

// ===== 경로 정보 =====

/**
 * 경로 포인트 개수
 */
EXPORT_API int CALLING_CONVENTION 
GetPathPointCount(PathHandle handle);

/**
 * 경로 총 길이 (mm)
 */
EXPORT_API double CALLING_CONVENTION 
GetPathTotalLength(PathHandle handle);

/**
 * 모든 경로 포인트 복사
 * @param handle 경로 핸들
 * @param buffer 출력 버퍼 (WeldPoint 배열)
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
GetPathPoints(PathHandle handle, WeldPoint* buffer);

// ===== 경로 → 조인트 변환 =====

/**
 * TCP 경로를 조인트 경로로 변환
 * @param robotHandle 로봇 핸들
 * @param pathHandle 경로 핸들
 * @param seedJoints 시작 조인트 (첫 IK 시드)
 * @param outJoints 출력 조인트 배열
 * @param outCount 출력 개수 (성공한 포인트)
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
ConvertPathToJoints(RobotHandle robotHandle,
                    PathHandle pathHandle,
                    const JointAngles* seedJoints,
                    JointAngles* outJoints,
                    int* outCount);

/**
 * TCP 경로를 조인트 경로로 변환 (최적화)
 * @param robotHandle 로봇 핸들
 * @param pathHandle 경로 핸들
 * @param seedJoints 시작 조인트
 * @param minManipulability 최소 조작성 (0~1)
 * @param outJoints 출력 조인트 배열
 * @param outManipulability 각 포인트의 조작성
 * @param outCount 출력 개수
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
ConvertPathToJointsOptimized(RobotHandle robotHandle,
                             PathHandle pathHandle,
                             const JointAngles* seedJoints,
                             double minManipulability,
                             JointAngles* outJoints,
                             double* outManipulability,
                             int* outCount);

// ===== 경로 수정 =====

/**
 * 경로에 위빙 패턴 적용
 * @param handle 경로 핸들
 * @param type WeaveType
 * @param width 위빙 폭
 * @param frequency 주파수
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
ApplyWeavePattern(PathHandle handle,
                  int type,
                  float width,
                  float frequency);

/**
 * 경로 리샘플링
 * @param handle 경로 핸들
 * @param targetStep 목표 간격
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
ResamplePath(PathHandle handle, float targetStep);

/**
 * B-스플라인 스무딩
 * @param handle 경로 핸들
 * @param smoothness 스무딩 강도 (0~1)
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
SmoothPath(PathHandle handle, float smoothness);

// ===== 충돌/도달성 검사 =====

/**
 * 경로 도달성 검사
 * @param robotHandle 로봇 핸들
 * @param pathHandle 경로 핸들
 * @param seedJoints 시작 조인트
 * @param reachabilityMask 출력: 각 포인트 도달 가능 여부
 * @return 도달 가능 비율 (0~1)
 */
EXPORT_API double CALLING_CONVENTION 
CheckPathReachability(RobotHandle robotHandle,
                      PathHandle pathHandle,
                      const JointAngles* seedJoints,
                      int* reachabilityMask);

#ifdef __cplusplus
}
#endif
```

---

## 6. 핵심 구현 코드

### 6.1 exports.cpp - 진입점

```cpp
// src/exports.cpp
#include "exports.h"
#include "internal_types.h"
#include <open3d/Open3D.h>

// Unity 플러그인 라이프사이클
extern "C" {

void CALLING_CONVENTION UnityPluginLoad(void* unityInterfaces) {
    smr::LogInfo("UnityPluginLoad", "SMR Welding Plugin loaded");
}

void CALLING_CONVENTION UnityPluginUnload() {
    smr::LogInfo("UnityPluginUnload", "SMR Welding Plugin unloaded");
}

} // extern "C"

namespace smr {

void LogError(const char* function, const char* message) {
    std::cerr << "[SMR ERROR] " << function << ": " << message << std::endl;
}

void LogInfo(const char* function, const char* message) {
    std::cout << "[SMR INFO] " << function << ": " << message << std::endl;
}

} // namespace smr
```

### 6.2 point_cloud.cpp

```cpp
// src/point_cloud.cpp
#include "point_cloud.h"
#include "internal_types.h"
#include <fstream>

using namespace smr;
using namespace open3d;

extern "C" {

PointCloudHandle CALLING_CONVENTION CreatePointCloud() {
    try {
        return new PointCloudData();
    } catch (...) {
        LogError("CreatePointCloud", "Memory allocation failed");
        return nullptr;
    }
}

void CALLING_CONVENTION DestroyPointCloud(PointCloudHandle handle) {
    if (handle) {
        delete ToPointCloud(handle);
    }
}

int CALLING_CONVENTION LoadPointCloudPLY(PointCloudHandle handle, 
                                          const char* filepath) {
    if (!handle) return static_cast<int>(ErrorCode::INVALID_HANDLE);
    if (!filepath) return static_cast<int>(ErrorCode::INVALID_PARAMETER);
    
    auto* data = ToPointCloud(handle);
    
    if (!io::ReadPointCloud(filepath, *data->cloud)) {
        LogError("LoadPointCloudPLY", "Failed to read file");
        return static_cast<int>(ErrorCode::FILE_NOT_FOUND);
    }
    
    data->normals_estimated = data->cloud->HasNormals();
    return static_cast<int>(ErrorCode::SUCCESS);
}

int CALLING_CONVENTION SetPointCloudPoints(PointCloudHandle handle,
                                            const float* points,
                                            int pointCount) {
    if (!handle) return static_cast<int>(ErrorCode::INVALID_HANDLE);
    if (!points || pointCount <= 0) 
        return static_cast<int>(ErrorCode::INVALID_PARAMETER);
    
    auto* data = ToPointCloud(handle);
    data->cloud->points_.clear();
    data->cloud->points_.reserve(pointCount);
    
    for (int i = 0; i < pointCount; ++i) {
        data->cloud->points_.emplace_back(
            points[i*3], points[i*3+1], points[i*3+2]
        );
    }
    
    return static_cast<int>(ErrorCode::SUCCESS);
}

int CALLING_CONVENTION EstimateNormalsKNN(PointCloudHandle handle, int knn) {
    if (!handle) return static_cast<int>(ErrorCode::INVALID_HANDLE);
    if (knn < 3) return static_cast<int>(ErrorCode::INVALID_PARAMETER);
    
    auto* data = ToPointCloud(handle);
    
    if (data->cloud->points_.size() < 3) {
        return static_cast<int>(ErrorCode::INSUFFICIENT_POINTS);
    }
    
    data->cloud->EstimateNormals(
        geometry::KDTreeSearchParamKNN(knn)
    );
    data->normals_estimated = true;
    
    return static_cast<int>(ErrorCode::SUCCESS);
}

int CALLING_CONVENTION EstimateNormalsRadius(PointCloudHandle handle,
                                              float radius, int maxNN) {
    if (!handle) return static_cast<int>(ErrorCode::INVALID_HANDLE);
    
    auto* data = ToPointCloud(handle);
    
    data->cloud->EstimateNormals(
        geometry::KDTreeSearchParamHybrid(radius, maxNN)
    );
    data->normals_estimated = true;
    
    return static_cast<int>(ErrorCode::SUCCESS);
}

int CALLING_CONVENTION OrientNormalsConsistent(PointCloudHandle handle, int k) {
    if (!handle) return static_cast<int>(ErrorCode::INVALID_HANDLE);
    
    auto* data = ToPointCloud(handle);
    
    if (!data->normals_estimated) {
        return static_cast<int>(ErrorCode::NORMAL_ESTIMATION_FAILED);
    }
    
    data->cloud->OrientNormalsConsistentTangentPlane(k);
    return static_cast<int>(ErrorCode::SUCCESS);
}

int CALLING_CONVENTION GetPointCloudPointCount(PointCloudHandle handle) {
    if (!handle) return -1;
    return static_cast<int>(ToPointCloud(handle)->cloud->points_.size());
}

int CALLING_CONVENTION HasNormals(PointCloudHandle handle) {
    if (!handle) return -1;
    return ToPointCloud(handle)->cloud->HasNormals() ? 1 : 0;
}

int CALLING_CONVENTION GetPointCloudPoints(PointCloudHandle handle, 
                                            float* buffer) {
    if (!handle) return static_cast<int>(ErrorCode::INVALID_HANDLE);
    if (!buffer) return static_cast<int>(ErrorCode::INVALID_PARAMETER);
    
    auto* data = ToPointCloud(handle);
    size_t idx = 0;
    for (const auto& pt : data->cloud->points_) {
        buffer[idx++] = static_cast<float>(pt.x());
        buffer[idx++] = static_cast<float>(pt.y());
        buffer[idx++] = static_cast<float>(pt.z());
    }
    
    return static_cast<int>(ErrorCode::SUCCESS);
}

int CALLING_CONVENTION DownsampleVoxel(PointCloudHandle handle, 
                                        float voxelSize) {
    if (!handle) return static_cast<int>(ErrorCode::INVALID_HANDLE);
    
    auto* data = ToPointCloud(handle);
    auto downsampled = data->cloud->VoxelDownSample(voxelSize);
    data->cloud = downsampled;
    
    return static_cast<int>(ErrorCode::SUCCESS);
}

int CALLING_CONVENTION RemoveStatisticalOutliers(PointCloudHandle handle,
                                                  int nbNeighbors,
                                                  float stdRatio) {
    if (!handle) return static_cast<int>(ErrorCode::INVALID_HANDLE);
    
    auto* data = ToPointCloud(handle);
    auto [filtered, indices] = data->cloud->RemoveStatisticalOutliers(
        nbNeighbors, stdRatio
    );
    data->cloud = filtered;
    
    return static_cast<int>(ErrorCode::SUCCESS);
}

} // extern "C"
```

### 6.3 mesh_generator.cpp (핵심부)

```cpp
// src/mesh_generator.cpp
#include "mesh_generator.h"
#include "internal_types.h"

using namespace smr;
using namespace open3d;

extern "C" {

MeshHandle CALLING_CONVENTION CreatePoissonMesh(PointCloudHandle pcdHandle,
                                                 int depth,
                                                 float scale,
                                                 int linearFit) {
    if (!pcdHandle) return nullptr;
    
    auto* pcdData = ToPointCloud(pcdHandle);
    
    // 법선 체크
    if (!pcdData->cloud->HasNormals()) {
        LogError("CreatePoissonMesh", "Point cloud has no normals");
        return nullptr;
    }
    
    try {
        auto meshData = new MeshData();
        
        // Poisson Surface Reconstruction
        auto [mesh, densities] = geometry::TriangleMesh::
            CreateFromPointCloudPoisson(
                *pcdData->cloud,
                depth,
                0,           // width (0 = auto)
                scale,
                linearFit != 0
            );
        
        meshData->mesh = mesh;
        meshData->densities = densities;
        
        // 법선 계산
        meshData->mesh->ComputeVertexNormals();
        
        LogInfo("CreatePoissonMesh", 
                ("Created mesh with " + 
                 std::to_string(mesh->vertices_.size()) + " vertices").c_str());
        
        return meshData;
        
    } catch (const std::exception& e) {
        LogError("CreatePoissonMesh", e.what());
        return nullptr;
    }
}

void CALLING_CONVENTION DestroyMesh(MeshHandle handle) {
    if (handle) {
        delete ToMesh(handle);
    }
}

int CALLING_CONVENTION GetMeshVertexCount(MeshHandle handle) {
    if (!handle) return -1;
    return static_cast<int>(ToMesh(handle)->mesh->vertices_.size());
}

int CALLING_CONVENTION GetMeshTriangleCount(MeshHandle handle) {
    if (!handle) return -1;
    return static_cast<int>(ToMesh(handle)->mesh->triangles_.size());
}

int CALLING_CONVENTION GetMeshVertices(MeshHandle handle, float* buffer) {
    if (!handle || !buffer) 
        return static_cast<int>(ErrorCode::INVALID_PARAMETER);
    
    auto* data = ToMesh(handle);
    size_t idx = 0;
    for (const auto& v : data->mesh->vertices_) {
        buffer[idx++] = static_cast<float>(v.x());
        buffer[idx++] = static_cast<float>(v.y());
        buffer[idx++] = static_cast<float>(v.z());
    }
    
    return static_cast<int>(ErrorCode::SUCCESS);
}

int CALLING_CONVENTION GetMeshTriangles(MeshHandle handle, int* buffer) {
    if (!handle || !buffer) 
        return static_cast<int>(ErrorCode::INVALID_PARAMETER);
    
    auto* data = ToMesh(handle);
    size_t idx = 0;
    for (const auto& tri : data->mesh->triangles_) {
        buffer[idx++] = static_cast<int>(tri.x());
        buffer[idx++] = static_cast<int>(tri.y());
        buffer[idx++] = static_cast<int>(tri.z());
    }
    
    return static_cast<int>(ErrorCode::SUCCESS);
}

int CALLING_CONVENTION RemoveLowDensityVertices(MeshHandle handle,
                                                 float densityThreshold) {
    if (!handle) return static_cast<int>(ErrorCode::INVALID_HANDLE);
    
    auto* data = ToMesh(handle);
    
    if (data->densities.empty()) {
        return static_cast<int>(ErrorCode::INVALID_PARAMETER);
    }
    
    // 밀도 기반 필터링
    std::vector<double> sorted = data->densities;
    std::sort(sorted.begin(), sorted.end());
    
    size_t threshold_idx = static_cast<size_t>(
        densityThreshold * sorted.size()
    );
    double threshold = sorted[threshold_idx];
    
    std::vector<bool> mask(data->densities.size());
    for (size_t i = 0; i < data->densities.size(); ++i) {
        mask[i] = data->densities[i] >= threshold;
    }
    
    data->mesh = data->mesh->SelectByIndex(
        [&mask](size_t i) { return mask[i]; }
    );
    
    return static_cast<int>(ErrorCode::SUCCESS);
}

int CALLING_CONVENTION SimplifyMesh(MeshHandle handle, int targetTriangles) {
    if (!handle) return static_cast<int>(ErrorCode::INVALID_HANDLE);
    
    auto* data = ToMesh(handle);
    data->mesh = data->mesh->SimplifyQuadricDecimation(targetTriangles);
    
    return static_cast<int>(ErrorCode::SUCCESS);
}

int CALLING_CONVENTION SaveMeshPLY(MeshHandle handle, const char* filepath) {
    if (!handle || !filepath) 
        return static_cast<int>(ErrorCode::INVALID_PARAMETER);
    
    auto* data = ToMesh(handle);
    
    if (!io::WriteTriangleMesh(filepath, *data->mesh)) {
        return static_cast<int>(ErrorCode::FILE_NOT_FOUND);
    }
    
    return static_cast<int>(ErrorCode::SUCCESS);
}

} // extern "C"
```

---

## 7. DH 파라미터 프리셋

```cpp
// src/robot_presets.h
#pragma once

#include "robot_ik.h"
#include <cmath>

namespace smr {

// UR5 DH 파라미터 (미터 단위)
constexpr DHParams UR5_DH = {
    .d     = {0.089159, 0, 0, 0.10915, 0.09465, 0.0823},
    .a     = {0, -0.425, -0.39225, 0, 0, 0},
    .alpha = {M_PI/2, 0, 0, M_PI/2, -M_PI/2, 0},
    .q_home = {0, -M_PI/2, 0, -M_PI/2, 0, 0},
    .q_min = {-2*M_PI, -2*M_PI, -2*M_PI, -2*M_PI, -2*M_PI, -2*M_PI},
    .q_max = {2*M_PI, 2*M_PI, 2*M_PI, 2*M_PI, 2*M_PI, 2*M_PI}
};

// UR10 DH 파라미터
constexpr DHParams UR10_DH = {
    .d     = {0.1273, 0, 0, 0.163941, 0.1157, 0.0922},
    .a     = {0, -0.612, -0.5723, 0, 0, 0},
    .alpha = {M_PI/2, 0, 0, M_PI/2, -M_PI/2, 0},
    .q_home = {0, -M_PI/2, 0, -M_PI/2, 0, 0},
    .q_min = {-2*M_PI, -2*M_PI, -2*M_PI, -2*M_PI, -2*M_PI, -2*M_PI},
    .q_max = {2*M_PI, 2*M_PI, 2*M_PI, 2*M_PI, 2*M_PI, 2*M_PI}
};

// KUKA KR6 R700
constexpr DHParams KUKA_KR6_DH = {
    .d     = {0.400, 0, 0, 0.365, 0, 0.080},
    .a     = {0.025, 0.315, 0.035, 0, 0, 0},
    .alpha = {-M_PI/2, 0, -M_PI/2, M_PI/2, -M_PI/2, 0},
    .q_home = {0, -M_PI/2, M_PI/2, 0, 0, 0},
    .q_min = {-2.967, -2.705, -2.269, -3.491, -2.094, -3.491},
    .q_max = {2.967, 0.611, 2.618, 3.491, 2.094, 3.491}
};

// Doosan M1013
constexpr DHParams DOOSAN_M1013_DH = {
    .d     = {0.1545, 0, 0, 0.1225, 0.106, 0.1145},
    .a     = {0, -0.411, -0.368, 0, 0, 0},
    .alpha = {-M_PI/2, 0, 0, -M_PI/2, M_PI/2, 0},
    .q_home = {0, 0, -M_PI/2, 0, -M_PI/2, 0},
    .q_min = {-6.283, -6.283, -2.617, -6.283, -2.182, -6.283},
    .q_max = {6.283, 6.283, 2.617, 6.283, 2.182, 6.283}
};

const DHParams& GetPresetDH(RobotType type);

} // namespace smr
```

---

*다음: 03_Unity_CSharp_Wrapper.md (C# 래퍼 클래스)*
