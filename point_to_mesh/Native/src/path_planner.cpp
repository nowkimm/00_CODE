/**
 * @file path_planner.cpp
 * @brief Weld Path Planning Implementation
 */

#include "smr_welding_api.h"
#include <vector>
#include <cmath>
#include <algorithm>
#include <cstring>

// Forward declaration
class RobotImpl;

// =============================================================================
// Path Implementation
// =============================================================================

class PathImpl {
public:
    std::vector<WeldPoint> points;
    PathParams params;
    
    PathImpl() {
        params.step_size = 0.005f;           // 5mm default
        params.standoff_distance = 0.015f;    // 15mm standoff
        params.approach_angle = 0.0f;
        params.travel_angle = 0.0f;
        params.weave_type = WEAVE_NONE;
        params.weave_amplitude = 0.002f;      // 2mm
        params.weave_frequency = 2.0f;        // 2Hz
    }
    
    void create_from_points(const float* positions, const float* normals, 
                            int count, const PathParams& p) {
        params = p;
        points.clear();
        points.reserve(count);
        
        float arc_length = 0.0f;
        
        for (int i = 0; i < count; ++i) {
            WeldPoint wp;
            wp.position[0] = positions[i*3];
            wp.position[1] = positions[i*3+1];
            wp.position[2] = positions[i*3+2];
            
            wp.normal[0] = normals[i*3];
            wp.normal[1] = normals[i*3+1];
            wp.normal[2] = normals[i*3+2];
            
            // Compute tangent from adjacent points
            if (count > 1) {
                int next = std::min(i + 1, count - 1);
                int prev = std::max(i - 1, 0);
                
                wp.tangent[0] = positions[next*3] - positions[prev*3];
                wp.tangent[1] = positions[next*3+1] - positions[prev*3+1];
                wp.tangent[2] = positions[next*3+2] - positions[prev*3+2];
                
                // Normalize tangent
                float len = std::sqrt(wp.tangent[0]*wp.tangent[0] + 
                                      wp.tangent[1]*wp.tangent[1] + 
                                      wp.tangent[2]*wp.tangent[2]);
                if (len > 1e-6f) {
                    wp.tangent[0] /= len;
                    wp.tangent[1] /= len;
                    wp.tangent[2] /= len;
                }
            }
            
            // Compute arc length
            if (i > 0) {
                float dx = wp.position[0] - points[i-1].position[0];
                float dy = wp.position[1] - points[i-1].position[1];
                float dz = wp.position[2] - points[i-1].position[2];
                arc_length += std::sqrt(dx*dx + dy*dy + dz*dz);
            }
            wp.arc_length = arc_length;
            
            points.push_back(wp);
        }
    }
    
    void apply_weave(WeaveType type, float amplitude, float frequency) {
        if (type == WEAVE_NONE || points.empty()) return;
        
        float travel_speed = 0.01f; // 10mm/s assumed
        
        for (size_t i = 0; i < points.size(); ++i) {
            WeldPoint& wp = points[i];
            float t = wp.arc_length / travel_speed;
            
            // Compute lateral direction (perpendicular to tangent and normal)
            float lateral[3];
            lateral[0] = wp.tangent[1]*wp.normal[2] - wp.tangent[2]*wp.normal[1];
            lateral[1] = wp.tangent[2]*wp.normal[0] - wp.tangent[0]*wp.normal[2];
            lateral[2] = wp.tangent[0]*wp.normal[1] - wp.tangent[1]*wp.normal[0];
            
            float offset = 0.0f;
            
            switch (type) {
                case WEAVE_ZIGZAG: {
                    float phase = std::fmod(t * frequency, 1.0f);
                    offset = (phase < 0.5f) ? 
                             amplitude * (4.0f * phase - 1.0f) :
                             amplitude * (3.0f - 4.0f * phase);
                    break;
                }
                case WEAVE_CIRCULAR: {
                    offset = amplitude * std::sin(2.0f * M_PI * frequency * t);
                    // Also add vertical component
                    float v_offset = amplitude * std::cos(2.0f * M_PI * frequency * t);
                    wp.position[0] += wp.normal[0] * v_offset * 0.5f;
                    wp.position[1] += wp.normal[1] * v_offset * 0.5f;
                    wp.position[2] += wp.normal[2] * v_offset * 0.5f;
                    break;
                }
                case WEAVE_TRIANGLE: {
                    float phase = std::fmod(t * frequency, 1.0f);
                    offset = amplitude * (1.0f - 4.0f * std::abs(phase - 0.5f));
                    break;
                }
                case WEAVE_FIGURE8: {
                    offset = amplitude * std::sin(4.0f * M_PI * frequency * t);
                    float v_offset = amplitude * 0.5f * std::sin(2.0f * M_PI * frequency * t);
                    wp.position[0] += wp.normal[0] * v_offset;
                    wp.position[1] += wp.normal[1] * v_offset;
                    wp.position[2] += wp.normal[2] * v_offset;
                    break;
                }
                default:
                    break;
            }
            
            // Apply lateral offset
            wp.position[0] += lateral[0] * offset;
            wp.position[1] += lateral[1] * offset;
            wp.position[2] += lateral[2] * offset;
        }
    }
    
    void resample(float step_size) {
        if (points.size() < 2 || step_size <= 0) return;
        
        std::vector<WeldPoint> new_points;
        float total_length = points.back().arc_length;
        int num_points = static_cast<int>(total_length / step_size) + 1;
        
        new_points.reserve(num_points);
        
        size_t src_idx = 0;
        for (int i = 0; i < num_points; ++i) {
            float target_arc = i * step_size;
            
            // Find surrounding points
            while (src_idx < points.size() - 1 && 
                   points[src_idx + 1].arc_length < target_arc) {
                ++src_idx;
            }
            
            if (src_idx >= points.size() - 1) {
                new_points.push_back(points.back());
                continue;
            }
            
            // Interpolate
            const WeldPoint& p0 = points[src_idx];
            const WeldPoint& p1 = points[src_idx + 1];
            
            float t = (target_arc - p0.arc_length) / 
                      (p1.arc_length - p0.arc_length);
            t = std::max(0.0f, std::min(1.0f, t));
            
            WeldPoint wp;
            for (int j = 0; j < 3; ++j) {
                wp.position[j] = p0.position[j] + t * (p1.position[j] - p0.position[j]);
                wp.normal[j] = p0.normal[j] + t * (p1.normal[j] - p0.normal[j]);
                wp.tangent[j] = p0.tangent[j] + t * (p1.tangent[j] - p0.tangent[j]);
            }
            
            // Normalize normal and tangent
            float n_len = std::sqrt(wp.normal[0]*wp.normal[0] + 
                                    wp.normal[1]*wp.normal[1] + 
                                    wp.normal[2]*wp.normal[2]);
            float t_len = std::sqrt(wp.tangent[0]*wp.tangent[0] + 
                                    wp.tangent[1]*wp.tangent[1] + 
                                    wp.tangent[2]*wp.tangent[2]);
            
            if (n_len > 1e-6f) {
                wp.normal[0] /= n_len;
                wp.normal[1] /= n_len;
                wp.normal[2] /= n_len;
            }
            if (t_len > 1e-6f) {
                wp.tangent[0] /= t_len;
                wp.tangent[1] /= t_len;
                wp.tangent[2] /= t_len;
            }
            
            wp.arc_length = target_arc;
            new_points.push_back(wp);
        }
        
        points = std::move(new_points);
    }
    
    void smooth(int window_size) {
        if (points.size() < static_cast<size_t>(window_size) || window_size < 3) return;
        
        std::vector<WeldPoint> smoothed = points;
        int half = window_size / 2;
        
        for (size_t i = half; i < points.size() - half; ++i) {
            float sum_pos[3] = {0, 0, 0};
            float sum_norm[3] = {0, 0, 0};
            
            for (int j = -half; j <= half; ++j) {
                const WeldPoint& wp = points[i + j];
                for (int k = 0; k < 3; ++k) {
                    sum_pos[k] += wp.position[k];
                    sum_norm[k] += wp.normal[k];
                }
            }
            
            float count = static_cast<float>(window_size);
            for (int k = 0; k < 3; ++k) {
                smoothed[i].position[k] = sum_pos[k] / count;
                smoothed[i].normal[k] = sum_norm[k] / count;
            }
            
            // Normalize normal
            float len = std::sqrt(smoothed[i].normal[0]*smoothed[i].normal[0] +
                                  smoothed[i].normal[1]*smoothed[i].normal[1] +
                                  smoothed[i].normal[2]*smoothed[i].normal[2]);
            if (len > 1e-6f) {
                smoothed[i].normal[0] /= len;
                smoothed[i].normal[1] /= len;
                smoothed[i].normal[2] /= len;
            }
        }
        
        // Recompute tangents
        for (size_t i = 0; i < smoothed.size(); ++i) {
            size_t next = std::min(i + 1, smoothed.size() - 1);
            size_t prev = (i > 0) ? i - 1 : 0;
            
            smoothed[i].tangent[0] = smoothed[next].position[0] - smoothed[prev].position[0];
            smoothed[i].tangent[1] = smoothed[next].position[1] - smoothed[prev].position[1];
            smoothed[i].tangent[2] = smoothed[next].position[2] - smoothed[prev].position[2];
            
            float len = std::sqrt(smoothed[i].tangent[0]*smoothed[i].tangent[0] +
                                  smoothed[i].tangent[1]*smoothed[i].tangent[1] +
                                  smoothed[i].tangent[2]*smoothed[i].tangent[2]);
            if (len > 1e-6f) {
                smoothed[i].tangent[0] /= len;
                smoothed[i].tangent[1] /= len;
                smoothed[i].tangent[2] /= len;
            }
        }
        
        points = std::move(smoothed);
        
        // Recompute arc lengths
        float arc = 0.0f;
        for (size_t i = 0; i < points.size(); ++i) {
            if (i > 0) {
                float dx = points[i].position[0] - points[i-1].position[0];
                float dy = points[i].position[1] - points[i-1].position[1];
                float dz = points[i].position[2] - points[i-1].position[2];
                arc += std::sqrt(dx*dx + dy*dy + dz*dz);
            }
            points[i].arc_length = arc;
        }
    }
};

// =============================================================================
// C API Implementation
// =============================================================================

SMR_API PathHandle smr_path_create_from_edge(MeshHandle mesh_handle,
                                              const PathParams* params) {
    if (!mesh_handle || !params) return nullptr;
    
    // Simplified: create a test circular path
    auto* path = new PathImpl();
    path->params = *params;
    
    int num_points = 100;
    float radius = 0.1f;
    
    std::vector<float> positions(num_points * 3);
    std::vector<float> normals(num_points * 3);
    
    for (int i = 0; i < num_points; ++i) {
        float angle = 2.0f * M_PI * i / num_points;
        positions[i*3] = radius * std::cos(angle);
        positions[i*3+1] = radius * std::sin(angle);
        positions[i*3+2] = 0.0f;
        
        normals[i*3] = 0.0f;
        normals[i*3+1] = 0.0f;
        normals[i*3+2] = 1.0f;
    }
    
    path->create_from_points(positions.data(), normals.data(), num_points, *params);
    
    return path;
}

SMR_API PathHandle smr_path_create_from_points(const float* points,
                                                const float* normals,
                                                int count,
                                                const PathParams* params) {
    if (!points || !normals || count <= 0 || !params) return nullptr;
    
    auto* path = new PathImpl();
    path->create_from_points(points, normals, count, *params);
    
    return path;
}

SMR_API void smr_path_destroy(PathHandle handle) {
    delete static_cast<PathImpl*>(handle);
}

SMR_API int smr_path_get_count(PathHandle handle) {
    if (!handle) return -1;
    return static_cast<int>(static_cast<PathImpl*>(handle)->points.size());
}

SMR_API SMRErrorCode smr_path_get_points(PathHandle handle, WeldPoint* out_points) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (!out_points) return SMR_ERROR_INVALID_PARAMETER;
    
    auto* path = static_cast<PathImpl*>(handle);
    std::memcpy(out_points, path->points.data(), 
                path->points.size() * sizeof(WeldPoint));
    return SMR_SUCCESS;
}

SMR_API SMRErrorCode smr_path_apply_weave(PathHandle handle,
                                           WeaveType weave_type,
                                           float amplitude, float frequency) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    static_cast<PathImpl*>(handle)->apply_weave(weave_type, amplitude, frequency);
    return SMR_SUCCESS;
}

SMR_API SMRErrorCode smr_path_resample(PathHandle handle, float step_size) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (step_size <= 0) return SMR_ERROR_INVALID_PARAMETER;
    static_cast<PathImpl*>(handle)->resample(step_size);
    return SMR_SUCCESS;
}

SMR_API SMRErrorCode smr_path_smooth(PathHandle handle, int window_size) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (window_size < 3) return SMR_ERROR_INVALID_PARAMETER;
    static_cast<PathImpl*>(handle)->smooth(window_size);
    return SMR_SUCCESS;
}

// Robot forward declaration for path to joints conversion
extern "C" SMRErrorCode smr_robot_ik_nearest(RobotHandle handle,
                                              const double* target_transform,
                                              const double* reference_angles,
                                              double* out_angles);

SMR_API SMRErrorCode smr_path_to_joints(PathHandle path_handle,
                                         RobotHandle robot_handle,
                                         float standoff,
                                         double* out_joints,
                                         bool* out_reachable) {
    if (!path_handle || !robot_handle) return SMR_ERROR_INVALID_HANDLE;
    if (!out_joints || !out_reachable) return SMR_ERROR_INVALID_PARAMETER;
    
    auto* path = static_cast<PathImpl*>(path_handle);
    
    double prev_joints[6] = {0, -M_PI/2, M_PI/2, 0, 0, 0};
    
    for (size_t i = 0; i < path->points.size(); ++i) {
        const WeldPoint& wp = path->points[i];
        
        // Build target transform
        // Position = weld point + standoff along normal
        double target[16] = {0};
        
        // Rotation: Z-axis along negative normal (pointing at surface)
        // X-axis along tangent
        float z[3] = {-wp.normal[0], -wp.normal[1], -wp.normal[2]};
        float x[3] = {wp.tangent[0], wp.tangent[1], wp.tangent[2]};
        
        // Y = Z x X
        float y[3] = {
            z[1]*x[2] - z[2]*x[1],
            z[2]*x[0] - z[0]*x[2],
            z[0]*x[1] - z[1]*x[0]
        };
        
        // Normalize
        float y_len = std::sqrt(y[0]*y[0] + y[1]*y[1] + y[2]*y[2]);
        if (y_len > 1e-6f) { y[0] /= y_len; y[1] /= y_len; y[2] /= y_len; }
        
        // Rebuild X = Y x Z for orthogonality
        x[0] = y[1]*z[2] - y[2]*z[1];
        x[1] = y[2]*z[0] - y[0]*z[2];
        x[2] = y[0]*z[1] - y[1]*z[0];
        
        // Build rotation matrix (column-major for OpenGL style, but we use row-major)
        target[0] = x[0]; target[1] = y[0]; target[2] = z[0]; target[3] = wp.position[0] - standoff*wp.normal[0];
        target[4] = x[1]; target[5] = y[1]; target[6] = z[1]; target[7] = wp.position[1] - standoff*wp.normal[1];
        target[8] = x[2]; target[9] = y[2]; target[10] = z[2]; target[11] = wp.position[2] - standoff*wp.normal[2];
        target[12] = 0;   target[13] = 0;   target[14] = 0;   target[15] = 1;
        
        // Solve IK
        double joints[6];
        SMRErrorCode result = smr_robot_ik_nearest(robot_handle, target, prev_joints, joints);
        
        if (result == SMR_SUCCESS) {
            std::memcpy(out_joints + i * 6, joints, 6 * sizeof(double));
            std::memcpy(prev_joints, joints, 6 * sizeof(double));
            out_reachable[i] = true;
        } else {
            std::memset(out_joints + i * 6, 0, 6 * sizeof(double));
            out_reachable[i] = false;
        }
    }
    
    return SMR_SUCCESS;
}
