/**
 * @file smr_welding_api.h
 * @brief SMR Welding Robot Mesh Generation System - Native Plugin API
 * @version 1.0.0
 * @date 2025-01-07
 * 
 * Unity C# P/Invoke를 위한 C 스타일 API
 * 모든 함수는 extern "C"로 내보내기
 */

#ifndef SMR_WELDING_API_H
#define SMR_WELDING_API_H

#include <stdint.h>
#include <stdbool.h>

// Platform-specific export macros
#ifdef _WIN32
    #ifdef SMR_WELDING_EXPORTS
        #define SMR_API __declspec(dllexport)
    #else
        #define SMR_API __declspec(dllimport)
    #endif
#else
    #define SMR_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

// =============================================================================
// Type Definitions
// =============================================================================

/// Opaque handle types for native objects
typedef void* PointCloudHandle;
typedef void* MeshHandle;
typedef void* RobotHandle;
typedef void* PathHandle;

/// Error codes
typedef enum {
    SMR_SUCCESS = 0,
    SMR_ERROR_INVALID_HANDLE = -1,
    SMR_ERROR_INVALID_PARAMETER = -2,
    SMR_ERROR_FILE_NOT_FOUND = -3,
    SMR_ERROR_FILE_FORMAT = -4,
    SMR_ERROR_MEMORY_ALLOCATION = -5,
    SMR_ERROR_COMPUTATION_FAILED = -6,
    SMR_ERROR_NO_SOLUTION = -7,
    SMR_ERROR_JOINT_LIMITS = -8,
    SMR_ERROR_SINGULARITY = -9,
    SMR_ERROR_NOT_IMPLEMENTED = -99
} SMRErrorCode;

/// Robot type presets
typedef enum {
    ROBOT_UR5 = 0,
    ROBOT_UR10 = 1,
    ROBOT_KUKA_KR6_R700 = 2,
    ROBOT_DOOSAN_M1013 = 3,
    ROBOT_CUSTOM = 99
} RobotType;

/// Weave pattern types
typedef enum {
    WEAVE_NONE = 0,
    WEAVE_ZIGZAG = 1,
    WEAVE_CIRCULAR = 2,
    WEAVE_TRIANGLE = 3,
    WEAVE_FIGURE8 = 4
} WeaveType;

/// DH Parameters structure (row-major)
typedef struct {
    double a;           // Link length (m)
    double alpha;       // Link twist (rad)
    double d;           // Link offset (m)
    double theta_offset; // Joint angle offset (rad)
} DHParams;

/// Joint limits structure
typedef struct {
    double min_angle;   // Minimum angle (rad)
    double max_angle;   // Maximum angle (rad)
    double max_velocity; // Maximum velocity (rad/s)
    double max_accel;   // Maximum acceleration (rad/s^2)
} JointLimits;

/// Weld point structure
typedef struct {
    float position[3];  // X, Y, Z
    float normal[3];    // Normal vector
    float tangent[3];   // Tangent vector
    float arc_length;   // Arc length from start
} WeldPoint;

/// Path parameters
typedef struct {
    float step_size;        // Path step size (m)
    float standoff_distance; // Tool standoff (m)
    float approach_angle;   // Approach angle (rad)
    float travel_angle;     // Travel angle (rad)
    WeaveType weave_type;
    float weave_amplitude;  // Weave amplitude (m)
    float weave_frequency;  // Weave frequency (Hz)
} PathParams;

/// Poisson reconstruction settings
typedef struct {
    int depth;              // Octree depth (6-12)
    float scale;            // Bounding box scale (1.0-1.5)
    bool linear_fit;        // Use linear interpolation
    float density_threshold; // Low density removal (0.0-1.0)
} PoissonSettings;

// =============================================================================
// Point Cloud API
// =============================================================================

/**
 * @brief Create a new point cloud object
 * @return Handle to point cloud, or NULL on failure
 */
SMR_API PointCloudHandle smr_pointcloud_create(void);

/**
 * @brief Destroy a point cloud object
 * @param handle Point cloud handle
 */
SMR_API void smr_pointcloud_destroy(PointCloudHandle handle);

/**
 * @brief Load point cloud from PLY file
 * @param handle Point cloud handle
 * @param filepath Path to PLY file
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_pointcloud_load_ply(PointCloudHandle handle, const char* filepath);

/**
 * @brief Load point cloud from PCD file
 * @param handle Point cloud handle
 * @param filepath Path to PCD file
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_pointcloud_load_pcd(PointCloudHandle handle, const char* filepath);

/**
 * @brief Set point cloud data from arrays
 * @param handle Point cloud handle
 * @param points Array of XYZ coordinates (count * 3 floats)
 * @param count Number of points
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_pointcloud_set_points(PointCloudHandle handle, 
                                                const float* points, int count);

/**
 * @brief Get point count
 * @param handle Point cloud handle
 * @return Number of points, or -1 on error
 */
SMR_API int smr_pointcloud_get_count(PointCloudHandle handle);

/**
 * @brief Get points array
 * @param handle Point cloud handle
 * @param out_points Output buffer (must be preallocated: count * 3 floats)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_pointcloud_get_points(PointCloudHandle handle, float* out_points);

/**
 * @brief Check if normals exist
 * @param handle Point cloud handle
 * @return true if normals exist
 */
SMR_API bool smr_pointcloud_has_normals(PointCloudHandle handle);

/**
 * @brief Get normals array
 * @param handle Point cloud handle
 * @param out_normals Output buffer (must be preallocated: count * 3 floats)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_pointcloud_get_normals(PointCloudHandle handle, float* out_normals);

/**
 * @brief Estimate normals using KNN
 * @param handle Point cloud handle
 * @param k Number of neighbors
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_pointcloud_estimate_normals_knn(PointCloudHandle handle, int k);

/**
 * @brief Estimate normals using radius search
 * @param handle Point cloud handle
 * @param radius Search radius (m)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_pointcloud_estimate_normals_radius(PointCloudHandle handle, 
                                                             float radius);

/**
 * @brief Orient normals consistently
 * @param handle Point cloud handle
 * @param camera_x, camera_y, camera_z Camera/viewpoint position
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_pointcloud_orient_normals(PointCloudHandle handle,
                                                    float camera_x, float camera_y, float camera_z);

/**
 * @brief Downsample using voxel grid
 * @param handle Point cloud handle
 * @param voxel_size Voxel size (m)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_pointcloud_downsample_voxel(PointCloudHandle handle, float voxel_size);

/**
 * @brief Remove statistical outliers
 * @param handle Point cloud handle
 * @param nb_neighbors Number of neighbors for analysis
 * @param std_ratio Standard deviation ratio threshold
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_pointcloud_remove_outliers(PointCloudHandle handle,
                                                     int nb_neighbors, float std_ratio);

// =============================================================================
// Mesh Generation API
// =============================================================================

/**
 * @brief Create mesh from point cloud using Poisson reconstruction
 * @param pc_handle Point cloud handle (must have normals)
 * @param settings Poisson reconstruction settings
 * @return Mesh handle, or NULL on failure
 */
SMR_API MeshHandle smr_mesh_create_poisson(PointCloudHandle pc_handle, 
                                            const PoissonSettings* settings);

/**
 * @brief Destroy a mesh object
 * @param handle Mesh handle
 */
SMR_API void smr_mesh_destroy(MeshHandle handle);

/**
 * @brief Get vertex count
 * @param handle Mesh handle
 * @return Number of vertices, or -1 on error
 */
SMR_API int smr_mesh_get_vertex_count(MeshHandle handle);

/**
 * @brief Get triangle count
 * @param handle Mesh handle
 * @return Number of triangles, or -1 on error
 */
SMR_API int smr_mesh_get_triangle_count(MeshHandle handle);

/**
 * @brief Get vertices array
 * @param handle Mesh handle
 * @param out_vertices Output buffer (vertex_count * 3 floats)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_mesh_get_vertices(MeshHandle handle, float* out_vertices);

/**
 * @brief Get vertex normals array
 * @param handle Mesh handle
 * @param out_normals Output buffer (vertex_count * 3 floats)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_mesh_get_normals(MeshHandle handle, float* out_normals);

/**
 * @brief Get triangle indices array
 * @param handle Mesh handle
 * @param out_indices Output buffer (triangle_count * 3 ints)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_mesh_get_triangles(MeshHandle handle, int* out_indices);

/**
 * @brief Remove low-density vertices
 * @param handle Mesh handle
 * @param quantile Density quantile threshold (0.0-1.0)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_mesh_remove_low_density(MeshHandle handle, float quantile);

/**
 * @brief Simplify mesh using quadric decimation
 * @param handle Mesh handle
 * @param target_ratio Target triangle ratio (0.0-1.0)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_mesh_simplify(MeshHandle handle, float target_ratio);

/**
 * @brief Save mesh to PLY file
 * @param handle Mesh handle
 * @param filepath Output file path
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_mesh_save_ply(MeshHandle handle, const char* filepath);

/**
 * @brief Save mesh to OBJ file
 * @param handle Mesh handle
 * @param filepath Output file path
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_mesh_save_obj(MeshHandle handle, const char* filepath);

// =============================================================================
// Robot Kinematics API
// =============================================================================

/**
 * @brief Create a robot model from preset
 * @param type Robot type preset
 * @return Robot handle, or NULL on failure
 */
SMR_API RobotHandle smr_robot_create(RobotType type);

/**
 * @brief Create a custom robot from DH parameters
 * @param dh_params Array of 6 DH parameter sets
 * @param joint_limits Array of 6 joint limit sets
 * @return Robot handle, or NULL on failure
 */
SMR_API RobotHandle smr_robot_create_custom(const DHParams* dh_params, 
                                             const JointLimits* joint_limits);

/**
 * @brief Destroy a robot model
 * @param handle Robot handle
 */
SMR_API void smr_robot_destroy(RobotHandle handle);

/**
 * @brief Compute forward kinematics
 * @param handle Robot handle
 * @param joint_angles Array of 6 joint angles (rad)
 * @param out_transform Output 4x4 transformation matrix (row-major, 16 floats)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_robot_forward_kinematics(RobotHandle handle,
                                                   const double* joint_angles,
                                                   double* out_transform);

/**
 * @brief Compute inverse kinematics (all solutions)
 * @param handle Robot handle
 * @param target_transform 4x4 target transformation matrix (row-major)
 * @param out_solutions Output buffer for solutions (max 8 solutions * 6 angles)
 * @param out_count Output number of valid solutions
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_robot_inverse_kinematics(RobotHandle handle,
                                                   const double* target_transform,
                                                   double* out_solutions,
                                                   int* out_count);

/**
 * @brief Compute IK solution nearest to reference configuration
 * @param handle Robot handle
 * @param target_transform 4x4 target transformation matrix
 * @param reference_angles Reference joint configuration
 * @param out_angles Output joint angles
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_robot_ik_nearest(RobotHandle handle,
                                           const double* target_transform,
                                           const double* reference_angles,
                                           double* out_angles);

/**
 * @brief Compute Jacobian matrix
 * @param handle Robot handle
 * @param joint_angles Array of 6 joint angles
 * @param out_jacobian Output 6x6 Jacobian matrix (row-major, 36 doubles)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_robot_compute_jacobian(RobotHandle handle,
                                                 const double* joint_angles,
                                                 double* out_jacobian);

/**
 * @brief Get manipulability measure
 * @param handle Robot handle
 * @param joint_angles Array of 6 joint angles
 * @return Manipulability value (sqrt(det(J*J^T))), or -1 on error
 */
SMR_API double smr_robot_get_manipulability(RobotHandle handle,
                                             const double* joint_angles);

/**
 * @brief Check if configuration is within joint limits
 * @param handle Robot handle
 * @param joint_angles Array of 6 joint angles
 * @return true if within limits
 */
SMR_API bool smr_robot_check_joint_limits(RobotHandle handle,
                                           const double* joint_angles);

// =============================================================================
// Path Planning API
// =============================================================================

/**
 * @brief Create a weld path from mesh edge
 * @param mesh_handle Mesh handle
 * @param params Path parameters
 * @return Path handle, or NULL on failure
 */
SMR_API PathHandle smr_path_create_from_edge(MeshHandle mesh_handle,
                                              const PathParams* params);

/**
 * @brief Create a weld path from point array
 * @param points Array of XYZ positions (count * 3 floats)
 * @param normals Array of normal vectors (count * 3 floats)
 * @param count Number of points
 * @param params Path parameters
 * @return Path handle, or NULL on failure
 */
SMR_API PathHandle smr_path_create_from_points(const float* points,
                                                const float* normals,
                                                int count,
                                                const PathParams* params);

/**
 * @brief Destroy a path object
 * @param handle Path handle
 */
SMR_API void smr_path_destroy(PathHandle handle);

/**
 * @brief Get path point count
 * @param handle Path handle
 * @return Number of weld points, or -1 on error
 */
SMR_API int smr_path_get_count(PathHandle handle);

/**
 * @brief Get path weld points
 * @param handle Path handle
 * @param out_points Output buffer (count * sizeof(WeldPoint))
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_path_get_points(PathHandle handle, WeldPoint* out_points);

/**
 * @brief Apply weave pattern to path
 * @param handle Path handle
 * @param weave_type Weave pattern type
 * @param amplitude Weave amplitude (m)
 * @param frequency Weave frequency (Hz)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_path_apply_weave(PathHandle handle,
                                           WeaveType weave_type,
                                           float amplitude, float frequency);

/**
 * @brief Resample path to uniform spacing
 * @param handle Path handle
 * @param step_size New step size (m)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_path_resample(PathHandle handle, float step_size);

/**
 * @brief Smooth path using moving average
 * @param handle Path handle
 * @param window_size Smoothing window size
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_path_smooth(PathHandle handle, int window_size);

/**
 * @brief Convert path to joint trajectory
 * @param path_handle Path handle
 * @param robot_handle Robot handle
 * @param standoff Tool standoff distance (m)
 * @param out_joints Output joint angles buffer (path_count * 6 doubles)
 * @param out_reachable Output reachability flags (path_count bools)
 * @return SMR_SUCCESS or error code
 */
SMR_API SMRErrorCode smr_path_to_joints(PathHandle path_handle,
                                         RobotHandle robot_handle,
                                         float standoff,
                                         double* out_joints,
                                         bool* out_reachable);

// =============================================================================
// Utility Functions
// =============================================================================

/**
 * @brief Get last error message
 * @return Error message string (static buffer, do not free)
 */
SMR_API const char* smr_get_last_error(void);

/**
 * @brief Get library version string
 * @return Version string (e.g., "1.0.0")
 */
SMR_API const char* smr_get_version(void);

#ifdef __cplusplus
}
#endif

#endif // SMR_WELDING_API_H
