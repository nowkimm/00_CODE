// =============================================================================
// NativeTypes.cs - Native Type Definitions for P/Invoke
// =============================================================================
using System;
using System.Runtime.InteropServices;

namespace SMRWelding.Native
{
    /// <summary>
    /// Error codes returned by native functions
    /// </summary>
    public enum SMRErrorCode
    {
        Success = 0,
        InvalidHandle = -1,
        InvalidParameter = -2,
        OutOfMemory = -3,
        FileNotFound = -4,
        FileIOError = -5,
        InvalidFormat = -6,
        NoSolution = -7,
        Timeout = -8,
        Unknown = -99
    }

    /// <summary>
    /// Predefined robot types
    /// </summary>
    public enum RobotType
    {
        UR5 = 0,
        UR10 = 1,
        KukaKR6R700 = 2,
        DoosanM1013 = 3,
        Custom = 99
    }

    /// <summary>
    /// Weave pattern types for welding
    /// </summary>
    public enum WeaveType
    {
        None = 0,
        Zigzag = 1,
        Circular = 2,
        Triangle = 3,
        Figure8 = 4
    }

    /// <summary>
    /// Denavit-Hartenberg parameters for a single joint
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DHParams
    {
        public double a;           // Link length
        public double alpha;       // Link twist
        public double d;           // Link offset
        public double theta_offset; // Joint angle offset

        public DHParams(double a, double alpha, double d, double thetaOffset = 0)
        {
            this.a = a;
            this.alpha = alpha;
            this.d = d;
            this.theta_offset = thetaOffset;
        }
    }

    /// <summary>
    /// Joint limits for a single joint
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct JointLimits
    {
        public double min_angle;    // Minimum angle (rad)
        public double max_angle;    // Maximum angle (rad)
        public double max_velocity; // Maximum velocity (rad/s)
        public double max_accel;    // Maximum acceleration (rad/s²)

        public JointLimits(double minAngle, double maxAngle, double maxVel = 3.14, double maxAccel = 5.0)
        {
            min_angle = minAngle;
            max_angle = maxAngle;
            max_velocity = maxVel;
            max_accel = maxAccel;
        }
    }

    /// <summary>
    /// Weld point with position, orientation, and arc length
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WeldPoint
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] position;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] normal;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        public float[] tangent;

        public float arc_length;

        public UnityEngine.Vector3 Position => new UnityEngine.Vector3(position[0], position[1], position[2]);
        public UnityEngine.Vector3 Normal => new UnityEngine.Vector3(normal[0], normal[1], normal[2]);
        public UnityEngine.Vector3 Tangent => new UnityEngine.Vector3(tangent[0], tangent[1], tangent[2]);
    }

    /// <summary>
    /// Path generation parameters
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PathParams
    {
        public float step_size;
        public float standoff_distance;
        public float approach_angle;
        public float travel_angle;
        public WeaveType weave_type;
        public float weave_amplitude;
        public float weave_frequency;

        public static PathParams Default => new PathParams
        {
            step_size = 0.005f,
            standoff_distance = 0.015f,
            approach_angle = 0f,
            travel_angle = 0f,
            weave_type = WeaveType.None,
            weave_amplitude = 0.002f,
            weave_frequency = 2.0f
        };
    }

    /// <summary>
    /// Poisson surface reconstruction settings
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PoissonSettings
    {
        public int depth;
        public float scale;
        [MarshalAs(UnmanagedType.I1)]
        public bool linear_fit;
        public float density_threshold;

        public static PoissonSettings Default => new PoissonSettings
        {
            depth = 8,
            scale = 1.1f,
            linear_fit = false,
            density_threshold = 0.01f
        };
    }

    /// <summary>
    /// 4x4 transformation matrix (row-major)
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Matrix4x4Native
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public double[] m;

        public Matrix4x4Native(double[] values)
        {
            m = new double[16];
            if (values != null && values.Length >= 16)
                Array.Copy(values, m, 16);
        }

        public static Matrix4x4Native Identity
        {
            get
            {
                var mat = new Matrix4x4Native(new double[16]);
                mat.m[0] = mat.m[5] = mat.m[10] = mat.m[15] = 1.0;
                return mat;
            }
        }

        public UnityEngine.Vector3 GetPosition()
        {
            return new UnityEngine.Vector3((float)m[3], (float)m[7], (float)m[11]);
        }

        public UnityEngine.Quaternion GetRotation()
        {
            // Extract rotation from 3x3 submatrix
            var unity = ToUnityMatrix();
            return unity.rotation;
        }

        public UnityEngine.Matrix4x4 ToUnityMatrix()
        {
            var mat = new UnityEngine.Matrix4x4();
            // Convert row-major to Unity's column-major
            mat.m00 = (float)m[0];  mat.m01 = (float)m[1];  mat.m02 = (float)m[2];  mat.m03 = (float)m[3];
            mat.m10 = (float)m[4];  mat.m11 = (float)m[5];  mat.m12 = (float)m[6];  mat.m13 = (float)m[7];
            mat.m20 = (float)m[8];  mat.m21 = (float)m[9];  mat.m22 = (float)m[10]; mat.m23 = (float)m[11];
            mat.m30 = (float)m[12]; mat.m31 = (float)m[13]; mat.m32 = (float)m[14]; mat.m33 = (float)m[15];
            return mat;
        }

        public static Matrix4x4Native FromUnityMatrix(UnityEngine.Matrix4x4 mat)
        {
            var native = new Matrix4x4Native(new double[16]);
            native.m[0] = mat.m00;  native.m[1] = mat.m01;  native.m[2] = mat.m02;  native.m[3] = mat.m03;
            native.m[4] = mat.m10;  native.m[5] = mat.m11;  native.m[6] = mat.m12;  native.m[7] = mat.m13;
            native.m[8] = mat.m20;  native.m[9] = mat.m21;  native.m[10] = mat.m22; native.m[11] = mat.m23;
            native.m[12] = mat.m30; native.m[13] = mat.m31; native.m[14] = mat.m32; native.m[15] = mat.m33;
            return native;
        }
    }

    /// <summary>
    /// Exception thrown when native operations fail
    /// </summary>
    public class SMRNativeException : Exception
    {
        public SMRErrorCode ErrorCode { get; }

        public SMRNativeException(SMRErrorCode code)
            : base($"Native operation failed with error: {code}")
        {
            ErrorCode = code;
        }

        public SMRNativeException(SMRErrorCode code, string message)
            : base(message)
        {
            ErrorCode = code;
        }
    }
}
