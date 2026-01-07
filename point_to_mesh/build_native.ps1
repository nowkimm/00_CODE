# =============================================================================
# build_native.ps1 - Build Native C++ Plugin for SMR Welding (PowerShell)
# =============================================================================

param(
    [string]$BuildType = "Release",
    [string]$VcpkgRoot = "C:\vcpkg",
    [switch]$Clean,
    [switch]$NoCopy
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "SMR Welding Native Plugin Build Script" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Configuration
$NativeDir = Join-Path $ScriptDir "Native"
$BuildDir = Join-Path $NativeDir "build"
$UnityPlugins = Join-Path $ScriptDir "Unity\SMRWelding\Assets\Plugins\x86_64"

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

# Check CMake
$cmake = Get-Command cmake -ErrorAction SilentlyContinue
if (-not $cmake) {
    Write-Host "ERROR: CMake not found. Please install CMake." -ForegroundColor Red
    exit 1
}
Write-Host "  CMake: $($cmake.Source)" -ForegroundColor Green

# Check vcpkg
if (-not (Test-Path $VcpkgRoot)) {
    Write-Host "WARNING: vcpkg not found at $VcpkgRoot" -ForegroundColor Yellow
    Write-Host "  Dependencies (Open3D, Eigen) must be installed manually" -ForegroundColor Yellow
}
else {
    Write-Host "  vcpkg: $VcpkgRoot" -ForegroundColor Green
}

# Clean if requested
if ($Clean -and (Test-Path $BuildDir)) {
    Write-Host ""
    Write-Host "Cleaning build directory..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $BuildDir
}

# Create build directory
if (-not (Test-Path $BuildDir)) {
    New-Item -ItemType Directory -Path $BuildDir | Out-Null
}

# Configure
Write-Host ""
Write-Host "Configuring CMake..." -ForegroundColor Yellow
Push-Location $BuildDir

$cmakeArgs = @(
    "..",
    "-G", "Visual Studio 17 2022",
    "-A", "x64",
    "-DCMAKE_BUILD_TYPE=$BuildType"
)

if (Test-Path $VcpkgRoot) {
    $cmakeArgs += "-DCMAKE_TOOLCHAIN_FILE=$VcpkgRoot\scripts\buildsystems\vcpkg.cmake"
}

& cmake @cmakeArgs

if ($LASTEXITCODE -ne 0) {
    Pop-Location
    Write-Host "ERROR: CMake configuration failed!" -ForegroundColor Red
    exit 1
}

# Build
Write-Host ""
Write-Host "Building ($BuildType)..." -ForegroundColor Yellow

& cmake --build . --config $BuildType --parallel

if ($LASTEXITCODE -ne 0) {
    Pop-Location
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    exit 1
}

Pop-Location

# Copy to Unity
if (-not $NoCopy) {
    Write-Host ""
    Write-Host "Copying to Unity Plugins..." -ForegroundColor Yellow
    
    if (-not (Test-Path $UnityPlugins)) {
        New-Item -ItemType Directory -Path $UnityPlugins -Force | Out-Null
    }
    
    $dllPath = Join-Path $BuildDir "$BuildType\smr_welding_native.dll"
    $libPath = Join-Path $BuildDir "$BuildType\smr_welding_native.lib"
    
    if (Test-Path $dllPath) {
        Copy-Item $dllPath $UnityPlugins -Force
        Write-Host "  Copied: smr_welding_native.dll" -ForegroundColor Green
    }
    
    if (Test-Path $libPath) {
        Copy-Item $libPath $UnityPlugins -Force
        Write-Host "  Copied: smr_welding_native.lib" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Build Successful!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output: $UnityPlugins" -ForegroundColor White
Write-Host ""
