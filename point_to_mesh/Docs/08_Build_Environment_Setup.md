# SMR 용접 시스템 - 빌드 환경 설정 가이드

## 개요
이 문서는 SMR 용접 로봇 시스템의 C++ 네이티브 플러그인을 빌드하기 위한 환경 설정 방법을 설명합니다.

---

## 필수 도구

### 1. Visual Studio 2022
**다운로드**: https://visualstudio.microsoft.com/downloads/

**설치 시 필수 구성 요소**:
- C++를 사용한 데스크톱 개발
- Windows 10/11 SDK
- C++ CMake 도구

### 2. CMake (3.20 이상)
**다운로드**: https://cmake.org/download/

**설치 방법**:
```powershell
# winget 사용
winget install Kitware.CMake

# 또는 chocolatey 사용
choco install cmake
```

**설치 확인**:
```powershell
cmake --version
```

### 3. vcpkg (패키지 관리자)
**설치 방법**:
```powershell
# 1. vcpkg 클론
cd C:\
git clone https://github.com/Microsoft/vcpkg.git

# 2. 부트스트랩 실행
cd vcpkg
.\bootstrap-vcpkg.bat

# 3. 환경 변수 설정 (관리자 권한)
[Environment]::SetEnvironmentVariable("VCPKG_ROOT", "C:\vcpkg", "Machine")
[Environment]::SetEnvironmentVariable("Path", $env:Path + ";C:\vcpkg", "Machine")

# 4. Visual Studio 통합
.\vcpkg integrate install
```

**설치 확인**:
```powershell
vcpkg version
```

---

## 의존성 설치

### Open3D 설치
```powershell
vcpkg install open3d:x64-windows
```

**예상 시간**: 30-60분 (첫 설치 시)

### Eigen3 설치
```powershell
vcpkg install eigen3:x64-windows
```

**예상 시간**: 5-10분

### 전체 의존성 한번에 설치
```powershell
vcpkg install open3d:x64-windows eigen3:x64-windows
```

---

## 빌드 방법

### 방법 1: PowerShell 스크립트 사용 (권장)
```powershell
cd D:\00_CODE\point_to_mesh
.\build_native.ps1
```

### 방법 2: 배치 파일 사용
```cmd
cd D:\00_CODE\point_to_mesh
build_native.bat
```

### 방법 3: 수동 빌드
```powershell
# 1. 빌드 디렉토리 생성
cd D:\00_CODE\point_to_mesh\Native
mkdir build
cd build

# 2. CMake 구성
cmake .. -G "Visual Studio 17 2022" -A x64 `
    -DCMAKE_TOOLCHAIN_FILE="C:/vcpkg/scripts/buildsystems/vcpkg.cmake"

# 3. 빌드
cmake --build . --config Release

# 4. DLL 복사
copy Release\smr_welding_native.dll ..\..\Unity\SMRWelding\Assets\Plugins\x86_64\
```

---

## 빌드 결과물

### 생성되는 파일
```
Native/build/Release/
├── smr_welding_native.dll    # 메인 DLL
├── smr_welding_native.lib    # 정적 라이브러리
└── smr_welding_native.pdb    # 디버그 심볼
```

### Unity 플러그인 위치
```
Unity/SMRWelding/Assets/Plugins/x86_64/
└── smr_welding_native.dll
```

---

## 문제 해결

### CMake를 찾을 수 없음
```
오류: 'cmake'은(는) 내부 또는 외부 명령...이 아닙니다
```

**해결**: CMake 설치 후 PATH 환경 변수에 추가
```powershell
# PATH 확인
$env:Path -split ';' | Where-Object { $_ -like '*cmake*' }

# 수동 추가 (일시적)
$env:Path += ";C:\Program Files\CMake\bin"
```

### vcpkg를 찾을 수 없음
```
오류: 'vcpkg'은(는) 내부 또는 외부 명령...이 아닙니다
```

**해결**: vcpkg 경로를 PATH에 추가
```powershell
$env:Path += ";C:\vcpkg"
```

### Open3D 빌드 실패
```
오류: Could not find Open3D
```

**해결**:
1. vcpkg에서 Open3D 재설치
```powershell
vcpkg remove open3d:x64-windows
vcpkg install open3d:x64-windows
```

2. CMake 캐시 삭제 후 재빌드
```powershell
cd Native/build
Remove-Item -Recurse -Force *
cmake ..
```

### Visual Studio 버전 문제
```
오류: Generator "Visual Studio 17 2022" not found
```

**해결**: 설치된 VS 버전에 맞게 수정
- VS 2019: `"Visual Studio 16 2019"`
- VS 2022: `"Visual Studio 17 2022"`

---

## 대안: 시뮬레이션 모드

C++ 빌드 없이 Unity에서 테스트하려면 **시뮬레이션 모드**를 사용하세요:

1. Unity 프로젝트 열기
2. `SMR Welding > Create Demo Scene` 실행
3. Play 버튼 클릭

시뮬레이션 모드는 네이티브 DLL 없이 기본적인 파이프라인을 테스트할 수 있습니다.

---

## 버전 호환성

| 도구 | 최소 버전 | 권장 버전 |
|------|----------|----------|
| Visual Studio | 2019 | 2022 |
| CMake | 3.20 | 3.28+ |
| vcpkg | 2023.01 | 최신 |
| Open3D | 0.17 | 0.18+ |
| Eigen | 3.4 | 3.4+ |

---

## 참고 링크

- [Open3D 공식 문서](http://www.open3d.org/docs/)
- [vcpkg 시작 가이드](https://vcpkg.io/en/getting-started)
- [CMake 튜토리얼](https://cmake.org/cmake/help/latest/guide/tutorial/)
- [Visual Studio C++ 설치](https://docs.microsoft.com/cpp/build/vscpp-step-0-installation)

---

*최종 업데이트: 2026-01-07*
