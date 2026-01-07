/**
 * @file point_cloud.cpp
 * @brief Point Cloud Processing Implementation
 */

#include "smr_welding_api.h"
#include <vector>
#include <string>
#include <cmath>
#include <algorithm>
#include <fstream>
#include <sstream>
#include <cstring>

// Thread-local error message
static thread_local char g_last_error[512] = {0};

static void set_error(const char* msg) {
    strncpy(g_last_error, msg, sizeof(g_last_error) - 1);
    g_last_error[sizeof(g_last_error) - 1] = '\0';
}

// =============================================================================
// Internal Point Cloud Class
// =============================================================================

class PointCloudImpl {
public:
    std::vector<float> points;   // XYZ * count
    std::vector<float> normals;  // XYZ * count
    std::vector<float> colors;   // RGB * count
    bool has_normals = false;
    bool has_colors = false;

    int count() const { return static_cast<int>(points.size() / 3); }

    void clear() {
        points.clear();
        normals.clear();
        colors.clear();
        has_normals = false;
        has_colors = false;
    }

    bool load_ply(const char* filepath);
    bool load_pcd(const char* filepath);
    void estimate_normals_knn(int k);
    void estimate_normals_radius(float radius);
    void orient_normals(float cx, float cy, float cz);
    void downsample_voxel(float voxel_size);
    void remove_outliers(int nb_neighbors, float std_ratio);
};

// Simple PLY loader (ASCII format)
bool PointCloudImpl::load_ply(const char* filepath) {
    std::ifstream file(filepath);
    if (!file.is_open()) {
        set_error("Cannot open PLY file");
        return false;
    }

    std::string line;
    int vertex_count = 0;
    bool in_header = true;
    bool has_nx = false, has_red = false;

    // Parse header
    while (in_header && std::getline(file, line)) {
        std::istringstream iss(line);
        std::string token;
        iss >> token;

        if (token == "element") {
            std::string type;
            iss >> type;
            if (type == "vertex") {
                iss >> vertex_count;
            }
        } else if (token == "property") {
            std::string dtype, name;
            iss >> dtype >> name;
            if (name == "nx") has_nx = true;
            if (name == "red") has_red = true;
        } else if (token == "end_header") {
            in_header = false;
        }
    }

    if (vertex_count <= 0) {
        set_error("Invalid vertex count in PLY");
        return false;
    }

    // Read vertices
    clear();
    points.reserve(vertex_count * 3);
    if (has_nx) normals.reserve(vertex_count * 3);
    if (has_red) colors.reserve(vertex_count * 3);

    for (int i = 0; i < vertex_count && std::getline(file, line); ++i) {
        std::istringstream iss(line);
        float x, y, z;
        iss >> x >> y >> z;
        points.push_back(x);
        points.push_back(y);
        points.push_back(z);

        if (has_nx) {
            float nx, ny, nz;
            iss >> nx >> ny >> nz;
            normals.push_back(nx);
            normals.push_back(ny);
            normals.push_back(nz);
        }

        if (has_red) {
            float r, g, b;
            iss >> r >> g >> b;
            colors.push_back(r / 255.0f);
            colors.push_back(g / 255.0f);
            colors.push_back(b / 255.0f);
        }
    }

    has_normals = has_nx && !normals.empty();
    has_colors = has_red && !colors.empty();
    return true;
}

// Simple PCD loader (ASCII format)
bool PointCloudImpl::load_pcd(const char* filepath) {
    std::ifstream file(filepath);
    if (!file.is_open()) {
        set_error("Cannot open PCD file");
        return false;
    }

    std::string line;
    int point_count = 0;
    bool in_header = true;

    // Parse header
    while (in_header && std::getline(file, line)) {
        std::istringstream iss(line);
        std::string token;
        iss >> token;

        if (token == "POINTS") {
            iss >> point_count;
        } else if (token == "DATA") {
            in_header = false;
        }
    }

    if (point_count <= 0) {
        set_error("Invalid point count in PCD");
        return false;
    }

    clear();
    points.reserve(point_count * 3);

    for (int i = 0; i < point_count && std::getline(file, line); ++i) {
        std::istringstream iss(line);
        float x, y, z;
        iss >> x >> y >> z;
        points.push_back(x);
        points.push_back(y);
        points.push_back(z);
    }

    return true;
}

// KNN-based normal estimation (simplified)
void PointCloudImpl::estimate_normals_knn(int k) {
    int n = count();
    if (n < k) return;

    normals.resize(n * 3);
    
    // For each point, find k nearest neighbors and compute normal via PCA
    for (int i = 0; i < n; ++i) {
        float px = points[i*3], py = points[i*3+1], pz = points[i*3+2];
        
        // Find k nearest neighbors (brute force for simplicity)
        std::vector<std::pair<float, int>> distances;
        for (int j = 0; j < n; ++j) {
            if (i == j) continue;
            float dx = points[j*3] - px;
            float dy = points[j*3+1] - py;
            float dz = points[j*3+2] - pz;
            float d = dx*dx + dy*dy + dz*dz;
            distances.push_back({d, j});
        }
        std::partial_sort(distances.begin(), distances.begin() + k, 
                          distances.end());

        // Compute centroid of neighbors
        float cx = 0, cy = 0, cz = 0;
        for (int j = 0; j < k; ++j) {
            int idx = distances[j].second;
            cx += points[idx*3];
            cy += points[idx*3+1];
            cz += points[idx*3+2];
        }
        cx /= k; cy /= k; cz /= k;

        // Compute covariance matrix
        float cov[9] = {0};
        for (int j = 0; j < k; ++j) {
            int idx = distances[j].second;
            float dx = points[idx*3] - cx;
            float dy = points[idx*3+1] - cy;
            float dz = points[idx*3+2] - cz;
            cov[0] += dx*dx; cov[1] += dx*dy; cov[2] += dx*dz;
            cov[3] += dy*dx; cov[4] += dy*dy; cov[5] += dy*dz;
            cov[6] += dz*dx; cov[7] += dz*dy; cov[8] += dz*dz;
        }

        // Simplified: use cross product of two eigenvectors approximation
        // Normal = smallest eigenvector (simplified: use column with smallest variance)
        float var0 = cov[0], var1 = cov[4], var2 = cov[8];
        int min_idx = (var0 < var1 && var0 < var2) ? 0 : (var1 < var2 ? 1 : 2);
        
        float nx = (min_idx == 0) ? 1.0f : 0.0f;
        float ny = (min_idx == 1) ? 1.0f : 0.0f;
        float nz = (min_idx == 2) ? 1.0f : 0.0f;

        // More accurate: power iteration for smallest eigenvector
        // (simplified version for demo)
        float len = std::sqrt(nx*nx + ny*ny + nz*nz);
        if (len > 1e-6f) {
            normals[i*3] = nx / len;
            normals[i*3+1] = ny / len;
            normals[i*3+2] = nz / len;
        } else {
            normals[i*3] = 0;
            normals[i*3+1] = 0;
            normals[i*3+2] = 1;
        }
    }
    has_normals = true;
}

void PointCloudImpl::estimate_normals_radius(float radius) {
    // Similar to KNN but using radius search
    estimate_normals_knn(20); // Simplified: use KNN with default k
}

void PointCloudImpl::orient_normals(float cx, float cy, float cz) {
    if (!has_normals) return;
    
    int n = count();
    for (int i = 0; i < n; ++i) {
        float px = points[i*3], py = points[i*3+1], pz = points[i*3+2];
        float nx = normals[i*3], ny = normals[i*3+1], nz = normals[i*3+2];
        
        // Vector from point to camera
        float vx = cx - px, vy = cy - py, vz = cz - pz;
        
        // Flip normal if pointing away from camera
        float dot = nx*vx + ny*vy + nz*vz;
        if (dot < 0) {
            normals[i*3] = -nx;
            normals[i*3+1] = -ny;
            normals[i*3+2] = -nz;
        }
    }
}

void PointCloudImpl::downsample_voxel(float voxel_size) {
    if (voxel_size <= 0) return;
    
    // Simple voxel grid downsampling
    struct VoxelKey {
        int x, y, z;
        bool operator<(const VoxelKey& other) const {
            if (x != other.x) return x < other.x;
            if (y != other.y) return y < other.y;
            return z < other.z;
        }
    };

    std::map<VoxelKey, std::vector<int>> voxels;
    int n = count();
    
    for (int i = 0; i < n; ++i) {
        VoxelKey key;
        key.x = static_cast<int>(std::floor(points[i*3] / voxel_size));
        key.y = static_cast<int>(std::floor(points[i*3+1] / voxel_size));
        key.z = static_cast<int>(std::floor(points[i*3+2] / voxel_size));
        voxels[key].push_back(i);
    }

    std::vector<float> new_points;
    std::vector<float> new_normals;
    
    for (auto& pair : voxels) {
        float px = 0, py = 0, pz = 0;
        float nx = 0, ny = 0, nz = 0;
        int count = static_cast<int>(pair.second.size());
        
        for (int idx : pair.second) {
            px += points[idx*3];
            py += points[idx*3+1];
            pz += points[idx*3+2];
            if (has_normals) {
                nx += normals[idx*3];
                ny += normals[idx*3+1];
                nz += normals[idx*3+2];
            }
        }
        
        new_points.push_back(px / count);
        new_points.push_back(py / count);
        new_points.push_back(pz / count);
        
        if (has_normals) {
            float len = std::sqrt(nx*nx + ny*ny + nz*nz);
            if (len > 1e-6f) {
                new_normals.push_back(nx / len);
                new_normals.push_back(ny / len);
                new_normals.push_back(nz / len);
            } else {
                new_normals.push_back(0);
                new_normals.push_back(0);
                new_normals.push_back(1);
            }
        }
    }

    points = std::move(new_points);
    if (has_normals) normals = std::move(new_normals);
}

void PointCloudImpl::remove_outliers(int nb_neighbors, float std_ratio) {
    int n = count();
    if (n <= nb_neighbors) return;

    std::vector<float> mean_distances(n);
    
    // Compute mean distance to neighbors for each point
    for (int i = 0; i < n; ++i) {
        float px = points[i*3], py = points[i*3+1], pz = points[i*3+2];
        std::vector<float> distances;
        
        for (int j = 0; j < n; ++j) {
            if (i == j) continue;
            float dx = points[j*3] - px;
            float dy = points[j*3+1] - py;
            float dz = points[j*3+2] - pz;
            distances.push_back(std::sqrt(dx*dx + dy*dy + dz*dz));
        }
        std::partial_sort(distances.begin(), 
                          distances.begin() + nb_neighbors, 
                          distances.end());
        
        float sum = 0;
        for (int k = 0; k < nb_neighbors; ++k) sum += distances[k];
        mean_distances[i] = sum / nb_neighbors;
    }

    // Compute global statistics
    float global_mean = 0, global_std = 0;
    for (float d : mean_distances) global_mean += d;
    global_mean /= n;
    
    for (float d : mean_distances) {
        float diff = d - global_mean;
        global_std += diff * diff;
    }
    global_std = std::sqrt(global_std / n);

    float threshold = global_mean + std_ratio * global_std;

    // Filter points
    std::vector<float> new_points;
    std::vector<float> new_normals;
    
    for (int i = 0; i < n; ++i) {
        if (mean_distances[i] <= threshold) {
            new_points.push_back(points[i*3]);
            new_points.push_back(points[i*3+1]);
            new_points.push_back(points[i*3+2]);
            if (has_normals) {
                new_normals.push_back(normals[i*3]);
                new_normals.push_back(normals[i*3+1]);
                new_normals.push_back(normals[i*3+2]);
            }
        }
    }

    points = std::move(new_points);
    if (has_normals) normals = std::move(new_normals);
}

// =============================================================================
// C API Implementation
// =============================================================================

SMR_API PointCloudHandle smr_pointcloud_create(void) {
    return new PointCloudImpl();
}

SMR_API void smr_pointcloud_destroy(PointCloudHandle handle) {
    delete static_cast<PointCloudImpl*>(handle);
}

SMR_API SMRErrorCode smr_pointcloud_load_ply(PointCloudHandle handle, const char* filepath) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (!filepath) return SMR_ERROR_INVALID_PARAMETER;
    
    auto* pc = static_cast<PointCloudImpl*>(handle);
    return pc->load_ply(filepath) ? SMR_SUCCESS : SMR_ERROR_FILE_NOT_FOUND;
}

SMR_API SMRErrorCode smr_pointcloud_load_pcd(PointCloudHandle handle, const char* filepath) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (!filepath) return SMR_ERROR_INVALID_PARAMETER;
    
    auto* pc = static_cast<PointCloudImpl*>(handle);
    return pc->load_pcd(filepath) ? SMR_SUCCESS : SMR_ERROR_FILE_NOT_FOUND;
}

SMR_API SMRErrorCode smr_pointcloud_set_points(PointCloudHandle handle, 
                                                const float* points, int count) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (!points || count <= 0) return SMR_ERROR_INVALID_PARAMETER;
    
    auto* pc = static_cast<PointCloudImpl*>(handle);
    pc->clear();
    pc->points.assign(points, points + count * 3);
    return SMR_SUCCESS;
}

SMR_API int smr_pointcloud_get_count(PointCloudHandle handle) {
    if (!handle) return -1;
    return static_cast<PointCloudImpl*>(handle)->count();
}

SMR_API SMRErrorCode smr_pointcloud_get_points(PointCloudHandle handle, float* out_points) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (!out_points) return SMR_ERROR_INVALID_PARAMETER;
    
    auto* pc = static_cast<PointCloudImpl*>(handle);
    std::memcpy(out_points, pc->points.data(), pc->points.size() * sizeof(float));
    return SMR_SUCCESS;
}

SMR_API bool smr_pointcloud_has_normals(PointCloudHandle handle) {
    if (!handle) return false;
    return static_cast<PointCloudImpl*>(handle)->has_normals;
}

SMR_API SMRErrorCode smr_pointcloud_get_normals(PointCloudHandle handle, float* out_normals) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (!out_normals) return SMR_ERROR_INVALID_PARAMETER;
    
    auto* pc = static_cast<PointCloudImpl*>(handle);
    if (!pc->has_normals) return SMR_ERROR_COMPUTATION_FAILED;
    
    std::memcpy(out_normals, pc->normals.data(), pc->normals.size() * sizeof(float));
    return SMR_SUCCESS;
}

SMR_API SMRErrorCode smr_pointcloud_estimate_normals_knn(PointCloudHandle handle, int k) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (k <= 0) return SMR_ERROR_INVALID_PARAMETER;
    
    static_cast<PointCloudImpl*>(handle)->estimate_normals_knn(k);
    return SMR_SUCCESS;
}

SMR_API SMRErrorCode smr_pointcloud_estimate_normals_radius(PointCloudHandle handle, float radius) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (radius <= 0) return SMR_ERROR_INVALID_PARAMETER;
    
    static_cast<PointCloudImpl*>(handle)->estimate_normals_radius(radius);
    return SMR_SUCCESS;
}

SMR_API SMRErrorCode smr_pointcloud_orient_normals(PointCloudHandle handle,
                                                    float cx, float cy, float cz) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    static_cast<PointCloudImpl*>(handle)->orient_normals(cx, cy, cz);
    return SMR_SUCCESS;
}

SMR_API SMRErrorCode smr_pointcloud_downsample_voxel(PointCloudHandle handle, float voxel_size) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (voxel_size <= 0) return SMR_ERROR_INVALID_PARAMETER;
    
    static_cast<PointCloudImpl*>(handle)->downsample_voxel(voxel_size);
    return SMR_SUCCESS;
}

SMR_API SMRErrorCode smr_pointcloud_remove_outliers(PointCloudHandle handle,
                                                     int nb_neighbors, float std_ratio) {
    if (!handle) return SMR_ERROR_INVALID_HANDLE;
    if (nb_neighbors <= 0 || std_ratio <= 0) return SMR_ERROR_INVALID_PARAMETER;
    
    static_cast<PointCloudImpl*>(handle)->remove_outliers(nb_neighbors, std_ratio);
    return SMR_SUCCESS;
}

SMR_API const char* smr_get_last_error(void) {
    return g_last_error;
}

SMR_API const char* smr_get_version(void) {
    return "1.0.0";
}

// Include map for voxel downsampling
#include <map>
