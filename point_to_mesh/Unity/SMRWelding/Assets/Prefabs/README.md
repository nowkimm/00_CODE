# Prefabs 폴더

이 폴더에는 SMR 용접 시스템의 재사용 가능한 프리팹이 위치합니다.

## 권장 프리팹 구조

### WeldingSystem.prefab
```
WeldingSystem (GameObject)
├── WeldingPipelineController
├── PointCloudVisualizer
├── PathVisualizer
├── RobotVisualizer
└── WeldingUI (Canvas)
```

### RobotArm.prefab
```
RobotArm (GameObject)
├── Base
├── Link1
├── Link2
├── Link3
├── Link4
├── Link5
├── Link6
└── Tool
    └── WeldingTorch
```

### PointCloudRenderer.prefab
```
PointCloudRenderer (GameObject)
├── PointCloudVisualizer
└── LODGroup
```

## 생성 방법

### 자동 생성
1. `SMR Welding > Setup Welding Scene` 실행
2. Hierarchy에서 생성된 오브젝트 선택
3. Project 창의 Prefabs 폴더로 드래그

### 수동 생성
1. Hierarchy에서 오브젝트 구성
2. 필요한 컴포넌트 추가
3. Prefabs 폴더로 드래그

## 프리팹 사용

```csharp
// 코드에서 프리팹 인스턴스화
var prefab = Resources.Load<GameObject>("Prefabs/WeldingSystem");
var instance = Instantiate(prefab);
```

또는 Inspector에서 직접 참조
