# Native Plugin Placeholder

이 폴더에 빌드된 네이티브 DLL이 위치합니다.

## 필요한 파일
- `smr_welding_native.dll` - SMR 용접 시스템 네이티브 플러그인

## 빌드 방법
프로젝트 루트에서 다음 명령 실행:

```powershell
.\build_native.ps1
```

또는

```cmd
build_native.bat
```

## 시뮬레이션 모드
DLL 없이도 `SimulationMode`를 통해 기본 기능 테스트가 가능합니다.

## 의존성
빌드를 위해 다음이 필요합니다:
- Visual Studio 2022 (C++ 데스크톱 개발)
- CMake 3.20+
- vcpkg (Open3D, Eigen3)

자세한 내용은 `Docs/08_Build_Environment_Setup.md` 참조
