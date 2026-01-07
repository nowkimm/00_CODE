// =============================================================================
// RobotModel.cs - Robot Kinematics Data Structure
// =============================================================================
using System;
using UnityEngine;

namespace SMRWelding.Robot
{
    /// <summary>
    /// Supported robot types
    /// </summary>
    public enum RobotType
    {
        UR5 = 0,
        UR10 = 1,
        KUKA_KR6 = 2,
        Doosan_M1013 = 3,
        Custom = 99
    }

    /// <summary>
    /// DH (Denavit-Hartenberg) parameters for a single joint
    /// </summary>
    [Serializable]
    public struct DHParameter
    {
        public float A;         // Link length (mm)
        public float D;         // Link offset (mm)
        public float Alpha;     // Link twist (radians)
        public float Theta;     // Joint angle (radians) - variable for revolute

        public DHParameter(float a, float d, float alpha)
        {
            A = a;
            D = d;
            Alpha = alpha;
            Theta = 0;
        }

        /// <summary>
        /// Get transformation matrix for this DH parameter
        /// </summary>
        public Matrix4x4 GetTransformMatrix(float jointAngle)
        {
            float theta = Theta + jointAngle;
            float ct = Mathf.Cos(theta);
            float st = Mathf.Sin(theta);
            float ca = Mathf.Cos(Alpha);
            float sa = Mathf.Sin(Alpha);

            // Convert mm to meters for Unity
            float a_m = A * 0.001f;
            float d_m = D * 0.001f;

            Matrix4x4 m = new Matrix4x4();
            m.m00 = ct;  m.m01 = -st * ca; m.m02 = st * sa;   m.m03 = a_m * ct;
            m.m10 = st;  m.m11 = ct * ca;  m.m12 = -ct * sa;  m.m13 = a_m * st;
            m.m20 = 0;   m.m21 = sa;       m.m22 = ca;        m.m23 = d_m;
            m.m30 = 0;   m.m31 = 0;        m.m32 = 0;         m.m33 = 1;

            return m;
        }
    }

    /// <summary>
    /// Joint limits for a single joint
    /// </summary>
    [Serializable]
    public struct JointLimit
    {
        public float Min;       // radians
        public float Max;       // radians
        public float MaxSpeed;  // rad/s

        public JointLimit(float min, float max, float maxSpeed = 3.14f)
        {
            Min = min;
            Max = max;
            MaxSpeed = maxSpeed;
        }

        public float Clamp(float angle) => Mathf.Clamp(angle, Min, Max);
        public bool IsValid(float angle) => angle >= Min && angle <= Max;
    }

    /// <summary>
    /// Complete robot model with kinematics
    /// </summary>
    [Serializable]
    public class RobotModel
    {
        public string Name;
        public RobotType Type;
        public int DOF = 6;
        public DHParameter[] DHParams;
        public JointLimit[] JointLimits;
        public Vector3 BasePosition;
        public Quaternion BaseRotation;

        public float[] CurrentJoints;

        public RobotModel() { }

        public RobotModel(RobotType type)
        {
            Type = type;
            InitializeFromType(type);
        }

        /// <summary>
        /// Initialize DH parameters from robot type
        /// </summary>
        private void InitializeFromType(RobotType type)
        {
            switch (type)
            {
                case RobotType.UR5:
                    InitializeUR5();
                    break;
                case RobotType.UR10:
                    InitializeUR10();
                    break;
                case RobotType.KUKA_KR6:
                    InitializeKUKA();
                    break;
                case RobotType.Doosan_M1013:
                    InitializeDoosan();
                    break;
                default:
                    InitializeUR5(); // Default
                    break;
            }

            CurrentJoints = new float[DOF];
            BasePosition = Vector3.zero;
            BaseRotation = Quaternion.identity;
        }

        private void InitializeUR5()
        {
            Name = "Universal Robots UR5";
            DOF = 6;
            
            DHParams = new DHParameter[]
            {
                new DHParameter(0, 89.159f, Mathf.PI / 2),
                new DHParameter(-425, 0, 0),
                new DHParameter(-392.25f, 0, 0),
                new DHParameter(0, 109.15f, Mathf.PI / 2),
                new DHParameter(0, 94.65f, -Mathf.PI / 2),
                new DHParameter(0, 82.3f, 0)
            };

            float limit = Mathf.PI * 2;
            JointLimits = new JointLimit[]
            {
                new JointLimit(-limit, limit, 3.14f),
                new JointLimit(-limit, limit, 3.14f),
                new JointLimit(-limit, limit, 3.14f),
                new JointLimit(-limit, limit, 6.28f),
                new JointLimit(-limit, limit, 6.28f),
                new JointLimit(-limit, limit, 6.28f)
            };
        }

        private void InitializeUR10()
        {
            Name = "Universal Robots UR10";
            DOF = 6;
            
            DHParams = new DHParameter[]
            {
                new DHParameter(0, 118.0f, Mathf.PI / 2),
                new DHParameter(-612.0f, 0, 0),
                new DHParameter(-572.3f, 0, 0),
                new DHParameter(0, 163.9f, Mathf.PI / 2),
                new DHParameter(0, 115.7f, -Mathf.PI / 2),
                new DHParameter(0, 92.2f, 0)
            };

            float limit = Mathf.PI * 2;
            JointLimits = new JointLimit[]
            {
                new JointLimit(-limit, limit, 2.09f),
                new JointLimit(-limit, limit, 2.09f),
                new JointLimit(-limit, limit, 3.14f),
                new JointLimit(-limit, limit, 3.14f),
                new JointLimit(-limit, limit, 3.14f),
                new JointLimit(-limit, limit, 3.14f)
            };
        }

        private void InitializeKUKA()
        {
            Name = "KUKA KR6-R700";
            DOF = 6;
            
            DHParams = new DHParameter[]
            {
                new DHParameter(25, 400, -Mathf.PI / 2),
                new DHParameter(315, 0, 0),
                new DHParameter(35, 0, Mathf.PI / 2),
                new DHParameter(0, 365, -Mathf.PI / 2),
                new DHParameter(0, 0, Mathf.PI / 2),
                new DHParameter(0, 80, 0)
            };

            JointLimits = new JointLimit[]
            {
                new JointLimit(-170 * Mathf.Deg2Rad, 170 * Mathf.Deg2Rad, 3.49f),
                new JointLimit(-190 * Mathf.Deg2Rad, 45 * Mathf.Deg2Rad, 3.49f),
                new JointLimit(-120 * Mathf.Deg2Rad, 156 * Mathf.Deg2Rad, 3.84f),
                new JointLimit(-185 * Mathf.Deg2Rad, 185 * Mathf.Deg2Rad, 5.24f),
                new JointLimit(-120 * Mathf.Deg2Rad, 120 * Mathf.Deg2Rad, 5.24f),
                new JointLimit(-350 * Mathf.Deg2Rad, 350 * Mathf.Deg2Rad, 7.33f)
            };
        }

        private void InitializeDoosan()
        {
            Name = "Doosan M1013";
            DOF = 6;
            
            DHParams = new DHParameter[]
            {
                new DHParameter(0, 135.0f, Mathf.PI / 2),
                new DHParameter(-411.0f, 0, 0),
                new DHParameter(-368.0f, 0, 0),
                new DHParameter(0, 126.5f, Mathf.PI / 2),
                new DHParameter(0, 113.5f, -Mathf.PI / 2),
                new DHParameter(0, 92.0f, 0)
            };

            float limit = Mathf.PI * 2;
            JointLimits = new JointLimit[]
            {
                new JointLimit(-limit, limit, 2.62f),
                new JointLimit(-limit, limit, 2.62f),
                new JointLimit(-2.57f, 2.57f, 2.62f),
                new JointLimit(-limit, limit, 3.93f),
                new JointLimit(-limit, limit, 3.93f),
                new JointLimit(-limit, limit, 3.93f)
            };
        }

        /// <summary>
        /// Forward Kinematics - calculate end effector pose from joint angles
        /// </summary>
        public Matrix4x4 ForwardKinematics(float[] joints)
        {
            if (joints == null || joints.Length != DOF)
                throw new ArgumentException($"Expected {DOF} joints");

            Matrix4x4 T = Matrix4x4.TRS(BasePosition, BaseRotation, Vector3.one);

            for (int i = 0; i < DOF; i++)
            {
                T *= DHParams[i].GetTransformMatrix(joints[i]);
            }

            return T;
        }

        /// <summary>
        /// Get end effector position and rotation
        /// </summary>
        public (Vector3 position, Quaternion rotation) GetEndEffectorPose(float[] joints)
        {
            Matrix4x4 T = ForwardKinematics(joints);
            Vector3 pos = T.GetColumn(3);
            Quaternion rot = T.rotation;
            return (pos, rot);
        }

        /// <summary>
        /// Get all joint transforms for visualization
        /// </summary>
        public Matrix4x4[] GetJointTransforms(float[] joints)
        {
            if (joints == null || joints.Length != DOF)
                throw new ArgumentException($"Expected {DOF} joints");

            var transforms = new Matrix4x4[DOF + 1];
            Matrix4x4 T = Matrix4x4.TRS(BasePosition, BaseRotation, Vector3.one);
            transforms[0] = T;

            for (int i = 0; i < DOF; i++)
            {
                T *= DHParams[i].GetTransformMatrix(joints[i]);
                transforms[i + 1] = T;
            }

            return transforms;
        }

        /// <summary>
        /// Check if joint configuration is valid
        /// </summary>
        public bool IsValidConfiguration(float[] joints)
        {
            if (joints == null || joints.Length != DOF) return false;

            for (int i = 0; i < DOF; i++)
            {
                if (!JointLimits[i].IsValid(joints[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Clamp joints to limits
        /// </summary>
        public float[] ClampJoints(float[] joints)
        {
            var clamped = new float[DOF];
            for (int i = 0; i < DOF; i++)
            {
                clamped[i] = JointLimits[i].Clamp(joints[i]);
            }
            return clamped;
        }

        /// <summary>
        /// Get reach (approximate workspace radius)
        /// </summary>
        public float GetReach()
        {
            float reach = 0;
            for (int i = 0; i < DOF; i++)
            {
                reach += Mathf.Abs(DHParams[i].A) * 0.001f;
            }
            return reach;
        }

        /// <summary>
        /// Create preset robot model
        /// </summary>
        public static RobotModel CreatePreset(RobotType type)
        {
            return new RobotModel(type);
        }
    }
}
