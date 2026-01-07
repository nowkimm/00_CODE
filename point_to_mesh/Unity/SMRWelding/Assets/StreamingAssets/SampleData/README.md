# Sample Data 폴더

SMR 용접 시스템 테스트를 위한 샘플 포인트 클라우드 데이터입니다.

## 포함된 파일

### t_joint_seam.ply
- **용도**: T-조인트 용접 이음부 테스트
- **포인트 수**: 100개
- **형상**: T자 형태의 용접선
- **크기**: 약 200mm x 100mm x 100mm
- **특징**: 두 평면이 만나는 직선형 용접 이음

### pipe_seam.ply
- **용도**: 파이프 원주 용접 테스트
- **포인트 수**: 72개
- **형상**: 원형 파이프 단면
- **직경**: 100mm
- **특징**: 원주 방향 용접 이음

## 파일 형식

PLY (Polygon File Format / Stanford Triangle Format)
- ASCII 포맷
- 정점 위치 (x, y, z)
- 법선 벡터 (nx, ny, nz)
- RGB 컬러 (red, green, blue)

## 사용 방법

### Unity 에디터에서
```csharp
// SampleDataGenerator 사용
string path = Path.Combine(Application.streamingAssetsPath, "SampleData/t_joint_seam.ply");
var points = FileUtilities.LoadPointCloudPLY(path);
```

### 메뉴에서
1. `SMR Welding > Generate Sample Data` 실행
2. 샘플 유형 선택
3. Generate 클릭

### 시뮬레이션 모드에서
SimulationMode는 자체 샘플 데이터를 생성하므로 이 파일들 없이도 테스트 가능합니다.

## 좌표계

- **단위**: 미터 (m)
- **X축**: 용접 진행 방향
- **Y축**: 위쪽 방향
- **Z축**: 용접선에 수직

## 커스텀 데이터 추가

### PLY 파일 요구사항
1. ASCII 포맷 권장
2. 최소 정점 위치 (x, y, z) 필요
3. 법선 정보 권장 (nx, ny, nz)
4. 컬러 정보 선택사항 (red, green, blue)

### 파일 예시
```
ply
format ascii 1.0
element vertex 3
property float x
property float y
property float z
property float nx
property float ny
property float nz
end_header
0.0 0.0 0.0 0.0 1.0 0.0
0.1 0.0 0.0 0.0 1.0 0.0
0.2 0.0 0.0 0.0 1.0 0.0
```

## 실제 스캐너 데이터

실제 3D 스캐너 데이터를 사용할 경우:
1. PCD 또는 PLY 형식으로 변환
2. 이 폴더에 복사
3. FileUtilities로 로드

지원 형식:
- PLY (ASCII/Binary)
- PCD (네이티브 DLL 필요)
- XYZ (텍스트)
