// =============================================================================
// NativeBindings.cs - P/Invoke Declarations for Native Plugin
// =============================================================================
using System;
using System.Runtime.InteropServices;

namespace SMRWelding.Native
{
    /// <summary>
    /// P/Invoke declarations for the SMR Welding native library
    /// </summary>
    public static class NativeBindings
    {
        private const string DLL_NAME = "smr_welding";

        // =====================================================================
        // Utility Functions
        // =====================================================================
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr smr_get_last_error();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int smr_get_version();

        public static string GetLastError()
        {
            IntPtr ptr = smr_get_last_error();
            return ptr != IntPtr.Zero ? Marshal.PtrToStringAnsi(ptr) : string.Empty;
        }

        // =====================================================================
        // Point Cloud Functions
        // =====================================================================
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr smr_pointcloud_create();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void smr_pointcloud_destroy(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern SMRErrorCode smr_pointcloud_load_ply(IntPtr handle, string path);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern SMRErrorCode smr_pointcloud_load_pcd(IntPtr handle, string path);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_pointcloud_set_points(IntPtr handle, float[] points, int count);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int smr_pointcloud_get_count(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_pointcloud_get_points(IntPtr handle, float[] out_points);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool smr_pointcloud_has_normals(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_pointcloud_get_normals(IntPtr handle, float[] out_normals);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_pointcloud_estimate_normals_knn(IntPtr handle, int k);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_pointcloud_estimate_normals_radius(IntPtr handle, float radius);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_pointcloud_orient_normals(IntPtr handle, float[] camera_pos);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_pointcloud_downsample_voxel(IntPtr handle, float voxel_size);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_pointcloud_remove_outliers(IntPtr handle, int nb_neighbors, float std_ratio);

        // =====================================================================
        // Mesh Functions
        // =====================================================================
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr smr_mesh_create_poisson(IntPtr pc_handle, ref PoissonSettings settings);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void smr_mesh_destroy(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int smr_mesh_get_vertex_count(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int smr_mesh_get_triangle_count(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_mesh_get_vertices(IntPtr handle, float[] out_vertices);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_mesh_get_normals(IntPtr handle, float[] out_normals);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_mesh_get_triangles(IntPtr handle, int[] out_triangles);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_mesh_remove_low_density(IntPtr handle, float quantile);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_mesh_simplify(IntPtr handle, int target_triangles);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern SMRErrorCode smr_mesh_save_ply(IntPtr handle, string path);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern SMRErrorCode smr_mesh_save_obj(IntPtr handle, string path);

        // =====================================================================
        // Robot Functions
        // =====================================================================
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr smr_robot_create(RobotType type);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr smr_robot_create_custom(
            [MarshalAs(UnmanagedType.LPArray, SizeConst = 6)] DHParams[] dh_params,
            [MarshalAs(UnmanagedType.LPArray, SizeConst = 6)] JointLimits[] joint_limits);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void smr_robot_destroy(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_robot_forward_kinematics(
            IntPtr handle, double[] joint_angles, double[] out_transform);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_robot_inverse_kinematics(
            IntPtr handle, double[] target_transform, double[] out_solutions, out int out_count);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_robot_ik_nearest(
            IntPtr handle, double[] target_transform, double[] reference_angles, double[] out_angles);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_robot_compute_jacobian(
            IntPtr handle, double[] joint_angles, double[] out_jacobian);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern double smr_robot_get_manipulability(IntPtr handle, double[] joint_angles);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool smr_robot_check_joint_limits(IntPtr handle, double[] joint_angles);

        // =====================================================================
        // Path Functions
        // =====================================================================
        
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr smr_path_create_from_edge(IntPtr mesh_handle, ref PathParams parameters);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr smr_path_create_from_points(
            float[] points, float[] normals, int count, ref PathParams parameters);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void smr_path_destroy(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int smr_path_get_count(IntPtr handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_path_get_points(IntPtr handle, 
            [Out, MarshalAs(UnmanagedType.LPArray)] WeldPoint[] out_points);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_path_apply_weave(
            IntPtr handle, WeaveType weave_type, float amplitude, float frequency);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_path_resample(IntPtr handle, float step_size);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_path_smooth(IntPtr handle, int window_size);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern SMRErrorCode smr_path_to_joints(
            IntPtr path_handle, IntPtr robot_handle, float standoff,
            double[] out_joints, [MarshalAs(UnmanagedType.LPArray)] bool[] out_reachable);
    }
}
