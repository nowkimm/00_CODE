/**
 * @file robot_kinematics.cpp
 * @brief Robot Kinematics Implementation (FK, IK, Jacobian)
 */

#include "smr_welding_api.h"
#include <vector>
#include <cmath>
#include <algorithm>
#include <cstring>

// =============================================================================
// DH Parameters Presets
// =============================================================================

// UR5 DH Parameters (meters, radians)
static const DHParams UR5_DH[6] = {
    {0.0,      -M_PI/2, 0.089159, 0.0},
    {-0.425,   0.0,     0.0,      0.0},
    {-0.39225, 0.0,     0.0,      0.0},
    {0.0,      -M_PI/2, 0.10915,  0.0},
    {0.0,       M_PI/2, 0.09465,  0.0},
    {0.0,      0.0,     0.0823,   0.0}
};

static const JointLimits UR5_LIMITS[6] = {
    {-2*M_PI, 2*M_PI, 3.14, 5.0},
    {-2*M_PI, 2*M_PI, 3.14, 5.0},
    {-2*M_PI, 2*M_PI, 3.14, 5.0},
    {-2*M_PI, 2*M_PI, 6.28, 5.0},
    {-2*M_PI, 2*M_PI, 6.28, 5.0},
    {-2*M_PI, 2*M_PI, 6.28, 5.0}
};

// UR10 DH Parameters
static const DHParams UR10_DH[6] = {
    {0.0,      -M_PI/2, 0.1273,  0.0},
    {-0.612,   0.0,     0.0,     0.0},
    {-0.5723,  0.0,     0.0,     0.0},
    {0.0,      -M_PI/2, 0.163941, 0.0},
    {0.0,       M_PI/2, 0.1157,  0.0},
    {0.0,      0.0,     0.0922,  0.0}
};

static const JointLimits UR10_LIMITS[6] = {
    {-2*M_PI, 2*M_PI, 2.09, 5.0},
    {-2*M_PI, 2*M_PI, 2.09, 5.0},
    {-2*M_PI, 2*M_PI, 3.14, 5.0},
    {-2*M_PI, 2*M_PI, 3.14, 5.0},
    {-2*M_PI, 2*M_PI, 3.14, 5.0},
    {-2*M_PI, 2*M_PI, 3.14, 5.0}
};

// KUKA KR6 R700 DH Parameters
static const DHParams KUKA_KR6_DH[6] = {
    {0.025,   -M_PI/2, 0.400,  0.0},
    {0.315,   0.0,     0.0,    0.0},
    {0.035,   -M_PI/2, 0.0,    0.0},
    {0.0,      M_PI/2, 0.365,  0.0},
    {0.0,     -M_PI/2, 0.0,    0.0},
    {0.0,     0.0,     0.080,  0.0}
};

static const JointLimits KUKA_KR6_LIMITS[6] = {
    {-2.967, 2.967, 6.54, 10.0},
    {-2.094, 2.443, 6.28, 10.0},
    {-2.356, 2.094, 6.54, 10.0},
    {-3.490, 3.490, 7.85, 10.0},
    {-2.094, 2.094, 7.85, 10.0},
    {-6.109, 6.109, 12.04, 10.0}
};

// Doosan M1013 DH Parameters
static const DHParams DOOSAN_M1013_DH[6] = {
    {0.0,     -M_PI/2, 0.1555, 0.0},
    {-0.550,  0.0,     0.0,    0.0},
    {0.0,     -M_PI/2, 0.0,    0.0},
    {0.0,      M_PI/2, 0.546,  0.0},
    {0.0,     -M_PI/2, 0.0,    0.0},
    {0.0,     0.0,     0.110,  0.0}
};

static const JointLimits DOOSAN_M1013_LIMITS[6] = {
    {-6.283, 6.283, 2.09, 5.0},
    {-6.283, 6.283, 2.09, 5.0},
    {-2.618, 2.618, 2.97, 5.0},
    {-6.283, 6.283, 3.93, 5.0},
    {-6.283, 6.283, 3.93, 5.0},
    {-6.283, 6.283, 5.93, 5.0}
};

// =============================================================================
// Matrix Operations
// =============================================================================

class Matrix4x4 {
public:
    double m[16]; // Row-major order
    
    Matrix4x4() { identity(); }
    
    void identity() {
        std::memset(m, 0, sizeof(m));
        m[0] = m[5] = m[10] = m[15] = 1.0;
    }
    
    void set_dh(double a, double alpha, double d, double theta) {
        double ct = std::cos(theta);
        double st = std::sin(theta);
        double ca = std::cos(alpha);
        double sa = std::sin(alpha);
        
        m[0] = ct;       m[1] = -st*ca;   m[2] = st*sa;    m[3] = a*ct;
        m[4] = st;       m[5] = ct*ca;    m[6] = -ct*sa;   m[7] = a*st;
        m[8] = 0;        m[9] = sa;       m[10] = ca;      m[11] = d;
        m[12] = 0;       m[13] = 0;       m[14] = 0;       m[15] = 1;
    }
    
    Matrix4x4 operator*(const Matrix4x4& other) const {
        Matrix4x4 result;
        for (int i = 0; i < 4; ++i) {
            for (int j = 0; j < 4; ++j) {
                result.m[i*4+j] = 0;
                for (int k = 0; k < 4; ++k) {
                    result.m[i*4+j] += m[i*4+k] * other.m[k*4+j];
                }
            }
        }
        return result;
    }
    
    void get_position(double& x, double& y, double& z) const {
        x = m[3]; y = m[7]; z = m[11];
    }
    
    void get_rotation_axis(int col, double& x, double& y, double& z) const {
        x = m[col]; y = m[4+col]; z = m[8+col];
    }
};

// =============================================================================
// Robot Implementation
// =============================================================================

class RobotImpl {
public:
    DHParams dh[6];
    JointLimits limits[6];
    RobotType type;
    
    RobotImpl(RobotType t) : type(t) {
        const DHParams* preset_dh = nullptr;
        const JointLimits* preset_limits = nullptr;
        
        switch (t) {
            case ROBOT_UR5:
                preset_dh = UR5_DH;
                preset_limits = UR5_LIMITS;
                break;
            case ROBOT_UR10:
                preset_dh = UR10_DH;
                preset_limits = UR10_LIMITS;
                break;
            case ROBOT_KUKA_KR6_R700:
                preset_dh = KUKA_KR6_DH;
                preset_limits = KUKA_KR6_LIMITS;
                break;
            case ROBOT_DOOSAN_M1013:
                preset_dh = DOOSAN_M1013_DH;
                preset_limits = DOOSAN_M1013_LIMITS;
                break;
            default:
                // Use UR5 as default
                preset_dh = UR5_DH;
                preset_limits = UR5_LIMITS;
                break;
        }
        
        std::memcpy(dh, preset_dh, sizeof(dh));
        std::memcpy(limits, preset_limits, sizeof(limits));
    }
    
    RobotImpl(const DHParams* custom_dh, const JointLimits* custom_limits) 
        : type(ROBOT_CUSTOM) {
        std::memcpy(dh, custom_dh, sizeof(dh));
        std::memcpy(limits, custom_limits, sizeof(limits));
    }
    
    void forward_kinematics(const double* joints, Matrix4x4& result) const {
        result.identity();
        
        for (int i = 0; i < 6; ++i) {
            Matrix4x4 Ti;
            double theta = joints[i] + dh[i].theta_offset;
            Ti.set_dh(dh[i].a, dh[i].alpha, dh[i].d, theta);
            result = result * Ti;
        }
    }
    
    // Numerical IK using Jacobian pseudo-inverse
    bool inverse_kinematics_numerical(const Matrix4x4& target, 
                                       const double* initial_guess,
                                       double* solution,
                                       int max_iterations = 100,
                                       double tolerance = 1e-6) const {
        std::memcpy(solution, initial_guess, 6 * sizeof(double));
        
        for (int iter = 0; iter < max_iterations; ++iter) {
            Matrix4x4 current;
            forward_kinematics(solution, current);
            
            // Compute position error
            double dx = target.m[3] - current.m[3];
            double dy = target.m[7] - current.m[7];
            double dz = target.m[11] - current.m[11];
            
            // Compute orientation error (simplified: using rotation matrix difference)
            double ex = target.m[0] - current.m[0] + target.m[5] - current.m[5];
            double ey = target.m[1] - current.m[1] + target.m[6] - current.m[6];
            double ez = target.m[2] - current.m[2] + target.m[9] - current.m[9];
            
            double error[6] = {dx, dy, dz, ex*0.1, ey*0.1, ez*0.1};
            
            // Check convergence
            double error_norm = 0;
            for (int i = 0; i < 6; ++i) error_norm += error[i] * error[i];
            if (error_norm < tolerance * tolerance) return true;
            
            // Compute Jacobian
            double J[36];
            compute_jacobian(solution, J);
            
            // Pseudo-inverse update (simplified)
            double damping = 0.01;
            for (int i = 0; i < 6; ++i) {
                double delta = 0;
                for (int j = 0; j < 6; ++j) {
                    delta += J[j*6+i] * error[j];
                }
                solution[i] += delta * damping;
                
                // Clamp to joint limits
                solution[i] = std::max(limits[i].min_angle, 
                              std::min(limits[i].max_angle, solution[i]));
            }
        }
        
        return false;
    }
    
    void compute_jacobian(const double* joints, double* J) const {
        // Compute geometric Jacobian
        Matrix4x4 T[7];
        T[0].identity();
        
        for (int i = 0; i < 6; ++i) {
            Matrix4x4 Ti;
            double theta = joints[i] + dh[i].theta_offset;
            Ti.set_dh(dh[i].a, dh[i].alpha, dh[i].d, theta);
            T[i+1] = T[i] * Ti;
        }
        
        double pe[3]; // End-effector position
        T[6].get_position(pe[0], pe[1], pe[2]);
        
        for (int i = 0; i < 6; ++i) {
            double pi[3]; // Joint i position
            T[i].get_position(pi[0], pi[1], pi[2]);
            
            double zi[3]; // Joint i z-axis
            T[i].get_rotation_axis(2, zi[0], zi[1], zi[2]);
            
            // Linear velocity component: z_i x (p_e - p_i)
            double dp[3] = {pe[0]-pi[0], pe[1]-pi[1], pe[2]-pi[2]};
            J[0*6+i] = zi[1]*dp[2] - zi[2]*dp[1];
            J[1*6+i] = zi[2]*dp[0] - zi[0]*dp[2];
            J[2*6+i] = zi[0]*dp[1] - zi[1]*dp[0];
            
            // Angular velocity component: z_i
            J[3*6+i] = zi[0];
            J[4*6+i] = zi[1];
            J[5*6+i] = zi[2];
        }
    }
    
    double compute_manipulability(const double* joints) const {
        double J[36];
        compute_jacobian(joints, J);
        
        // Compute J * J^T
        double JJT[36];
        for (int i = 0; i < 6; ++i) {
            for (int j = 0; j < 6; ++j) {
                JJT[i*6+j] = 0;
                for (int k = 0; k < 6; ++k) {
                    JJT[i*6+j] += J[i*6+k] * J[j*6+k];
                }
            }
        }
        
        // Compute determinant (simplified: trace as approximation for small values)
        // Real implementation would compute full determinant
        double trace = 0;
        for (int i = 0; i < 6; ++i) trace += JJT[i*6+i];
        
        return std::sqrt(std::max(0.0, trace / 6.0));
    }
    
    bool check_joint_limits(const double* joints) const {
        for (int i = 0; i < 6; ++i) {
            if (joints[i] < limits[i].min_angle || joints[i] > limits[i].max_angle) {
                return false;
            }
        }
        return true;
    }
};

// =============================================================================
// C API Implementation
// =============================================================================

SMR_API RobotHandle smr_robot_create(RobotType type) {
    return new RobotImpl(type);
}

SMR_API RobotHandle smr_robot_create_custom(const DHParams* dh_params, 
                                             const JointLimits* joint_limits) {
    if (!dh_params || !joint_limits) return nullptr;
    return new RobotImpl(dh_params, joint_limits);
}

SMR_API void smr_robot_destroy(RobotHandle handle) {
    delete static_cast<RobotImpl*>(handle);
}

SMR_API SMRErrorCode smr_robot_forward_kinematics(RobotHandle handle,
                                                   const double* joint_angles,
                                                   double* out_transform) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (!joint_angles || !out_transform) return SMR_ERROR_INVALID_PARAMETER;
    
    auto* robot = static_cast<RobotImpl*>(handle);
    Matrix4x4 result;
    robot->forward_kinematics(joint_angles, result);
    std::memcpy(out_transform, result.m, 16 * sizeof(double));
    return SMR_SUCCESS;
}

SMR_API SMRErrorCode smr_robot_inverse_kinematics(RobotHandle handle,
                                                   const double* target_transform,
                                                   double* out_solutions,
                                                   int* out_count) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (!target_transform || !out_solutions || !out_count) return SMR_ERROR_INVALID_PARAMETER;
    
    auto* robot = static_cast<RobotImpl*>(handle);
    
    Matrix4x4 target;
    std::memcpy(target.m, target_transform, 16 * sizeof(double));
    
    // Try multiple initial guesses
    double initial_guesses[8][6] = {
        {0, -M_PI/2, M_PI/2, 0, 0, 0},
        {0, -M_PI/4, M_PI/4, 0, 0, 0},
        {M_PI/2, -M_PI/2, M_PI/2, 0, 0, 0},
        {-M_PI/2, -M_PI/2, M_PI/2, 0, 0, 0},
        {0, -M_PI/2, M_PI/2, M_PI, 0, 0},
        {0, -3*M_PI/4, 3*M_PI/4, 0, 0, 0},
        {M_PI, -M_PI/2, M_PI/2, 0, 0, 0},
        {0, 0, 0, 0, 0, 0}
    };
    
    *out_count = 0;
    for (int i = 0; i < 8 && *out_count < 8; ++i) {
        double solution[6];
        if (robot->inverse_kinematics_numerical(target, initial_guesses[i], solution)) {
            // Check if solution is unique
            bool duplicate = false;
            for (int j = 0; j < *out_count; ++j) {
                double diff = 0;
                for (int k = 0; k < 6; ++k) {
                    double d = out_solutions[j*6+k] - solution[k];
                    diff += d * d;
                }
                if (diff < 0.01) {
                    duplicate = true;
                    break;
                }
            }
            
            if (!duplicate) {
                std::memcpy(out_solutions + (*out_count) * 6, solution, 6 * sizeof(double));
                (*out_count)++;
            }
        }
    }
    
    return (*out_count > 0) ? SMR_SUCCESS : SMR_ERROR_NO_SOLUTION;
}

SMR_API SMRErrorCode smr_robot_ik_nearest(RobotHandle handle,
                                           const double* target_transform,
                                           const double* reference_angles,
                                           double* out_angles) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (!target_transform || !reference_angles || !out_angles) return SMR_ERROR_INVALID_PARAMETER;
    
    auto* robot = static_cast<RobotImpl*>(handle);
    
    Matrix4x4 target;
    std::memcpy(target.m, target_transform, 16 * sizeof(double));
    
    if (robot->inverse_kinematics_numerical(target, reference_angles, out_angles)) {
        return SMR_SUCCESS;
    }
    
    return SMR_ERROR_NO_SOLUTION;
}

SMR_API SMRErrorCode smr_robot_compute_jacobian(RobotHandle handle,
                                                 const double* joint_angles,
                                                 double* out_jacobian) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (!joint_angles || !out_jacobian) return SMR_ERROR_INVALID_PARAMETER;
    
    static_cast<RobotImpl*>(handle)->compute_jacobian(joint_angles, out_jacobian);
    return SMR_SUCCESS;
}

SMR_API double smr_robot_get_manipulability(RobotHandle handle,
                                             const double* joint_angles) {
    if (!handle || !joint_angles) return -1.0;
    return static_cast<RobotImpl*>(handle)->compute_manipulability(joint_angles);
}

SMR_API bool smr_robot_check_joint_limits(RobotHandle handle,
                                           const double* joint_angles) {
    if (!handle || !joint_angles) return false;
    return static_cast<RobotImpl*>(handle)->check_joint_limits(joint_angles);
}
