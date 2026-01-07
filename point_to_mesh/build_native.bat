@echo off
REM =============================================================================
REM build_native.bat - Build Native C++ Plugin for SMR Welding
REM =============================================================================

echo ============================================
echo SMR Welding Native Plugin Build Script
echo ============================================

REM Configuration
set BUILD_TYPE=Release
set BUILD_DIR=build
set VCPKG_ROOT=C:\vcpkg

REM Check for Visual Studio
where cl >nul 2>nul
if %errorlevel% neq 0 (
    echo Visual Studio compiler not found in PATH.
    echo Please run this script from Developer Command Prompt.
    echo Or run: "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"
    pause
    exit /b 1
)

REM Check for CMake
where cmake >nul 2>nul
if %errorlevel% neq 0 (
    echo CMake not found. Please install CMake and add to PATH.
    pause
    exit /b 1
)

REM Navigate to Native directory
cd /d %~dp0Native

REM Create build directory
if not exist %BUILD_DIR% mkdir %BUILD_DIR%
cd %BUILD_DIR%

echo.
echo Configuring CMake...
echo.

REM Configure with CMake
cmake .. ^
    -G "Visual Studio 17 2022" ^
    -A x64 ^
    -DCMAKE_BUILD_TYPE=%BUILD_TYPE% ^
    -DCMAKE_TOOLCHAIN_FILE=%VCPKG_ROOT%\scripts\buildsystems\vcpkg.cmake

if %errorlevel% neq 0 (
    echo CMake configuration failed!
    pause
    exit /b 1
)

echo.
echo Building...
echo.

REM Build
cmake --build . --config %BUILD_TYPE% --parallel

if %errorlevel% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo.
echo Copying DLL to Unity Plugins folder...
echo.

REM Copy DLL to Unity Plugins
set UNITY_PLUGINS=%~dp0Unity\SMRWelding\Assets\Plugins\x86_64
if not exist "%UNITY_PLUGINS%" mkdir "%UNITY_PLUGINS%"

copy /Y "%BUILD_TYPE%\smr_welding_native.dll" "%UNITY_PLUGINS%\"
copy /Y "%BUILD_TYPE%\smr_welding_native.lib" "%UNITY_PLUGINS%\"

if %errorlevel% neq 0 (
    echo Failed to copy files!
    pause
    exit /b 1
)

echo.
echo ============================================
echo Build successful!
echo DLL copied to: %UNITY_PLUGINS%
echo ============================================
echo.

pause
