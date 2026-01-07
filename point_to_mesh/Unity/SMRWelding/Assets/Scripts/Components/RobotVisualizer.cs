// =============================================================================
// RobotVisualizer.cs - Robot Arm Visualization Component
// =============================================================================
using System;
using UnityEngine;

namespace SMRWelding.Components
{
    using Native;

    /// <summary>
    /// Visualizes 6-DOF robot arm with joint animation
    /// </summary>
    public class RobotVisualizer : MonoBehaviour
    {
        [Header("Robot Configuration")]
        [SerializeField] private RobotType robotType = RobotType.UR5;
        
        [Header("Joint Transforms")]
        [SerializeField] private Transform[] jointTransforms = new Transform[6];
        [SerializeField] private Transform endEffector;
        [SerializeField] private Transform toolTip;

        [Header("Visualization")]
        [SerializeField] private bool showJointAxes = true;
        [SerializeField] private float axisLength = 0.1f;
        [SerializeField] private bool showWorkspace = false;
        [SerializeField] private float workspaceRadius = 0.85f;

        [Header("Animation")]
        [SerializeField] private float animationSpeed = 1.0f;
        [SerializeField] private bool smoothAnimation = true;
        [SerializeField] private float smoothTime = 0.1f;

        [Header("Colors")]
        [SerializeField] private Color jointColor = Color.blue;
        [SerializeField] private Color endEffectorColor = Color.green;
        [SerializeField] private Color unreachableColor = Color.red;

        private RobotWrapper _robot;
        private double[] _currentJoints = new double[6];
        private double[] _targetJoints = new double[6];
        private double[] _jointVelocities = new double[6];
        private bool _isAnimating;
        private int _trajectoryIndex;
        private double[][] _trajectory;
        private Action _onTrajectoryComplete;

        public double[] CurrentJoints => _currentJoints;
        public bool IsAnimating => _isAnimating;

        private void Awake()
        {
            InitializeRobot();
        }

        private void OnDestroy()
        {
            _robot?.Dispose();
        }

        private void InitializeRobot()
        {
            _robot?.Dispose();
            _robot = new RobotWrapper(robotType);

            // Initialize to home position
            for (int i = 0; i < 6; i++)
            {
                _currentJoints[i] = 0;
                _targetJoints[i] = 0;
            }
        }

        /// <summary>
        /// Set joint angles directly
        /// </summary>
        public void SetJoints(double[] joints)
        {
            if (joints == null || joints.Length != 6)
                throw new ArgumentException("Joints must be array of 6");

            Array.Copy(joints, _targetJoints, 6);
            
            if (!smoothAnimation)
            {
                Array.Copy(joints, _currentJoints, 6);
                UpdateJointTransforms();
            }
        }

        /// <summary>
        /// Move to target pose using IK
        /// </summary>
        public bool MoveToPose(Vector3 position, Quaternion rotation)
        {
            Matrix4x4 targetPose = Matrix4x4.TRS(position, rotation, Vector3.one);
            
            var solutions = _robot.InverseKinematics(targetPose);
            if (solutions == null || solutions.Length == 0)
                return false;

            // Use nearest solution
            var nearest = _robot.InverseKinematicsNearest(targetPose, _currentJoints);
            if (nearest != null)
            {
                SetJoints(nearest);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Play trajectory animation
        /// </summary>
        public void PlayTrajectory(double[][] trajectory, Action onComplete = null)
        {
            if (trajectory == null || trajectory.Length == 0)
                return;

            _trajectory = trajectory;
            _trajectoryIndex = 0;
            _isAnimating = true;
            _onTrajectoryComplete = onComplete;
            
            SetJoints(_trajectory[0]);
        }

        /// <summary>
        /// Stop trajectory animation
        /// </summary>
        public void StopAnimation()
        {
            _isAnimating = false;
            _trajectory = null;
            _onTrajectoryComplete = null;
        }

        /// <summary>
        /// Pause/Resume animation
        /// </summary>
        public void SetAnimationPaused(bool paused)
        {
            if (_trajectory != null)
                _isAnimating = !paused;
        }

        private void Update()
        {
            // Smooth joint animation
            if (smoothAnimation)
            {
                bool allReached = true;
                for (int i = 0; i < 6; i++)
                {
                    double current = _currentJoints[i];
                    double target = _targetJoints[i];
                    
                    _currentJoints[i] = SmoothDamp(current, target, ref _jointVelocities[i], smoothTime);
                    
                    if (Math.Abs(_currentJoints[i] - target) > 0.001)
                        allReached = false;
                }
                
                UpdateJointTransforms();

                // Handle trajectory playback
                if (_isAnimating && allReached && _trajectory != null)
                {
                    _trajectoryIndex++;
                    if (_trajectoryIndex < _trajectory.Length)
                    {
                        SetJoints(_trajectory[_trajectoryIndex]);
                    }
                    else
                    {
                        _isAnimating = false;
                        _onTrajectoryComplete?.Invoke();
                        _onTrajectoryComplete = null;
                    }
                }
            }
        }

        private double SmoothDamp(double current, double target, ref double velocity, float smoothTime)
        {
            float dt = Time.deltaTime * animationSpeed;
            double omega = 2f / smoothTime;
            double x = omega * dt;
            double exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            double change = current - target;
            double temp = (velocity + omega * change) * dt;
            velocity = (velocity - omega * temp) * exp;
            return target + (change + temp) * exp;
        }

        private void UpdateJointTransforms()
        {
            if (jointTransforms == null) return;

            for (int i = 0; i < 6 && i < jointTransforms.Length; i++)
            {
                if (jointTransforms[i] == null) continue;

                // Rotate around local Z axis (typical robot joint configuration)
                float angleDeg = (float)(_currentJoints[i] * Mathf.Rad2Deg);
                jointTransforms[i].localRotation = Quaternion.Euler(0, 0, angleDeg);
            }

            // Update end effector using FK
            if (endEffector != null && _robot != null)
            {
                var fk = _robot.ForwardKinematics(_currentJoints);
                endEffector.position = fk.GetColumn(3);
                endEffector.rotation = fk.rotation;
            }
        }

        /// <summary>
        /// Get current end effector pose
        /// </summary>
        public (Vector3 position, Quaternion rotation) GetEndEffectorPose()
        {
            if (_robot == null)
                return (Vector3.zero, Quaternion.identity);

            return _robot.GetEndEffectorPose(_currentJoints);
        }

        /// <summary>
        /// Check if pose is reachable
        /// </summary>
        public bool IsPoseReachable(Vector3 position, Quaternion rotation)
        {
            if (_robot == null) return false;

            Matrix4x4 targetPose = Matrix4x4.TRS(position, rotation, Vector3.one);
            var solutions = _robot.InverseKinematics(targetPose);
            return solutions != null && solutions.Length > 0;
        }

        /// <summary>
        /// Get manipulability at current configuration
        /// </summary>
        public double GetManipulability()
        {
            return _robot?.GetManipulability(_currentJoints) ?? 0;
        }

        private void OnDrawGizmos()
        {
            if (showWorkspace)
            {
                Gizmos.color = new Color(0, 1, 0, 0.1f);
                Gizmos.DrawWireSphere(transform.position, workspaceRadius);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showJointAxes || jointTransforms == null) return;

            for (int i = 0; i < jointTransforms.Length; i++)
            {
                if (jointTransforms[i] == null) continue;

                Vector3 pos = jointTransforms[i].position;
                
                // Draw joint axis (Z)
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(pos, pos + jointTransforms[i].forward * axisLength);
                
                // Draw X and Y
                Gizmos.color = Color.red;
                Gizmos.DrawLine(pos, pos + jointTransforms[i].right * axisLength * 0.5f);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, pos + jointTransforms[i].up * axisLength * 0.5f);
            }

            // Draw end effector
            if (endEffector != null)
            {
                Gizmos.color = endEffectorColor;
                Gizmos.DrawWireSphere(endEffector.position, 0.02f);
            }
        }
    }
}
