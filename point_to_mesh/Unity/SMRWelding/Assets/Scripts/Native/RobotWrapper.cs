// =============================================================================
// RobotWrapper.cs - High-level Robot Kinematics API
// =============================================================================
using System;
using UnityEngine;

namespace SMRWelding.Native
{
    /// <summary>
    /// High-level wrapper for robot kinematics operations
    /// </summary>
    public class RobotWrapper : IDisposable
    {
        private IntPtr _handle;
        private bool _disposed;
        private RobotType _type;

        public IntPtr Handle => _handle;
        public bool IsValid => _handle != IntPtr.Zero;
        public RobotType Type => _type;

        /// <summary>
        /// Create robot from preset type
        /// </summary>
        public RobotWrapper(RobotType type)
        {
            _type = type;
            _handle = NativeBindings.smr_robot_create(type);
            if (_handle == IntPtr.Zero)
                throw new SMRNativeException(SMRErrorCode.OutOfMemory, "Failed to create robot");
        }

        /// <summary>
        /// Create robot with custom DH parameters
        /// </summary>
        public RobotWrapper(DHParams[] dhParams, JointLimits[] jointLimits)
        {
            if (dhParams == null || dhParams.Length != 6)
                throw new ArgumentException("DH parameters must have 6 joints");
            if (jointLimits == null || jointLimits.Length != 6)
                throw new ArgumentException("Joint limits must have 6 joints");

            _type = RobotType.Custom;
            _handle = NativeBindings.smr_robot_create_custom(dhParams, jointLimits);
            if (_handle == IntPtr.Zero)
                throw new SMRNativeException(SMRErrorCode.OutOfMemory, "Failed to create custom robot");
        }

        ~RobotWrapper()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && _handle != IntPtr.Zero)
            {
                NativeBindings.smr_robot_destroy(_handle);
                _handle = IntPtr.Zero;
            }
            _disposed = true;
        }

        /// <summary>
        /// Compute forward kinematics
        /// </summary>
        public Matrix4x4 ForwardKinematics(double[] jointAngles)
        {
            ThrowIfDisposed();
            if (jointAngles == null || jointAngles.Length != 6)
                throw new ArgumentException("Joint angles must have 6 values");

            double[] transform = new double[16];
            var result = NativeBindings.smr_robot_forward_kinematics(_handle, jointAngles, transform);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);

            return ConvertToUnityMatrix(transform);
        }

        /// <summary>
        /// Compute inverse kinematics (returns all solutions)
        /// </summary>
        public double[][] InverseKinematics(Matrix4x4 targetPose)
        {
            ThrowIfDisposed();
            double[] transform = ConvertFromUnityMatrix(targetPose);
            double[] solutions = new double[8 * 6]; // Max 8 solutions
            int count;

            var result = NativeBindings.smr_robot_inverse_kinematics(_handle, transform, solutions, out count);
            if (result == SMRErrorCode.NoSolution)
                return Array.Empty<double[]>();
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);

            double[][] results = new double[count][];
            for (int i = 0; i < count; i++)
            {
                results[i] = new double[6];
                Array.Copy(solutions, i * 6, results[i], 0, 6);
            }
            return results;
        }

        /// <summary>
        /// Compute inverse kinematics (nearest to reference)
        /// </summary>
        public double[] InverseKinematicsNearest(Matrix4x4 targetPose, double[] referenceAngles)
        {
            ThrowIfDisposed();
            if (referenceAngles == null || referenceAngles.Length != 6)
                throw new ArgumentException("Reference angles must have 6 values");

            double[] transform = ConvertFromUnityMatrix(targetPose);
            double[] solution = new double[6];

            var result = NativeBindings.smr_robot_ik_nearest(_handle, transform, referenceAngles, solution);
            if (result == SMRErrorCode.NoSolution)
                return null;
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);

            return solution;
        }

        /// <summary>
        /// Compute Jacobian matrix
        /// </summary>
        public double[,] ComputeJacobian(double[] jointAngles)
        {
            ThrowIfDisposed();
            if (jointAngles == null || jointAngles.Length != 6)
                throw new ArgumentException("Joint angles must have 6 values");

            double[] jacobian = new double[36];
            var result = NativeBindings.smr_robot_compute_jacobian(_handle, jointAngles, jacobian);
            if (result != SMRErrorCode.Success)
                throw new SMRNativeException(result);

            double[,] J = new double[6, 6];
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 6; j++)
                    J[i, j] = jacobian[i * 6 + j];
            return J;
        }

        /// <summary>
        /// Get manipulability index
        /// </summary>
        public double GetManipulability(double[] jointAngles)
        {
            ThrowIfDisposed();
            if (jointAngles == null || jointAngles.Length != 6)
                throw new ArgumentException("Joint angles must have 6 values");

            return NativeBindings.smr_robot_get_manipulability(_handle, jointAngles);
        }

        /// <summary>
        /// Check if joint angles are within limits
        /// </summary>
        public bool CheckJointLimits(double[] jointAngles)
        {
            ThrowIfDisposed();
            if (jointAngles == null || jointAngles.Length != 6)
                throw new ArgumentException("Joint angles must have 6 values");

            return NativeBindings.smr_robot_check_joint_limits(_handle, jointAngles);
        }

        /// <summary>
        /// Get end-effector position and rotation
        /// </summary>
        public (Vector3 position, Quaternion rotation) GetEndEffectorPose(double[] jointAngles)
        {
            var matrix = ForwardKinematics(jointAngles);
            return (matrix.GetColumn(3), matrix.rotation);
        }

        private static Matrix4x4 ConvertToUnityMatrix(double[] m)
        {
            var mat = new Matrix4x4();
            mat.m00 = (float)m[0];  mat.m01 = (float)m[1];  mat.m02 = (float)m[2];  mat.m03 = (float)m[3];
            mat.m10 = (float)m[4];  mat.m11 = (float)m[5];  mat.m12 = (float)m[6];  mat.m13 = (float)m[7];
            mat.m20 = (float)m[8];  mat.m21 = (float)m[9];  mat.m22 = (float)m[10]; mat.m23 = (float)m[11];
            mat.m30 = (float)m[12]; mat.m31 = (float)m[13]; mat.m32 = (float)m[14]; mat.m33 = (float)m[15];
            return mat;
        }

        private static double[] ConvertFromUnityMatrix(Matrix4x4 mat)
        {
            return new double[] {
                mat.m00, mat.m01, mat.m02, mat.m03,
                mat.m10, mat.m11, mat.m12, mat.m13,
                mat.m20, mat.m21, mat.m22, mat.m23,
                mat.m30, mat.m31, mat.m32, mat.m33
            };
        }

        private void ThrowIfDisposed()
        {
            if (_disposed || _handle == IntPtr.Zero)
                throw new ObjectDisposedException(nameof(RobotWrapper));
        }
    }
}
