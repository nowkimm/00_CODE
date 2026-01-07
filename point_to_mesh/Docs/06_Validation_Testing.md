# 검증 및 테스트 가이드

## 1. 테스트 전략 개요

### 1.1 테스트 레벨

| 레벨 | 대상 | 도구 | 빈도 |
|------|------|------|------|
| 단위 테스트 | C++ 함수, C# 클래스 | Google Test, NUnit | 매 커밋 |
| 통합 테스트 | P/Invoke 바인딩 | Unity Test Framework | 매 빌드 |
| 시스템 테스트 | 전체 파이프라인 | Unity PlayMode | 매일 |
| 성능 테스트 | 처리 시간, 메모리 | Unity Profiler | 주간 |

---

## 2. C++ 네이티브 플러그인 테스트

### 2.1 Google Test 설정

```cpp
// tests/test_point_cloud.cpp
#include <gtest/gtest.h>
#include "point_cloud.h"

class PointCloudTest : public ::testing::Test {
protected:
    PointCloudHandle handle;
    
    void SetUp() override {
        handle = CreatePointCloud();
        ASSERT_NE(handle, nullptr);
    }
    
    void TearDown() override {
        if (handle) {
            DestroyPointCloud(handle);
        }
    }
};

TEST_F(PointCloudTest, CreateAndDestroy) {
    // SetUp에서 생성, TearDown에서 해제 확인
    EXPECT_NE(handle, nullptr);
}

TEST_F(PointCloudTest, SetPointsAndGetCount) {
    float points[] = {
        0.0f, 0.0f, 0.0f,
        1.0f, 0.0f, 0.0f,
        0.0f, 1.0f, 0.0f
    };
    
    int result = SetPointCloudPoints(handle, points, 3);
    EXPECT_EQ(result, 0);
    EXPECT_EQ(GetPointCloudCount(handle), 3);
}

TEST_F(PointCloudTest, EstimateNormalsKNN) {
    // 최소 포인트 설정
    float points[30 * 3]; // 30개 포인트
    for (int i = 0; i < 30; i++) {
        points[i * 3 + 0] = (float)(i % 5);
        points[i * 3 + 1] = (float)(i / 5);
        points[i * 3 + 2] = 0.0f;
    }
    
    SetPointCloudPoints(handle, points, 30);
    
    int result = EstimateNormalsKNN(handle, 10);
    EXPECT_EQ(result, 0);
    EXPECT_TRUE(HasNormals(handle));
}

TEST_F(PointCloudTest, DownsampleVoxel) {
    // 1000개 랜덤 포인트
    std::vector<float> points(1000 * 3);
    for (int i = 0; i < 1000 * 3; i++) {
        points[i] = (float)(rand() % 100) / 100.0f;
    }
    
    SetPointCloudPoints(handle, points.data(), 1000);
    int originalCount = GetPointCloudCount(handle);
    
    DownsampleVoxel(handle, 0.1f);
    int newCount = GetPointCloudCount(handle);
    
    EXPECT_LT(newCount, originalCount);
}
```

### 2.2 메쉬 생성 테스트

```cpp
// tests/test_mesh_generator.cpp
#include <gtest/gtest.h>
#include "mesh_generator.h"
#include "point_cloud.h"

class MeshGeneratorTest : public ::testing::Test {
protected:
    PointCloudHandle pcHandle;
    
    void SetUp() override {
        pcHandle = CreatePointCloud();
        
        // 구체 포인트 클라우드 생성
        std::vector<float> points;
        const int N = 1000;
        
        for (int i = 0; i < N; i++) {
            float theta = 2.0f * M_PI * (float)rand() / RAND_MAX;
            float phi = M_PI * (float)rand() / RAND_MAX;
            float r = 1.0f;
            
            points.push_back(r * sin(phi) * cos(theta));
            points.push_back(r * sin(phi) * sin(theta));
            points.push_back(r * cos(phi));
        }
        
        SetPointCloudPoints(pcHandle, points.data(), N);
        EstimateNormalsKNN(pcHandle, 30);
        OrientNormalsConsistent(pcHandle, 10, nullptr);
    }
    
    void TearDown() override {
        DestroyPointCloud(pcHandle);
    }
};

TEST_F(MeshGeneratorTest, PoissonReconstruction) {
    MeshHandle mesh = CreatePoissonMesh(pcHandle, 8, 1.1f, 0);
    
    ASSERT_NE(mesh, nullptr);
    EXPECT_GT(GetMeshVertexCount(mesh), 0);
    EXPECT_GT(GetMeshTriangleCount(mesh), 0);
    
    DestroyMesh(mesh);
}

TEST_F(MeshGeneratorTest, GetMeshData) {
    MeshHandle mesh = CreatePoissonMesh(pcHandle, 6, 1.1f, 0);
    ASSERT_NE(mesh, nullptr);
    
    int vertCount = GetMeshVertexCount(mesh);
    int triCount = GetMeshTriangleCount(mesh);
    
    std::vector<float> vertices(vertCount * 3);
    std::vector<int> triangles(triCount * 3);
    
    int vResult = GetMeshVertices(mesh, vertices.data(), vertCount);
    int tResult = GetMeshTriangles(mesh, triangles.data(), triCount);
    
    EXPECT_EQ(vResult, vertCount);
    EXPECT_EQ(tResult, triCount);
    
    // 삼각형 인덱스 유효성 검사
    for (int i = 0; i < triCount * 3; i++) {
        EXPECT_GE(triangles[i], 0);
        EXPECT_LT(triangles[i], vertCount);
    }
    
    DestroyMesh(mesh);
}
```

### 2.3 로봇 IK 테스트

```cpp
// tests/test_robot_ik.cpp
#include <gtest/gtest.h>
#include "robot_ik.h"
#include <cmath>

class RobotIKTest : public ::testing::Test {
protected:
    RobotHandle robot;
    
    void SetUp() override {
        robot = CreateRobot(ROBOT_UR10);
        ASSERT_NE(robot, nullptr);
    }
    
    void TearDown() override {
        DestroyRobot(robot);
    }
};

TEST_F(RobotIKTest, ForwardKinematics_Home) {
    float joints[6] = {0, 0, 0, 0, 0, 0};
    float transform[16];
    
    int result = ForwardKinematics(robot, joints, transform);
    EXPECT_EQ(result, 0);
    
    // 변환 행렬 유효성 (4x4, 회전 부분 직교)
    // 마지막 행이 [0, 0, 0, 1]
    EXPECT_NEAR(transform[12], 0.0f, 1e-6f);
    EXPECT_NEAR(transform[13], 0.0f, 1e-6f);
    EXPECT_NEAR(transform[14], 0.0f, 1e-6f);
    EXPECT_NEAR(transform[15], 1.0f, 1e-6f);
}

TEST_F(RobotIKTest, IK_FK_Consistency) {
    // 임의 조인트 각도
    float originalJoints[6] = {0.1f, -0.5f, 1.2f, 0.3f, -0.8f, 0.4f};
    float transform[16];
    
    // FK 계산
    ForwardKinematics(robot, originalJoints, transform);
    
    // IK로 역계산
    float solutions[8 * 6];
    int solutionCount;
    InverseKinematics(robot, transform, solutions, &solutionCount);
    
    EXPECT_GT(solutionCount, 0);
    
    // 각 해에 대해 FK 재계산 후 원래 변환과 비교
    for (int i = 0; i < solutionCount; i++) {
        float* sol = &solutions[i * 6];
        float recoveredTransform[16];
        
        ForwardKinematics(robot, sol, recoveredTransform);
        
        // 위치 비교 (허용 오차 1mm)
        EXPECT_NEAR(transform[3], recoveredTransform[3], 0.001f);
        EXPECT_NEAR(transform[7], recoveredTransform[7], 0.001f);
        EXPECT_NEAR(transform[11], recoveredTransform[11], 0.001f);
    }
}

TEST_F(RobotIKTest, Manipulability) {
    // 특이점이 아닌 자세
    float normalJoints[6] = {0, -M_PI/4, M_PI/2, 0, M_PI/4, 0};
    float manip = ComputeManipulability(robot, normalJoints);
    
    EXPECT_GT(manip, 0.0f);
    EXPECT_LE(manip, 1.0f);
    
    // 특이점 근처 자세 (팔꿈치 완전 펴짐)
    float singularJoints[6] = {0, 0, 0, 0, 0, 0};
    float singularManip = ComputeManipulability(robot, singularJoints);
    
    EXPECT_LT(singularManip, manip);
}

TEST_F(RobotIKTest, JointLimits) {
    // 한계 내 조인트
    float validJoints[6] = {0, 0, 0, 0, 0, 0};
    EXPECT_EQ(CheckJointLimits(robot, validJoints), 0);
    
    // 한계 초과 조인트
    float invalidJoints[6] = {10.0f, 0, 0, 0, 0, 0}; // 10 rad > 2*pi
    EXPECT_NE(CheckJointLimits(robot, invalidJoints), 0);
}
```

---

## 3. Unity C# 테스트

### 3.1 NUnit 단위 테스트

```csharp
// Tests/Editor/NativeArrayHelperTests.cs
using NUnit.Framework;
using UnityEngine;
using SMRWelding.Native;

[TestFixture]
public class NativeArrayHelperTests
{
    [Test]
    public void Vector3ArrayToFloatArray_ValidInput_CorrectOutput()
    {
        Vector3[] input = new Vector3[]
        {
            new Vector3(1, 2, 3),
            new Vector3(4, 5, 6)
        };
        
        float[] result = NativeArrayHelper.Vector3ArrayToFloatArray(input);
        
        Assert.AreEqual(6, result.Length);
        Assert.AreEqual(1f, result[0]);
        Assert.AreEqual(2f, result[1]);
        Assert.AreEqual(3f, result[2]);
        Assert.AreEqual(4f, result[3]);
        Assert.AreEqual(5f, result[4]);
        Assert.AreEqual(6f, result[5]);
    }
    
    [Test]
    public void FloatArrayToVector3Array_ValidInput_CorrectOutput()
    {
        float[] input = new float[] { 1, 2, 3, 4, 5, 6 };
        
        Vector3[] result = NativeArrayHelper.FloatArrayToVector3Array(input);
        
        Assert.AreEqual(2, result.Length);
        Assert.AreEqual(new Vector3(1, 2, 3), result[0]);
        Assert.AreEqual(new Vector3(4, 5, 6), result[1]);
    }
    
    [Test]
    public void Matrix4x4Conversion_RoundTrip_Preserved()
    {
        Matrix4x4 original = Matrix4x4.TRS(
            new Vector3(1, 2, 3),
            Quaternion.Euler(30, 45, 60),
            Vector3.one);
        
        float[] floatArray = NativeArrayHelper.Matrix4x4ToFloatArray(original);
        Matrix4x4 recovered = NativeArrayHelper.FloatArrayToMatrix4x4(floatArray);
        
        for (int i = 0; i < 16; i++)
        {
            Assert.AreEqual(original[i / 4, i % 4], recovered[i / 4, i % 4], 1e-5f);
        }
    }
}
```

### 3.2 PlayMode 통합 테스트

```csharp
// Tests/PlayMode/PointCloudProcessorTests.cs
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SMRWelding.PointCloud;

[TestFixture]
public class PointCloudProcessorTests
{
    [UnityTest]
    public IEnumerator CreateAndDispose_NoMemoryLeak()
    {
        long memBefore = System.GC.GetTotalMemory(true);
        
        for (int i = 0; i < 10; i++)
        {
            using (var processor = new PointCloudProcessor())
            {
                // 간단한 데이터 설정
                var data = new PointCloudData(new Vector3[100]);
                processor.SetData(data);
            }
            yield return null;
        }
        
        System.GC.Collect();
        yield return null;
        
        long memAfter = System.GC.GetTotalMemory(true);
        
        // 메모리 누수 없음 확인 (10MB 이하 증가)
        Assert.LessOrEqual(memAfter - memBefore, 10 * 1024 * 1024);
    }
    
    [UnityTest]
    public IEnumerator SetData_ValidData_CorrectPointCount()
    {
        using (var processor = new PointCloudProcessor())
        {
            var points = new Vector3[1000];
            for (int i = 0; i < 1000; i++)
            {
                points[i] = Random.insideUnitSphere;
            }
            
            var data = new PointCloudData(points);
            processor.SetData(data);
            
            yield return null;
            
            Assert.AreEqual(1000, processor.PointCount);
        }
    }
    
    [UnityTest]
    public IEnumerator EstimateNormals_HasNormalsAfter()
    {
        using (var processor = new PointCloudProcessor())
        {
            // 평면 포인트 생성
            var points = new Vector3[100];
            for (int i = 0; i < 100; i++)
            {
                points[i] = new Vector3(
                    Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f),
                    0);
            }
            
            processor.SetData(new PointCloudData(points));
            
            Assert.IsFalse(processor.HasNormals);
            
            processor.EstimateNormalsKNN(10);
            
            yield return null;
            
            Assert.IsTrue(processor.HasNormals);
        }
    }
}
```

### 3.3 메쉬 생성 테스트

```csharp
// Tests/PlayMode/MeshGeneratorTests.cs
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SMRWelding.PointCloud;
using SMRWelding.Mesh;

[TestFixture]
public class MeshGeneratorTests
{
    private PointCloudData CreateSpherePointCloud(int count = 1000)
    {
        var points = new Vector3[count];
        var normals = new Vector3[count];
        
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Random.onUnitSphere;
            points[i] = dir;
            normals[i] = dir; // 구의 법선 = 위치 정규화
        }
        
        return new PointCloudData
        {
            Points = points,
            Normals = normals
        };
    }
    
    [UnityTest]
    public IEnumerator GenerateMesh_ValidPointCloud_ProducesMesh()
    {
        var pcData = CreateSpherePointCloud(500);
        
        using (var processor = new PointCloudProcessor(pcData))
        using (var meshGen = new MeshGenerator())
        {
            var settings = new PoissonSettings
            {
                Depth = 6,
                Scale = 1.1f
            };
            
            meshGen.GenerateFromPointCloud(processor, settings);
            
            yield return null;
            
            Assert.Greater(meshGen.VertexCount, 0);
            Assert.Greater(meshGen.TriangleCount, 0);
            
            // Unity 메쉬 변환
            var unityMesh = meshGen.ToUnityMesh();
            Assert.IsNotNull(unityMesh);
            Assert.AreEqual(meshGen.VertexCount, unityMesh.vertexCount);
        }
    }
    
    [UnityTest]
    public IEnumerator GenerateMesh_NoNormals_ThrowsException()
    {
        var points = new Vector3[100];
        for (int i = 0; i < 100; i++)
            points[i] = Random.insideUnitSphere;
        
        var pcData = new PointCloudData(points); // 법선 없음
        
        using (var processor = new PointCloudProcessor(pcData))
        using (var meshGen = new MeshGenerator())
        {
            Assert.Throws<System.InvalidOperationException>(() =>
            {
                meshGen.GenerateFromPointCloud(processor, new PoissonSettings());
            });
        }
        
        yield return null;
    }
}
```

---

## 4. 성능 테스트

### 4.1 벤치마크 스크립트

```csharp
// Tests/Performance/BenchmarkRunner.cs
using System;
using System.Diagnostics;
using UnityEngine;
using SMRWelding.PointCloud;
using SMRWelding.Mesh;

public class BenchmarkRunner : MonoBehaviour
{
    [Header("Test Parameters")]
    public int[] pointCounts = { 1000, 10000, 100000 };
    public int[] poissonDepths = { 6, 7, 8, 9 };
    
    void Start()
    {
        RunBenchmarks();
    }
    
    void RunBenchmarks()
    {
        UnityEngine.Debug.Log("=== Performance Benchmarks ===");
        
        foreach (int count in pointCounts)
        {
            UnityEngine.Debug.Log($"\n--- Point Count: {count} ---");
            
            // 포인트 클라우드 생성
            var stopwatch = Stopwatch.StartNew();
            var pcData = GenerateTestPointCloud(count);
            stopwatch.Stop();
            UnityEngine.Debug.Log($"Generate PC: {stopwatch.ElapsedMilliseconds}ms");
            
            using (var processor = new PointCloudProcessor(pcData))
            {
                // 법선 추정
                stopwatch.Restart();
                processor.EstimateNormalsKNN(30);
                stopwatch.Stop();
                UnityEngine.Debug.Log($"Normal Estimation: {stopwatch.ElapsedMilliseconds}ms");
                
                foreach (int depth in poissonDepths)
                {
                    stopwatch.Restart();
                    
                    using (var meshGen = new MeshGenerator())
                    {
                        var settings = new PoissonSettings { Depth = depth };
                        meshGen.GenerateFromPointCloud(processor, settings);
                        
                        stopwatch.Stop();
                        UnityEngine.Debug.Log(
                            $"Poisson (depth={depth}): {stopwatch.ElapsedMilliseconds}ms, " +
                            $"Vertices: {meshGen.VertexCount}, Triangles: {meshGen.TriangleCount}");
                    }
                }
            }
        }
    }
    
    PointCloudData GenerateTestPointCloud(int count)
    {
        var points = new Vector3[count];
        var normals = new Vector3[count];
        
        // 구체 표면 샘플링
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = UnityEngine.Random.onUnitSphere;
            points[i] = dir;
            normals[i] = dir;
        }
        
        return new PointCloudData
        {
            Points = points,
            Normals = normals
        };
    }
}
```

### 4.2 메모리 프로파일링

```csharp
// Tests/Performance/MemoryProfiler.cs
using UnityEngine;
using UnityEngine.Profiling;
using SMRWelding.PointCloud;
using SMRWelding.Mesh;

public class MemoryProfiler : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            LogMemoryStats("Manual Check");
        }
    }
    
    public static void LogMemoryStats(string label)
    {
        long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
        long totalReserved = Profiler.GetTotalReservedMemoryLong();
        long monoHeap = Profiler.GetMonoHeapSizeLong();
        long monoUsed = Profiler.GetMonoUsedSizeLong();
        
        UnityEngine.Debug.Log($"[{label}] " +
            $"Total: {totalAllocated / 1024 / 1024}MB / {totalReserved / 1024 / 1024}MB, " +
            $"Mono: {monoUsed / 1024 / 1024}MB / {monoHeap / 1024 / 1024}MB");
    }
    
    public static void ProfileOperation(string name, System.Action action)
    {
        System.GC.Collect();
        long memBefore = System.GC.GetTotalMemory(true);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        
        System.GC.Collect();
        long memAfter = System.GC.GetTotalMemory(true);
        
        UnityEngine.Debug.Log($"[{name}] Time: {stopwatch.ElapsedMilliseconds}ms, " +
            $"Memory Delta: {(memAfter - memBefore) / 1024}KB");
    }
}
```

---

## 5. 검증 체크리스트

### 5.1 기능 검증

| 항목 | 테스트 방법 | 합격 기준 |
|------|-------------|-----------|
| PLY 로드 | 샘플 파일 로드 | 포인트 수 일치 |
| PCD 로드 | 샘플 파일 로드 | 포인트 수 일치 |
| 복셀 다운샘플링 | 전후 포인트 수 비교 | 감소 확인 |
| 법선 추정 | HasNormals 확인 | true |
| Poisson 재구성 | 메쉬 정점/삼각형 수 | > 0 |
| Unity 메쉬 변환 | 렌더링 확인 | 시각적 검증 |
| FK 계산 | 알려진 자세 비교 | 위치 오차 < 1mm |
| IK 계산 | FK-IK 왕복 | 위치 오차 < 1mm |

### 5.2 품질 검증

| 항목 | 측정 방법 | 합격 기준 |
|------|-----------|-----------|
| 메쉬 완전성 | 구멍 검출 | 구멍 없음 |
| 메쉬 방향 | 법선 일관성 | 외향 법선 |
| 표면 품질 | Hausdorff 거리 | < 원본 해상도 |
| 로봇 도달성 | 경로 포인트 검사 | > 95% |
| 특이점 회피 | 조작성 지수 | > 0.01 |

---

*다음: 07_Next_Steps.md*
