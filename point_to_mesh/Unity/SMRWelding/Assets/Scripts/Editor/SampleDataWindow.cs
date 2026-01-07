// =============================================================================
// SampleDataWindow.cs - Editor Window for Generating Test Data
// =============================================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using SMRWelding.Utilities;
using System.IO;

namespace SMRWelding.Editor
{
    /// <summary>
    /// Editor window for generating sample point cloud data
    /// </summary>
    public class SampleDataWindow : EditorWindow
    {
        private enum ShapeType
        {
            Hemisphere,
            Cylinder,
            Torus,
            WeldSeam,
            SMRVessel
        }

        // Settings
        private ShapeType shapeType = ShapeType.Hemisphere;
        private int pointCount = 10000;
        private float radius = 0.5f;
        private float height = 1.0f;
        private float noise = 0.002f;
        private bool includeNormals = true;
        private bool includeColors = true;
        private Color lowColor = new Color(0.2f, 0.4f, 0.8f);
        private Color highColor = new Color(0.8f, 0.2f, 0.2f);

        // Preview
        private Vector3[] previewPoints;
        private bool showPreview = false;

        [MenuItem("SMR Welding/Generate Sample Data")]
        public static void ShowWindow()
        {
            var window = GetWindow<SampleDataWindow>("Sample Data Generator");
            window.minSize = new Vector2(350, 450);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Sample Point Cloud Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Shape selection
            EditorGUILayout.LabelField("Shape Settings", EditorStyles.boldLabel);
            shapeType = (ShapeType)EditorGUILayout.EnumPopup("Shape Type", shapeType);
            
            EditorGUILayout.Space(5);
            pointCount = EditorGUILayout.IntSlider("Point Count", pointCount, 1000, 100000);

            // Shape-specific parameters
            EditorGUILayout.Space(10);
            switch (shapeType)
            {
                case ShapeType.Hemisphere:
                    radius = EditorGUILayout.Slider("Radius", radius, 0.1f, 2.0f);
                    break;

                case ShapeType.Cylinder:
                    radius = EditorGUILayout.Slider("Radius", radius, 0.1f, 1.0f);
                    height = EditorGUILayout.Slider("Height", height, 0.2f, 3.0f);
                    break;

                case ShapeType.Torus:
                    radius = EditorGUILayout.Slider("Major Radius", radius, 0.2f, 1.0f);
                    float minorRadius = EditorGUILayout.Slider("Minor Radius", radius * 0.25f, 0.05f, 0.3f);
                    break;

                case ShapeType.WeldSeam:
                    height = EditorGUILayout.Slider("Length", height, 0.2f, 2.0f);
                    break;

                case ShapeType.SMRVessel:
                    radius = EditorGUILayout.Slider("Outer Radius", radius, 0.3f, 1.0f);
                    height = EditorGUILayout.Slider("Height", height, 0.5f, 2.0f);
                    break;
            }

            noise = EditorGUILayout.Slider("Noise", noise, 0f, 0.01f);

            // Output options
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Output Options", EditorStyles.boldLabel);
            includeNormals = EditorGUILayout.Toggle("Include Normals", includeNormals);
            includeColors = EditorGUILayout.Toggle("Include Colors", includeColors);

            if (includeColors)
            {
                EditorGUI.indentLevel++;
                lowColor = EditorGUILayout.ColorField("Low Color", lowColor);
                highColor = EditorGUILayout.ColorField("High Color", highColor);
                EditorGUI.indentLevel--;
            }

            // Preview
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Preview", GUILayout.Height(25)))
            {
                GeneratePreview();
            }
            if (GUILayout.Button("Clear Preview", GUILayout.Height(25)))
            {
                ClearPreview();
            }
            EditorGUILayout.EndHorizontal();

            if (showPreview && previewPoints != null)
            {
                EditorGUILayout.HelpBox($"Preview: {previewPoints.Length} points generated", MessageType.Info);
            }

            // Save
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save as PLY", GUILayout.Height(30)))
            {
                SaveAsPLY();
            }
            if (GUILayout.Button("Load into Scene", GUILayout.Height(30)))
            {
                LoadIntoScene();
            }
            EditorGUILayout.EndHorizontal();

            // Info
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Generated data can be used to test the welding pipeline without requiring actual scan data.\n\n" +
                "Shapes:\n" +
                "• Hemisphere: Dome-like surface\n" +
                "• Cylinder: Pipe section\n" +
                "• Torus: Pipe elbow\n" +
                "• Weld Seam: Linear weld path\n" +
                "• SMR Vessel: Reactor vessel segment",
                MessageType.None
            );
        }

        private void GeneratePreview()
        {
            previewPoints = GeneratePoints();
            showPreview = true;
            SceneView.RepaintAll();
        }

        private void ClearPreview()
        {
            previewPoints = null;
            showPreview = false;
            SceneView.RepaintAll();
        }

        private Vector3[] GeneratePoints()
        {
            switch (shapeType)
            {
                case ShapeType.Hemisphere:
                    return SampleDataGenerator.GenerateHemisphere(pointCount, radius, noise);
                case ShapeType.Cylinder:
                    return SampleDataGenerator.GenerateCylinder(pointCount, radius, height, noise);
                case ShapeType.Torus:
                    return SampleDataGenerator.GenerateTorus(pointCount, radius, radius * 0.25f, noise);
                case ShapeType.WeldSeam:
                    return SampleDataGenerator.GenerateWeldSeam(pointCount, height, 0.05f, noise);
                case ShapeType.SMRVessel:
                    return SampleDataGenerator.GenerateSMRVesselSegment(pointCount, radius, 0.05f, height);
                default:
                    return SampleDataGenerator.GenerateHemisphere(pointCount, radius, noise);
            }
        }

        private void SaveAsPLY()
        {
            string defaultName = $"sample_{shapeType.ToString().ToLower()}_{pointCount}.ply";
            string path = EditorUtility.SaveFilePanel("Save Point Cloud", Application.dataPath, defaultName, "ply");

            if (!string.IsNullOrEmpty(path))
            {
                var points = GeneratePoints();
                Vector3[] normals = null;
                Color[] colors = null;

                if (includeNormals)
                {
                    normals = SampleDataGenerator.EstimateNormals(points, Vector3.zero);
                }

                if (includeColors)
                {
                    colors = SampleDataGenerator.GenerateHeightColors(points, lowColor, highColor);
                }

                SampleDataGenerator.SaveToPLY(path, points, normals, colors);
                EditorUtility.DisplayDialog("Success", $"Saved {points.Length} points to:\n{path}", "OK");
                AssetDatabase.Refresh();
            }
        }

        private void LoadIntoScene()
        {
            var points = GeneratePoints();
            
            // Find or create visualizer
            var visualizer = FindObjectOfType<Components.PointCloudVisualizer>();
            if (visualizer == null)
            {
                var go = new GameObject("Sample Point Cloud");
                visualizer = go.AddComponent<Components.PointCloudVisualizer>();
                Undo.RegisterCreatedObjectUndo(go, "Create Point Cloud Visualizer");
            }

            Vector3[] normals = null;
            Color[] colors = null;

            if (includeNormals)
            {
                normals = SampleDataGenerator.EstimateNormals(points, Vector3.zero);
            }

            if (includeColors)
            {
                colors = SampleDataGenerator.GenerateHeightColors(points, lowColor, highColor);
            }

            visualizer.SetPoints(points, normals, colors);
            Selection.activeGameObject = visualizer.gameObject;
            
            Debug.Log($"Loaded {points.Length} points into scene");
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!showPreview || previewPoints == null) return;

            Handles.color = new Color(0.2f, 0.6f, 1f, 0.5f);
            
            // Draw subset for performance
            int stride = Mathf.Max(1, previewPoints.Length / 5000);
            for (int i = 0; i < previewPoints.Length; i += stride)
            {
                Handles.DrawSolidDisc(previewPoints[i], Camera.current.transform.forward, 0.003f);
            }
        }
    }
}
#endif
