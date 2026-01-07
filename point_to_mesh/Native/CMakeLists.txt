# =============================================================================
# SMR Welding Native Plugin - CMake Build Configuration
# =============================================================================
cmake_minimum_required(VERSION 3.15)
project(SMRWeldingNative VERSION 1.0.0 LANGUAGES CXX)

# =============================================================================
# Build Options
# =============================================================================
option(SMR_BUILD_SHARED "Build shared library" ON)
option(SMR_BUILD_TESTS "Build unit tests" OFF)
option(SMR_USE_OPEN3D "Use Open3D for point cloud processing" OFF)
option(SMR_USE_EIGEN "Use Eigen for linear algebra" OFF)

# =============================================================================
# Compiler Settings
# =============================================================================
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Position Independent Code (required for shared libraries)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Output directories
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# =============================================================================
# Platform-specific settings
# =============================================================================
if(WIN32)
    add_definitions(-DSMR_PLATFORM_WINDOWS)
    add_definitions(-D_USE_MATH_DEFINES)
    add_definitions(-DNOMINMAX)
    
    # Windows-specific compiler flags
    if(MSVC)
        add_compile_options(/W4 /WX- /MP)
        add_compile_options(/wd4100 /wd4244 /wd4267)  # Disable some warnings
        
        # Release optimizations
        set(CMAKE_CXX_FLAGS_RELEASE "${CMAKE_CXX_FLAGS_RELEASE} /O2 /Ob2 /DNDEBUG")
        
        # Debug settings
        set(CMAKE_CXX_FLAGS_DEBUG "${CMAKE_CXX_FLAGS_DEBUG} /Od /Zi")
    endif()
elseif(APPLE)
    add_definitions(-DSMR_PLATFORM_MACOS)
    set(CMAKE_OSX_ARCHITECTURES "x86_64;arm64")
    add_compile_options(-Wall -Wextra -Wpedantic)
elseif(UNIX)
    add_definitions(-DSMR_PLATFORM_LINUX)
    add_compile_options(-Wall -Wextra -Wpedantic)
    add_compile_options(-fvisibility=hidden)
endif()

# =============================================================================
# Source Files
# =============================================================================
set(SMR_HEADERS
    include/smr_welding_api.h
)

set(SMR_SOURCES
    src/point_cloud.cpp
    src/mesh_generator.cpp
    src/robot_kinematics.cpp
    src/path_planner.cpp
)

# =============================================================================
# Main Library Target
# =============================================================================
if(SMR_BUILD_SHARED)
    add_library(SMRWeldingNative SHARED ${SMR_SOURCES} ${SMR_HEADERS})
    target_compile_definitions(SMRWeldingNative PRIVATE SMR_BUILD_DLL)
else()
    add_library(SMRWeldingNative STATIC ${SMR_SOURCES} ${SMR_HEADERS})
endif()

target_include_directories(SMRWeldingNative
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<INSTALL_INTERFACE:include>
    PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/src
)

# Set output name
set_target_properties(SMRWeldingNative PROPERTIES
    OUTPUT_NAME "smr_welding"
    VERSION ${PROJECT_VERSION}
    SOVERSION 1
)

# =============================================================================
# Optional Dependencies
# =============================================================================
if(SMR_USE_OPEN3D)
    find_package(Open3D REQUIRED)
    target_link_libraries(SMRWeldingNative PRIVATE Open3D::Open3D)
    target_compile_definitions(SMRWeldingNative PRIVATE SMR_HAS_OPEN3D)
endif()

if(SMR_USE_EIGEN)
    find_package(Eigen3 3.4 REQUIRED NO_MODULE)
    target_link_libraries(SMRWeldingNative PRIVATE Eigen3::Eigen)
    target_compile_definitions(SMRWeldingNative PRIVATE SMR_HAS_EIGEN)
endif()

# =============================================================================
# Installation
# =============================================================================
include(GNUInstallDirs)

install(TARGETS SMRWeldingNative
    EXPORT SMRWeldingNativeTargets
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
    ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
    INCLUDES DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
)

install(FILES ${SMR_HEADERS}
    DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}/smr
)

# =============================================================================
# Unity Plugin Copy Target
# =============================================================================
set(UNITY_PLUGIN_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../Unity/SMRWelding/Assets/Plugins" CACHE PATH "Unity Plugins directory")

add_custom_target(copy_to_unity
    COMMAND ${CMAKE_COMMAND} -E make_directory "${UNITY_PLUGIN_DIR}/x86_64"
    COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:SMRWeldingNative> "${UNITY_PLUGIN_DIR}/x86_64/"
    DEPENDS SMRWeldingNative
    COMMENT "Copying native plugin to Unity project"
)

# =============================================================================
# Unit Tests (Optional)
# =============================================================================
if(SMR_BUILD_TESTS)
    enable_testing()
    
    add_executable(smr_tests
        tests/test_point_cloud.cpp
        tests/test_mesh.cpp
        tests/test_robot.cpp
        tests/test_path.cpp
    )
    
    target_link_libraries(smr_tests PRIVATE SMRWeldingNative)
    target_include_directories(smr_tests PRIVATE include)
    
    add_test(NAME SMRTests COMMAND smr_tests)
endif()

# =============================================================================
# Build Summary
# =============================================================================
message(STATUS "")
message(STATUS "=== SMR Welding Native Plugin Configuration ===")
message(STATUS "Version:        ${PROJECT_VERSION}")
message(STATUS "Build type:     ${CMAKE_BUILD_TYPE}")
message(STATUS "Shared library: ${SMR_BUILD_SHARED}")
message(STATUS "Use Open3D:     ${SMR_USE_OPEN3D}")
message(STATUS "Use Eigen:      ${SMR_USE_EIGEN}")
message(STATUS "Build tests:    ${SMR_BUILD_TESTS}")
message(STATUS "Install prefix: ${CMAKE_INSTALL_PREFIX}")
message(STATUS "Unity plugins:  ${UNITY_PLUGIN_DIR}")
message(STATUS "================================================")
message(STATUS "")
