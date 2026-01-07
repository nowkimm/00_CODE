# Scenes 폴더

이 폴더에는 SMR 용접 시스템의 Unity 씬 파일이 위치합니다.

## 권장 씬 구성

### WeldingMain.unity
메인 작업 씬
- 전체 용접 파이프라인 UI
- 포인트 클라우드 로딩
- 메쉬 생성 및 경로 계획
- 로봇 시뮬레이션

### WeldingDemo.unity
데모 및 테스트 씬
- 샘플 데이터 자동 생성
- 빠른 파이프라인 테스트
- 시뮬레이션 모드 확인

### RobotTest.unity
로봇 테스트 전용 씬
- 로봇 모델 시각화
- FK/IK 테스트
- 관절 제어 UI

## 씬 생성 방법

### 자동 생성 (권장)
```
Unity 메뉴 > SMR Welding > Create Demo Scene
```
또는
```
Unity 메뉴 > SMR Welding > Setup Welding Scene
```

### 수동 생성
1. File > New Scene
2. 필요한 게임오브젝트 추가
3. 컴포넌트 구성
4. File > Save As로 이 폴더에 저장

## 씬 구조 예시

```
WeldingMain.unity
├── Main Camera
├── Directional Light
├── EventSystem
├── WeldingSystem
│   ├── WeldingPipelineController
│   ├── PointCloudVisualizer
│   ├── PathVisualizer
│   └── RobotVisualizer
├── UI Canvas
│   └── WeldingUI
└── Environment
    └── Ground Plane
```

## 빌드 설정
씬을 빌드에 포함하려면:
1. File > Build Settings
2. Add Open Scenes 클릭
3. 빌드 순서 조정 (Main 씬이 첫 번째)
