# SMR 용접 로봇용 고정밀 메쉬 생성 시스템 - 연구 요약 Part 1

## 📖 문서 정보
- **작성일**: 2026-01-07
- **범위**: Open3D Poisson, Unity P/Invoke, 메모리 관리
- **출처 수**: 50+ 웹사이트 분석

---

## 1. Open3D Poisson Surface Reconstruction

### 1.1 알고리즘 개요

Poisson Surface Reconstruction은 포인트클라우드에서 고품질 삼각형 메쉬를 생성하는 implicit surface 기법입니다.

**핵심 원리:**
- 입력 포인트의 법선 벡터를 gradient field로 해석
- Poisson 방정식 (∇²χ = ∇·V) 풀이
- Indicator function의 isosurface 추출

**장점:**
- 물 샐틈 없는(watertight) 메쉬 생성
- 노이즈에 강건
- 매끄러운 표면 재구성
- 빈 영역 채움

### 1.2 Open3D API

```python
mesh, densities = o3d.geometry.TriangleMesh.create_from_point_cloud_poisson(
    pcd,
    depth=8,           # Octree 깊이 (해상도)
    width=0,           # 무시됨 (depth 우선)
    scale=1.1,         # 재구성 영역 확장 비율
    linear_fit=False,  # 선형 보간 사용 여부
    n_threads=-1       # CPU 스레드 수 (자동)
)
```

### 1.3 파라미터 상세 분석

#### depth (Octree 깊이)
- **범위**: 6-12 (일반적 8-10)
- **영향**: 해상도 = 2^depth voxels per dimension
- **용접 응용 권장**: 8-10 (표면 디테일 보존)
- **메모리 사용**: depth 1 증가 → 메모리 8배 증가

| depth | 해상도 | 대략적 정점 수 | 메모리(796K pts) |
|-------|--------|---------------|-----------------|
| 6 | 64³ | ~20K | ~50MB |
| 7 | 128³ | ~80K | ~100MB |
| 8 | 256³ | ~300K | ~200MB |
| 9 | 512³ | ~560K | ~600MB |
| 10 | 1024³ | ~2M | ~2GB |

#### scale (영역 확장)
- **기본값**: 1.1
- **범위**: 1.0-2.0
- **용도**: 경계 아티팩트 방지
- **노이즈 많은 데이터**: 1.2-1.5 권장

#### linear_fit
- **기본값**: False
- **True 권장**: 입력 포인트에 더 가깝게 피팅
- **용접 응용**: True 권장 (정확한 표면 위치 중요)

### 1.4 법선 추정 (필수 전처리)

```python
# 법선 추정
pcd.estimate_normals(
    search_param=o3d.geometry.KDTreeSearchParamHybrid(
        radius=0.1,    # 검색 반경
        max_nn=30      # 최대 이웃 수
    )
)

# 법선 방향 일관성
pcd.orient_normals_consistent_tangent_plane(k=15)
```

**중요 고려사항:**
- 법선이 모두 바깥쪽을 향해야 함
- 잘못된 법선 → 표면 반전/구멍
- radius는 포인트 간격의 2-3배 권장

### 1.5 저밀도 영역 필터링

```python
# density 기반 필터링
vertices_to_remove = densities < np.quantile(densities, 0.01)
mesh.remove_vertices_by_mask(vertices_to_remove)
```

### 1.6 성능 벤치마크 (796,825 포인트, depth=9)

| 단계 | 시간 | 메모리 |
|------|------|--------|
| Kernel density | 0.09s | 371MB |
| Normal field | 0.62s | 468MB |
| Tree finalize | 0.51s | 594MB |
| FEM constraints | 1.47s | 576MB |
| Linear solve | 1.94s | 593MB |
| **총계** | ~5.5s | ~600MB |

**출력**: 563,112 정점, 1,126,072 삼각형

---

## 2. 대안 알고리즘 비교

### 2.1 Ball Pivoting Algorithm (BPA)

```python
radii = [0.005, 0.01, 0.02, 0.04]
mesh = o3d.geometry.TriangleMesh.create_from_point_cloud_ball_pivoting(
    pcd, o3d.utility.DoubleVector(radii)
)
```

### 2.2 알고리즘 선택 기준

| 기준 | Poisson | BPA | Alpha |
|------|---------|-----|-------|
| 품질 | ★★★★★ | ★★★ | ★★ |
| 속도 | ★★★ | ★★★★ | ★★★★★ |
| 노이즈 강건성 | ★★★★★ | ★★ | ★★ |
| 빈 영역 처리 | ★★★★★ | ★ | ★ |

**용접 응용 결론**: Poisson 권장

---

## 3. Unity Native Plugin (P/Invoke)

### 3.1 기본 구조

**C++ 측 (DLL Export):**
```cpp
#ifdef _WIN32
    #define EXPORT_API __declspec(dllexport)
#else
    #define EXPORT_API __attribute__((visibility("default")))
#endif

extern "C" {
    EXPORT_API int ProcessPointCloud(
        float* points, int count,
        float** outVertices, int* outVertexCount
    );
    EXPORT_API void FreeMemory(void* ptr);
}
```

**C# 측 (DllImport):**
```csharp
[DllImport("PointToMesh", CallingConvention = CallingConvention.Cdecl)]
private static extern int ProcessPointCloud(
    float[] points, int count,
    out IntPtr outVertices, out int outVertexCount
);
```

### 3.2 Blittable Types (변환 없는 타입)

직접 메모리 복사 가능 (최고 성능):
- `byte`, `sbyte`, `short`, `ushort`
- `int`, `uint`, `long`, `ulong`
- `float`, `double`
- `IntPtr`, `UIntPtr`

### 3.3 구조체 마샬링

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct NativeVertex {
    public float x, y, z;      // 위치
    public float nx, ny, nz;   // 법선
}
```

### 3.4 대용량 데이터 전송 패턴

```csharp
// 방법 1: 고정 버퍼 (GC 방지)
public unsafe void ProcessLargeData(Vector3[] points) {
    fixed (Vector3* ptr = points) {
        NativeProcess((float*)ptr, points.Length);
    }
}

// 방법 2: GCHandle 사용
GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
try {
    IntPtr ptr = handle.AddrOfPinnedObject();
    NativeProcess(ptr, data.Length);
} finally {
    handle.Free();
}
```

### 3.5 플랫폼별 고려사항

| 플랫폼 | DLL 이름 | 비고 |
|--------|----------|------|
| Windows | plugin.dll | x64/x86 구분 |
| macOS | libplugin.dylib | Universal Binary |
| Linux | libplugin.so | -fPIC 필요 |
| iOS | __Internal | 정적 링크 |

---

## 4. 메모리 관리 전략

### 4.1 기본 원칙

1. **할당자 일관성**: 네이티브에서 할당 → 네이티브에서 해제
2. **수명 명확화**: 언제 메모리가 해제되는지 문서화
3. **GC 방지**: 대용량 데이터는 고정(pinned) 메모리 사용

### 4.2 핸들 기반 패턴 (권장)

```csharp
public class NativeMesh : IDisposable {
    private IntPtr _handle;
    private bool _disposed;
    
    public NativeMesh() {
        _handle = NativeMethods.CreateMeshHandle();
    }
    
    public void Dispose() {
        if (!_disposed && _handle != IntPtr.Zero) {
            NativeMethods.DestroyMeshHandle(_handle);
            _handle = IntPtr.Zero;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
    
    ~NativeMesh() => Dispose();
}
```

---

## 5. 출처 목록 (Part 1)

1. Open3D 공식 문서 - Surface Reconstruction
2. Open3D GitHub - Poisson Implementation
3. Kazhdan et al. - Screened Poisson Surface Reconstruction (2013)
4. Unity Manual - Native Plugins
5. Microsoft Docs - P/Invoke
6. Baracoda DevBlog - P/Invoke Guide
7. Long Qian Blog - Unity Native Programming
8. Jackson Dunstan - IL2CPP P/Invoke Internals
9. Eric Eastwood - Unity DLL Guide
10. VR-Modeling - C#/C++ Interface Patterns
11. Open3D Discourse - Parameter Tuning
12. Stack Overflow - P/Invoke Best Practices
13. Unity Forum - Native Memory Management
14. GitHub Issues - Open3D Poisson Parameters
15. Point Cloud Library Documentation

---

*다음 문서: 00_Research_Summary_Part2.md (로봇 통신 프로토콜)*
