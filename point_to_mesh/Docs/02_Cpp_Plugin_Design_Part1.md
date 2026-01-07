# C++ 플러그인 설계 Part 1: 헤더 및 API 정의

## 1. 빌드 환경 구성

### 1.1 CMakeLists.txt

```cmake
cmake_minimum_required(VERSION 3.18)
project(SMRWeldingPlugin VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# ===== Open3D 의존성 =====
find_package(Open3D REQUIRED)
if(Open3D_FOUND)
    message(STATUS "Found Open3D ${Open3D_VERSION}")
endif()

# ===== Eigen 의존성 (Open3D에 포함) =====
find_package(Eigen3 REQUIRED)

# ===== 공유 라이브러리 빌드 =====
add_library(${PROJECT_NAME} SHARED
    src/exports.cpp
    src/point_cloud.cpp
    src/mesh_generator.cpp
    src/robot_ik.cpp
    src/path_planner.cpp
)

target_include_directories(${PROJECT_NAME} 
    PUBLIC 
        ${CMAKE_CURRENT_SOURCE_DIR}/include
    PRIVATE
        ${Open3D_INCLUDE_DIRS}
)

target_link_libraries(${PROJECT_NAME} 
    PRIVATE 
        Open3D::Open3D
        Eigen3::Eigen
)

# ===== 플랫폼별 설정 =====
if(WIN32)
    target_compile_definitions(${PROJECT_NAME} PRIVATE PLUGIN_EXPORTS)
    set_target_properties(${PROJECT_NAME} PROPERTIES
        RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/bin"
        LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib"
    )
elseif(APPLE)
    set_target_properties(${PROJECT_NAME} PROPERTIES
        SUFFIX ".bundle"
    )
endif()

# ===== Unity 플러그인 출력 =====
set(UNITY_PLUGIN_DIR "${CMAKE_SOURCE_DIR}/../UnityProject/Assets/Plugins" 
    CACHE PATH "Unity Plugins directory")

add_custom_command(TARGET ${PROJECT_NAME} POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy
        $<TARGET_FILE:${PROJECT_NAME}>
        "${UNITY_PLUGIN_DIR}/$<TARGET_FILE_NAME:${PROJECT_NAME}>"
    COMMENT "Copying plugin to Unity Plugins folder"
)
```

---

## 2. 핵심 헤더 파일

### 2.1 exports.h - DLL 내보내기 매크로

```cpp
// include/exports.h
#pragma once

#ifdef _WIN32
    #ifdef PLUGIN_EXPORTS
        #define EXPORT_API __declspec(dllexport)
    #else
        #define EXPORT_API __declspec(dllimport)
    #endif
    #define CALLING_CONVENTION __stdcall
#else
    #define EXPORT_API __attribute__((visibility("default")))
    #define CALLING_CONVENTION
#endif

// 핸들 타입 정의
typedef void* PointCloudHandle;
typedef void* MeshHandle;
typedef void* RobotHandle;
typedef void* PathHandle;

// 에러 코드
enum class ErrorCode : int {
    SUCCESS = 0,
    INVALID_HANDLE = -1,
    INVALID_PARAMETER = -2,
    FILE_NOT_FOUND = -3,
    INSUFFICIENT_POINTS = -4,
    NORMAL_ESTIMATION_FAILED = -5,
    POISSON_FAILED = -6,
    IK_NO_SOLUTION = -7,
    PATH_EXTRACTION_FAILED = -8,
    OUT_OF_MEMORY = -9,
    UNKNOWN_ERROR = -100
};

// 로봇 타입
enum class RobotType : int {
    UR3 = 0,
    UR5 = 1,
    UR10 = 2,
    UR16 = 3,
    KUKA_KR6 = 10,
    KUKA_KR16 = 11,
    ABB_IRB1200 = 20,
    DOOSAN_M1013 = 30,
    GENERIC_6DOF = 100
};

// 위빙 패턴 타입
enum class WeaveType : int {
    NONE = 0,
    ZIGZAG = 1,
    CIRCULAR = 2,
    TRIANGLE = 3,
    FIGURE8 = 4
};
```

### 2.2 point_cloud.h - 포인트클라우드 API

```cpp
// include/point_cloud.h
#pragma once

#include "exports.h"

#ifdef __cplusplus
extern "C" {
#endif

// ===== 생성/해제 =====

/**
 * 빈 포인트클라우드 객체 생성
 * @return 포인트클라우드 핸들, 실패 시 nullptr
 */
EXPORT_API PointCloudHandle CALLING_CONVENTION 
CreatePointCloud();

/**
 * 포인트클라우드 객체 해제
 * @param handle 포인트클라우드 핸들
 */
EXPORT_API void CALLING_CONVENTION 
DestroyPointCloud(PointCloudHandle handle);

// ===== 데이터 로드/설정 =====

/**
 * PLY 파일에서 포인트클라우드 로드
 * @param handle 포인트클라우드 핸들
 * @param filepath PLY 파일 경로
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
LoadPointCloudPLY(PointCloudHandle handle, const char* filepath);

/**
 * PCD 파일에서 포인트클라우드 로드
 * @param handle 포인트클라우드 핸들
 * @param filepath PCD 파일 경로
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
LoadPointCloudPCD(PointCloudHandle handle, const char* filepath);

/**
 * 포인트 데이터 직접 설정
 * @param handle 포인트클라우드 핸들
 * @param points float 배열 [x0,y0,z0, x1,y1,z1, ...]
 * @param pointCount 포인트 개수
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
SetPointCloudPoints(PointCloudHandle handle, 
                    const float* points, 
                    int pointCount);

/**
 * 색상 데이터 설정
 * @param handle 포인트클라우드 핸들
 * @param colors float 배열 [r0,g0,b0, r1,g1,b1, ...] (0~1 범위)
 * @param colorCount 색상 개수 (포인트 개수와 동일해야 함)
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
SetPointCloudColors(PointCloudHandle handle, 
                    const float* colors, 
                    int colorCount);

// ===== 법선 추정 =====

/**
 * KNN 기반 법선 추정
 * @param handle 포인트클라우드 핸들
 * @param knn 이웃 개수 (권장: 30)
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
EstimateNormalsKNN(PointCloudHandle handle, int knn);

/**
 * 반경 기반 법선 추정
 * @param handle 포인트클라우드 핸들
 * @param radius 검색 반경
 * @param maxNN 최대 이웃 개수
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
EstimateNormalsRadius(PointCloudHandle handle, 
                      float radius, 
                      int maxNN);

/**
 * 법선 방향 일관성 정렬
 * @param handle 포인트클라우드 핸들
 * @param k 접선면 이웃 개수
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
OrientNormalsConsistent(PointCloudHandle handle, int k);

/**
 * 카메라 위치 기준 법선 방향 정렬
 * @param handle 포인트클라우드 핸들
 * @param cameraX, cameraY, cameraZ 카메라 위치
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
OrientNormalsToCamera(PointCloudHandle handle, 
                      float cameraX, float cameraY, float cameraZ);

// ===== 정보 조회 =====

/**
 * 포인트 개수 조회
 * @param handle 포인트클라우드 핸들
 * @return 포인트 개수, 오류 시 -1
 */
EXPORT_API int CALLING_CONVENTION 
GetPointCloudPointCount(PointCloudHandle handle);

/**
 * 법선 존재 여부 확인
 * @param handle 포인트클라우드 핸들
 * @return 1: 있음, 0: 없음, -1: 오류
 */
EXPORT_API int CALLING_CONVENTION 
HasNormals(PointCloudHandle handle);

/**
 * 포인트 데이터 복사
 * @param handle 포인트클라우드 핸들
 * @param buffer 출력 버퍼 (크기: pointCount * 3 * sizeof(float))
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
GetPointCloudPoints(PointCloudHandle handle, float* buffer);

/**
 * 법선 데이터 복사
 * @param handle 포인트클라우드 핸들
 * @param buffer 출력 버퍼 (크기: pointCount * 3 * sizeof(float))
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
GetPointCloudNormals(PointCloudHandle handle, float* buffer);

// ===== 전처리 =====

/**
 * 다운샘플링 (Voxel Grid)
 * @param handle 포인트클라우드 핸들
 * @param voxelSize 복셀 크기
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
DownsampleVoxel(PointCloudHandle handle, float voxelSize);

/**
 * 통계적 이상치 제거
 * @param handle 포인트클라우드 핸들
 * @param nbNeighbors 이웃 개수
 * @param stdRatio 표준편차 배수
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
RemoveStatisticalOutliers(PointCloudHandle handle, 
                          int nbNeighbors, 
                          float stdRatio);

#ifdef __cplusplus
}
#endif
```

### 2.3 mesh_generator.h - 메쉬 생성 API

```cpp
// include/mesh_generator.h
#pragma once

#include "exports.h"

#ifdef __cplusplus
extern "C" {
#endif

// ===== Poisson 재구성 =====

/**
 * Poisson Surface Reconstruction 실행
 * @param pcdHandle 포인트클라우드 핸들 (법선 필수)
 * @param depth 옥트리 깊이 (권장: 8-10)
 * @param scale 스케일 팩터 (권장: 1.1-1.5)
 * @param linearFit 선형 피팅 사용 여부
 * @return 메쉬 핸들, 실패 시 nullptr
 */
EXPORT_API MeshHandle CALLING_CONVENTION 
CreatePoissonMesh(PointCloudHandle pcdHandle,
                  int depth,
                  float scale,
                  int linearFit);

/**
 * Poisson 재구성 (상세 옵션)
 * @param pcdHandle 포인트클라우드 핸들
 * @param depth 옥트리 깊이
 * @param width 초기 셀 너비 (0: 자동)
 * @param scale 스케일 팩터
 * @param linearFit 선형 피팅
 * @param nThreads 스레드 수 (0: 자동)
 * @return 메쉬 핸들
 */
EXPORT_API MeshHandle CALLING_CONVENTION 
CreatePoissonMeshAdvanced(PointCloudHandle pcdHandle,
                          int depth,
                          float width,
                          float scale,
                          int linearFit,
                          int nThreads);

// ===== 메쉬 해제 =====

/**
 * 메쉬 객체 해제
 * @param handle 메쉬 핸들
 */
EXPORT_API void CALLING_CONVENTION 
DestroyMesh(MeshHandle handle);

// ===== 메쉬 정보 =====

/**
 * 정점 개수 조회
 * @param handle 메쉬 핸들
 * @return 정점 개수
 */
EXPORT_API int CALLING_CONVENTION 
GetMeshVertexCount(MeshHandle handle);

/**
 * 삼각형 개수 조회
 * @param handle 메쉬 핸들
 * @return 삼각형 개수
 */
EXPORT_API int CALLING_CONVENTION 
GetMeshTriangleCount(MeshHandle handle);

// ===== 메쉬 데이터 복사 =====

/**
 * 정점 좌표 복사
 * @param handle 메쉬 핸들
 * @param buffer 출력 버퍼 [x0,y0,z0, x1,y1,z1, ...]
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
GetMeshVertices(MeshHandle handle, float* buffer);

/**
 * 정점 법선 복사
 * @param handle 메쉬 핸들
 * @param buffer 출력 버퍼 [nx0,ny0,nz0, ...]
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
GetMeshNormals(MeshHandle handle, float* buffer);

/**
 * 삼각형 인덱스 복사
 * @param handle 메쉬 핸들
 * @param buffer 출력 버퍼 [i0,i1,i2, j0,j1,j2, ...]
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
GetMeshTriangles(MeshHandle handle, int* buffer);

/**
 * 정점 색상 복사 (있는 경우)
 * @param handle 메쉬 핸들
 * @param buffer 출력 버퍼 [r0,g0,b0, ...]
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
GetMeshColors(MeshHandle handle, float* buffer);

// ===== 메쉬 후처리 =====

/**
 * 법선 계산/갱신
 * @param handle 메쉬 핸들
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
ComputeMeshNormals(MeshHandle handle);

/**
 * 밀도 기반 저품질 정점 제거
 * @param handle 메쉬 핸들
 * @param densityThreshold 밀도 임계값 (백분위, 0~1)
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
RemoveLowDensityVertices(MeshHandle handle, float densityThreshold);

/**
 * 메쉬 단순화 (Quadric Decimation)
 * @param handle 메쉬 핸들
 * @param targetTriangles 목표 삼각형 개수
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
SimplifyMesh(MeshHandle handle, int targetTriangles);

/**
 * Laplacian Smoothing
 * @param handle 메쉬 핸들
 * @param iterations 반복 횟수
 * @param lambda 스무딩 강도 (0~1)
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
SmoothMesh(MeshHandle handle, int iterations, float lambda);

// ===== 메쉬 저장 =====

/**
 * PLY 형식으로 저장
 * @param handle 메쉬 핸들
 * @param filepath 저장 경로
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
SaveMeshPLY(MeshHandle handle, const char* filepath);

/**
 * OBJ 형식으로 저장
 * @param handle 메쉬 핸들
 * @param filepath 저장 경로
 * @return ErrorCode
 */
EXPORT_API int CALLING_CONVENTION 
SaveMeshOBJ(MeshHandle handle, const char* filepath);

#ifdef __cplusplus
}
#endif
```

---

## 3. 내부 C++ 클래스

### 3.1 내부 구조체 정의

```cpp
// src/internal_types.h
#pragma once

#include <open3d/Open3D.h>
#include <Eigen/Dense>
#include <memory>
#include <vector>

namespace smr {

// 포인트클라우드 내부 저장소
struct PointCloudData {
    std::shared_ptr<open3d::geometry::PointCloud> cloud;
    bool normals_estimated = false;
    
    PointCloudData() : cloud(std::make_shared<open3d::geometry::PointCloud>()) {}
};

// 메쉬 내부 저장소
struct MeshData {
    std::shared_ptr<open3d::geometry::TriangleMesh> mesh;
    std::vector<double> densities;  // Poisson 밀도 값
    
    MeshData() : mesh(std::make_shared<open3d::geometry::TriangleMesh>()) {}
};

// 핸들 → 포인터 변환
inline PointCloudData* ToPointCloud(PointCloudHandle h) {
    return static_cast<PointCloudData*>(h);
}

inline MeshData* ToMesh(MeshHandle h) {
    return static_cast<MeshData*>(h);
}

// 에러 로깅
void LogError(const char* function, const char* message);
void LogInfo(const char* function, const char* message);

} // namespace smr
```

---
*다음: Part 2에서 robot_ik.h, path_planner.h 및 구현 코드*
