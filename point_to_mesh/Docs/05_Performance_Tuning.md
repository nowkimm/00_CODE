# 성능 및 품질 튜닝 가이드

## 1. Poisson Surface Reconstruction 파라미터 튜닝

### 1.1 Depth 파라미터 (가장 중요)

| Depth | Octree 해상도 | 정점 수 (예상) | 처리 시간 | 사용 사례 |
|-------|---------------|----------------|-----------|-----------|
| 6 | 64³ | ~10K | <1초 | 프리뷰, 저사양 |
| 7 | 128³ | ~50K | 1-2초 | 일반 용도 |
| 8 | 256³ | ~200K | 3-5초 | 균형점 (권장) |
| 9 | 512³ | ~500K-1M | 10-30초 | 고품질 |
| 10 | 1024³ | ~2-5M | 1-3분 | 매우 고품질 |
| 11-12 | 2048³+ | ~10M+ | 5분+ | 초고해상도 |

**권장 전략:**
```csharp
// 포인트 밀도에 따른 자동 depth 선택
int AutoSelectDepth(int pointCount, float boundingBoxDiagonal)
{
    float density = pointCount / Mathf.Pow(boundingBoxDiagonal, 3);
    
    if (density < 1000) return 6;
    if (density < 10000) return 7;
    if (density < 100000) return 8;
    if (density < 1000000) return 9;
    return 10;
}
```

### 1.2 Scale 파라미터

| Scale | 효과 | 권장 상황 |
|-------|------|-----------|
| 1.0 | 바운딩 박스에 딱 맞음 | 닫힌 표면 |
| 1.1 | 약간 확장 (권장) | 일반 용도 |
| 1.2-1.5 | 많이 확장 | 열린 표면, 구멍 있음 |

### 1.3 Linear Fit

- **false (기본)**: 부드러운 표면, 노이즈 평활화
- **true**: 날카로운 엣지 보존, 노이즈 민감

---

## 2. 전처리 최적화

### 2.1 복셀 다운샘플링

```csharp
// 복셀 크기 선택 가이드
float SelectVoxelSize(float boundingBoxDiagonal, int targetPointCount)
{
    // 목표 포인트 수 기준
    float currentDensity = pointCount / Mathf.Pow(boundingBoxDiagonal, 3);
    float targetDensity = targetPointCount / Mathf.Pow(boundingBoxDiagonal, 3);
    
    // 복셀 크기 = 밀도 비율의 세제곱근
    float ratio = Mathf.Pow(currentDensity / targetDensity, 1f/3f);
    return boundingBoxDiagonal / 100f * ratio;
}
```

| 사용 사례 | 복셀 크기 | 설명 |
|-----------|-----------|------|
| 정밀 용접 | 0.5-1mm | 고밀도 유지 |
| 일반 검사 | 1-2mm | 균형점 |
| 빠른 프리뷰 | 3-5mm | 속도 우선 |

### 2.2 이상치 제거

```csharp
// 권장 파라미터
int nbNeighbors = 20;  // 이웃 수 (10-50)
float stdRatio = 2.0f; // 표준편차 비율 (1.5-3.0)

// 노이즈가 많은 경우
nbNeighbors = 30;
stdRatio = 1.5f;

// 특이점 보존 필요시
nbNeighbors = 10;
stdRatio = 3.0f;
```

### 2.3 법선 추정 최적화

```csharp
// KNN vs 반경 기반 선택
if (uniformDensity)
{
    // 균일한 밀도: KNN 사용
    EstimateNormalsKNN(30);
}
else
{
    // 가변 밀도: 반경 기반
    float avgSpacing = EstimateAverageSpacing();
    EstimateNormalsRadius(avgSpacing * 3f);
}

// 법선 방향 정렬
OrientNormals(k: 10, referencePoint: viewpointPosition);
```

---

## 3. 메모리 최적화

### 3.1 대규모 포인트 클라우드 처리

```csharp
public class ChunkedProcessor
{
    private const int CHUNK_SIZE = 1000000; // 100만 포인트
    
    public async Task<MeshData> ProcessLargePointCloud(
        string filePath, 
        PoissonSettings settings)
    {
        // 1. 포인트 클라우드 로드 (스트리밍)
        using var loader = new StreamingPointCloudLoader(filePath);
        
        // 2. 공간 분할 (Octree)
        var octree = new SpatialOctree(loader.Bounds, maxPointsPerNode: CHUNK_SIZE);
        
        await foreach (var chunk in loader.ReadChunksAsync(CHUNK_SIZE))
        {
            octree.Insert(chunk);
        }
        
        // 3. 청크별 처리
        var meshParts = new List<MeshData>();
        
        foreach (var node in octree.LeafNodes)
        {
            using var processor = new PointCloudProcessor(node.Points);
            processor.EstimateNormalsKNN(30);
            
            using var meshGen = new MeshGenerator();
            meshGen.GenerateFromPointCloud(processor, settings);
            
            meshParts.Add(meshGen.GetMeshData());
            
            // GC 압박 완화
            await Task.Yield();
        }
        
        // 4. 메쉬 병합
        return MergeMeshes(meshParts);
    }
}
```

### 3.2 Unity 메쉬 최적화

```csharp
public static class MeshOptimizer
{
    /// <summary>
    /// 대규모 메쉬 최적화 및 서브메쉬 분할
    /// </summary>
    public static Mesh[] OptimizeAndSplit(MeshData data, int maxVerticesPerMesh = 65000)
    {
        if (data.VertexCount <= maxVerticesPerMesh)
        {
            return new Mesh[] { CreateOptimizedMesh(data) };
        }
        
        // 공간 분할로 서브메쉬 생성
        var submeshes = SplitByPosition(data, maxVerticesPerMesh);
        return submeshes.Select(CreateOptimizedMesh).ToArray();
    }
    
    private static Mesh CreateOptimizedMesh(MeshData data)
    {
        var mesh = new Mesh();
        
        // 32비트 인덱스 (대규모 메쉬)
        if (data.VertexCount > 65535)
            mesh.indexFormat = IndexFormat.UInt32;
        
        mesh.vertices = data.Vertices;
        mesh.normals = data.Normals;
        mesh.triangles = data.Triangles;
        
        // 최적화
        mesh.Optimize();
        mesh.RecalculateBounds();
        
        // LOD용 정점 압축
        mesh.UploadMeshData(true); // GPU 전용 (수정 불가)
        
        return mesh;
    }
}
```

---

## 4. 품질 향상 기법

### 4.1 저밀도 영역 제거

```csharp
// 밀도 기반 필터링
void RemoveLowDensityRegions(MeshGenerator mesh, float quantile)
{
    // Poisson 재구성 후 밀도 값 활용
    // quantile: 0.0 ~ 1.0 (하위 몇 % 제거)
    mesh.RemoveLowDensityVertices(quantile);
}

// 권장 값
float quantile = 0.1f;  // 하위 10% 제거 (일반)
float quantile = 0.2f;  // 하위 20% 제거 (노이즈 많음)
float quantile = 0.05f; // 하위 5% 제거 (보존 우선)
```

### 4.2 메쉬 후처리

```csharp
public static class MeshPostProcessor
{
    /// <summary>
    /// 메쉬 스무딩 (Laplacian)
    /// </summary>
    public static void SmoothMesh(ref Vector3[] vertices, int[] triangles, int iterations = 1)
    {
        var adjacency = BuildAdjacencyList(triangles, vertices.Length);
        
        for (int iter = 0; iter < iterations; iter++)
        {
            Vector3[] newVertices = new Vector3[vertices.Length];
            
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 sum = vertices[i];
                var neighbors = adjacency[i];
                
                foreach (int n in neighbors)
                    sum += vertices[n];
                
                newVertices[i] = sum / (neighbors.Count + 1);
            }
            
            vertices = newVertices;
        }
    }
    
    /// <summary>
    /// 메쉬 단순화 (Quadric Decimation)
    /// </summary>
    public static void SimplifyMesh(MeshGenerator mesh, float reductionRatio)
    {
        int currentTriCount = mesh.TriangleCount;
        int targetTriCount = (int)(currentTriCount * (1f - reductionRatio));
        mesh.Simplify(targetTriCount);
    }
}
```

### 4.3 용접 엣지 검출 향상

```csharp
public static class EdgeDetector
{
    /// <summary>
    /// 곡률 기반 용접 엣지 검출
    /// </summary>
    public static int[] DetectWeldEdges(
        Vector3[] vertices, 
        Vector3[] normals, 
        int[] triangles,
        float curvatureThreshold = 0.5f)
    {
        var edgeCurvatures = new Dictionary<(int, int), float>();
        
        // 삼각형 순회
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v0 = triangles[i];
            int v1 = triangles[i + 1];
            int v2 = triangles[i + 2];
            
            // 엣지별 이면각 계산
            ProcessEdge(v0, v1, normals, edgeCurvatures);
            ProcessEdge(v1, v2, normals, edgeCurvatures);
            ProcessEdge(v2, v0, normals, edgeCurvatures);
        }
        
        // 높은 곡률 엣지 필터링
        var weldEdges = new List<int>();
        foreach (var kvp in edgeCurvatures)
        {
            if (kvp.Value > curvatureThreshold)
            {
                weldEdges.Add(kvp.Key.Item1);
                weldEdges.Add(kvp.Key.Item2);
            }
        }
        
        return weldEdges.ToArray();
    }
}
```

---

## 5. 로봇 IK 성능 최적화

### 5.1 해석적 IK 우선

```csharp
public JointState? OptimizedIK(Matrix4x4 target, JointState current)
{
    // 1. 해석적 IK 시도 (빠름)
    var solutions = InverseKinematics(target);
    
    if (solutions.Length > 0)
    {
        // 현재 조인트에 가장 가까운 해 선택
        return SelectNearestSolution(solutions, current);
    }
    
    // 2. 수치적 IK 폴백 (느림)
    return InverseKinematicsNumerical(target, current, maxIterations: 50);
}
```

### 5.2 경로 변환 최적화

```csharp
public class OptimizedPathConverter
{
    private RobotModel _robot;
    private JointState _lastValidJoints;
    
    /// <summary>
    /// 연속성 보장 경로 변환
    /// </summary>
    public JointState[] ConvertPathOptimized(Matrix4x4[] tcpPath)
    {
        var jointPath = new List<JointState>();
        _lastValidJoints = JointState.Zero; // 또는 홈 위치
        
        foreach (var tcp in tcpPath)
        {
            // 이전 조인트 기준 최근접 해
            var joints = _robot.InverseKinematicsNearest(tcp, _lastValidJoints);
            
            if (joints.HasValue)
            {
                // 조작성 검사
                float manip = _robot.GetManipulability(joints.Value);
                
                if (manip > 0.01f) // 특이점 회피
                {
                    jointPath.Add(joints.Value);
                    _lastValidJoints = joints.Value;
                }
                else
                {
                    // 특이점 근처: 대체 해 탐색
                    var allSolutions = _robot.InverseKinematics(tcp);
                    var bestSolution = SelectBestManipulability(allSolutions);
                    
                    if (bestSolution.HasValue)
                    {
                        jointPath.Add(bestSolution.Value);
                        _lastValidJoints = bestSolution.Value;
                    }
                }
            }
        }
        
        return jointPath.ToArray();
    }
}
```

---

## 6. 멀티스레딩

### 6.1 Unity Job System 활용

```csharp
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;

[BurstCompile]
public struct NormalEstimationJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float3> Points;
    [ReadOnly] public NativeArray<int> KNNIndices;
    public NativeArray<float3> Normals;
    
    public int K;
    
    public void Execute(int index)
    {
        // PCA로 법선 계산
        float3 centroid = float3.zero;
        
        int start = index * K;
        for (int i = 0; i < K; i++)
        {
            centroid += Points[KNNIndices[start + i]];
        }
        centroid /= K;
        
        // 공분산 행렬 계산
        float3x3 cov = float3x3.zero;
        for (int i = 0; i < K; i++)
        {
            float3 diff = Points[KNNIndices[start + i]] - centroid;
            cov += OuterProduct(diff, diff);
        }
        cov /= K;
        
        // 최소 고유벡터 = 법선
        Normals[index] = ComputeSmallestEigenvector(cov);
    }
}
```

### 6.2 비동기 처리 패턴

```csharp
public async Task<UnityEngine.Mesh> ProcessAsync(
    PointCloudData data,
    PoissonSettings settings,
    CancellationToken ct,
    IProgress<float> progress)
{
    // 1. 전처리 (백그라운드)
    var processedData = await Task.Run(() =>
    {
        ct.ThrowIfCancellationRequested();
        progress?.Report(0.1f);
        
        using var processor = new PointCloudProcessor(data);
        processor.DownsampleVoxel(0.002f);
        progress?.Report(0.2f);
        
        processor.RemoveOutliers(20, 2.0f);
        progress?.Report(0.3f);
        
        processor.EstimateNormalsKNN(30);
        progress?.Report(0.4f);
        
        processor.OrientNormals(10);
        progress?.Report(0.5f);
        
        return processor.GetData();
    }, ct);
    
    // 2. 메쉬 생성 (백그라운드)
    MeshData meshData = await Task.Run(() =>
    {
        ct.ThrowIfCancellationRequested();
        progress?.Report(0.6f);
        
        using var processor = new PointCloudProcessor(processedData);
        using var meshGen = new MeshGenerator();
        meshGen.GenerateFromPointCloud(processor, settings);
        
        progress?.Report(0.9f);
        return meshGen.GetMeshData();
    }, ct);
    
    // 3. Unity 메쉬 생성 (메인 스레드)
    progress?.Report(1.0f);
    return CreateUnityMesh(meshData);
}
```

---

## 7. 프로파일링 체크리스트

### 7.1 성능 측정 포인트

| 단계 | 측정 항목 | 목표 |
|------|-----------|------|
| 파일 로드 | 로드 시간 | <1초/100만 포인트 |
| 다운샘플링 | 처리 시간 | <0.5초 |
| 법선 추정 | 처리 시간 | <2초/10만 포인트 |
| Poisson | 처리 시간 | <10초 (depth 8) |
| Unity 메쉬 | 생성 시간 | <0.5초 |
| IK 계산 | 단일 IK | <1ms |
| 경로 변환 | 1000 포인트 | <1초 |

### 7.2 메모리 프로파일링

```csharp
public static class MemoryProfiler
{
    public static void LogMemoryUsage(string tag)
    {
        #if UNITY_EDITOR
        long totalMemory = Profiler.GetTotalAllocatedMemoryLong();
        long reservedMemory = Profiler.GetTotalReservedMemoryLong();
        
        Debug.Log($"[{tag}] Allocated: {totalMemory / 1024 / 1024}MB, " +
                  $"Reserved: {reservedMemory / 1024 / 1024}MB");
        #endif
    }
}
```

---

## 8. 추천 설정 프리셋

### 8.1 빠른 프리뷰
```csharp
var settings = new PoissonSettings
{
    Depth = 6,
    Scale = 1.1f,
    LinearFit = false,
    DensityThreshold = 0.2f
};
float voxelSize = 0.005f; // 5mm
```

### 8.2 일반 용도 (권장)
```csharp
var settings = new PoissonSettings
{
    Depth = 8,
    Scale = 1.1f,
    LinearFit = false,
    DensityThreshold = 0.1f
};
float voxelSize = 0.002f; // 2mm
```

### 8.3 고품질 용접
```csharp
var settings = new PoissonSettings
{
    Depth = 10,
    Scale = 1.05f,
    LinearFit = true,
    DensityThreshold = 0.05f
};
float voxelSize = 0.001f; // 1mm
```

---

*다음: 06_Validation_Testing.md*
