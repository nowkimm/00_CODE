// =============================================================================
// QuickStartDemo.cs - Quick Start Demo for SMR Welding System
// =============================================================================
using UnityEngine;
using SMRWelding.Core;
using SMRWelding.Components;
using SMRWelding.Utilities;

namespace SMRWelding
{
    /// <summary>
    /// Quick start demo that runs the welding pipeline with sample data
    /// </summary>
    public class QuickStartDemo : MonoBehaviour
    {
        [Header("Demo Settings")]
        [SerializeField] private DemoShape demoShape = DemoShape.Hemisphere;
        [SerializeField] private int pointCount = 10000;
        [SerializeField] private bool autoRunOnStart = true;

        [Header("Pipeline Settings")]
        [SerializeField] private float voxelSize = 0.005f;
        [SerializeField] private int meshResolution = 32;
        [SerializeField] private float pathStepSize = 0.01f;
        [SerializeField] private RobotType robotType = RobotType.UR5;

        [Header("References")]
        [SerializeField] private MeshFilter meshOutput;
        [SerializeField] private MeshRenderer meshRenderer;
        [SerializeField] private PathVisualizer pathVisualizer;
        [SerializeField] private PointCloudVisualizer pointCloudVisualizer;

        [Header("Status")]
        [SerializeField] private string currentStatus = "Ready";

        public enum DemoShape
        {
            Hemisphere,
            Cylinder,
            Torus,
            SMRVessel
        }

        private void Start()
        {
            // Create required components if not assigned
            EnsureComponents();

            if (autoRunOnStart)
            {
                RunDemo();
            }
        }

        private void EnsureComponents()
        {
            if (meshOutput == null)
            {
                var meshGO = new GameObject("Generated Mesh");
                meshGO.transform.SetParent(transform);
                meshOutput = meshGO.AddComponent<MeshFilter>();
                meshRenderer = meshGO.AddComponent<MeshRenderer>();
                meshRenderer.material = new Material(Shader.Find("Standard"));
                meshRenderer.material.color = new Color(0.7f, 0.7f, 0.7f);
            }

            if (pathVisualizer == null)
            {
                var pathGO = new GameObject("Path Visualizer");
                pathGO.transform.SetParent(transform);
                pathVisualizer = pathGO.AddComponent<PathVisualizer>();
            }

            if (pointCloudVisualizer == null)
            {
                var pcGO = new GameObject("Point Cloud Visualizer");
                pcGO.transform.SetParent(transform);
                pointCloudVisualizer = pcGO.AddComponent<PointCloudVisualizer>();
            }
        }

        [ContextMenu("Run Demo")]
        public void RunDemo()
        {
            currentStatus = "Generating points...";
            Debug.Log($"[QuickStartDemo] Starting demo with {demoShape} shape, {pointCount} points");

            // Step 1: Generate sample points
            Vector3[] points = GenerateSamplePoints();
            Vector3[] normals = SampleDataGenerator.EstimateNormals(points, Vector3.zero);
            Color[] colors = SampleDataGenerator.GenerateHeightColors(points, 
                new Color(0.2f, 0.4f, 0.8f), new Color(0.8f, 0.2f, 0.2f));

            // Show point cloud
            pointCloudVisualizer.SetPoints(points, normals, colors);
            currentStatus = "Points generated. Creating mesh...";

            // Step 2: Generate mesh (using simulation mode)
            SimulationMode.Enabled = true;
            Mesh generatedMesh = SimulationMode.GenerateMeshFromPoints(points, meshResolution);
            
            if (generatedMesh != null)
            {
                meshOutput.mesh = generatedMesh;
                currentStatus = "Mesh created. Generating path...";
                Debug.Log($"[QuickStartDemo] Mesh generated: {generatedMesh.vertexCount} vertices");
            }
            else
            {
                currentStatus = "Mesh generation failed";
                Debug.LogWarning("[QuickStartDemo] Failed to generate mesh");
                return;
            }

            // Step 3: Generate welding path
            var (pathPositions, pathNormals) = SimulationMode.GeneratePathFromMesh(generatedMesh, pathStepSize);
            
            if (pathPositions != null && pathPositions.Length > 0)
            {
                pathVisualizer.SetPath(pathPositions, pathNormals);
                currentStatus = "Path generated. Calculating trajectory...";
                Debug.Log($"[QuickStartDemo] Path generated: {pathPositions.Length} points");
            }

            // Step 4: Calculate robot trajectory
            double[][] trajectory = SimulationMode.CalculateTrajectory(pathPositions, pathNormals, robotType);
            
            if (trajectory != null)
            {
                currentStatus = $"Complete! Trajectory: {trajectory.Length} points";
                Debug.Log($"[QuickStartDemo] Trajectory calculated: {trajectory.Length} points");
            }

            Debug.Log("[QuickStartDemo] Demo complete!");
        }

        private Vector3[] GenerateSamplePoints()
        {
            switch (demoShape)
            {
                case DemoShape.Hemisphere:
                    return SampleDataGenerator.GenerateHemisphere(pointCount, 0.5f);
                case DemoShape.Cylinder:
                    return SampleDataGenerator.GenerateCylinder(pointCount, 0.3f, 1.0f);
                case DemoShape.Torus:
                    return SampleDataGenerator.GenerateTorus(pointCount, 0.4f, 0.1f);
                case DemoShape.SMRVessel:
                    return SampleDataGenerator.GenerateSMRVesselSegment(pointCount, 0.5f, 0.05f, 0.8f);
                default:
                    return SampleDataGenerator.GenerateHemisphere(pointCount);
            }
        }

        [ContextMenu("Clear Demo")]
        public void ClearDemo()
        {
            if (meshOutput != null && meshOutput.mesh != null)
            {
                if (Application.isPlaying)
                    Destroy(meshOutput.mesh);
                else
                    DestroyImmediate(meshOutput.mesh);
                meshOutput.mesh = null;
            }

            if (pathVisualizer != null)
                pathVisualizer.Clear();

            if (pointCloudVisualizer != null)
                pointCloudVisualizer.Clear();

            currentStatus = "Ready";
            Debug.Log("[QuickStartDemo] Demo cleared");
        }

        [ContextMenu("Focus Camera on Result")]
        public void FocusCameraOnResult()
        {
            if (meshOutput == null || meshOutput.mesh == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            Bounds bounds = meshOutput.mesh.bounds;
            float distance = bounds.extents.magnitude * 2.5f;
            
            cam.transform.position = bounds.center + new Vector3(0, 0.5f, -1) * distance;
            cam.transform.LookAt(bounds.center);
        }

        private void OnGUI()
        {
            // Simple status display
            GUILayout.BeginArea(new Rect(10, 10, 300, 150));
            GUILayout.Label("SMR Welding Demo", GUI.skin.box);
            GUILayout.Label($"Shape: {demoShape}");
            GUILayout.Label($"Points: {pointCount}");
            GUILayout.Label($"Status: {currentStatus}");
            
            if (GUILayout.Button("Run Demo"))
            {
                RunDemo();
            }
            if (GUILayout.Button("Clear"))
            {
                ClearDemo();
            }
            GUILayout.EndArea();
        }
    }
}
