# Materials 폴더

이 폴더에는 SMR 용접 시스템에서 사용하는 머티리얼이 위치합니다.

## 권장 머티리얼

### 포인트 클라우드용
- `PointCloud_Default.mat` - 기본 포인트 렌더링
- `PointCloud_Height.mat` - 높이 기반 컬러맵

### 메쉬용
- `Mesh_Default.mat` - 기본 메쉬 렌더링
- `Mesh_Wireframe.mat` - 와이어프레임 오버레이
- `Mesh_Transparent.mat` - 반투명 메쉬

### 경로용
- `Path_Weld.mat` - 용접 경로 (빨간색)
- `Path_Move.mat` - 이동 경로 (파란색)
- `Path_Weaving.mat` - 위빙 패턴 (노란색)

### 로봇용
- `Robot_Joint.mat` - 관절 (회색)
- `Robot_Link.mat` - 링크 (진회색)
- `Robot_Tool.mat` - 용접 토치 (주황색)

## 생성 방법
Unity 에디터에서:
1. Project 창에서 이 폴더 우클릭
2. Create > Material
3. Shader 선택 (Standard 또는 URP/Lit)
4. 색상 및 속성 설정

또는 `SMR Welding > Setup Welding Scene` 메뉴로 자동 생성
