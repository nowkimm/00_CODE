// =============================================================================
// WeldingSceneSetup.cs - Unity Editor Scene Setup Wizard
// =============================================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using SMRWelding.Components;
using SMRWelding.UI;

namespace SMRWelding.Editor
{
    /// <summary>
    /// Editor wizard for setting up welding scene
    /// </summary>
    public class WeldingSceneSetup : EditorWindow
    {
        [MenuItem("SMR Welding/Setup Scene")]
        public static void ShowWindow()
        {
            GetWindow<WeldingSceneSetup>("SMR Welding Setup");
        }

        [MenuItem("SMR Welding/Create Pipeline Controller")]
        public static void CreatePipelineController()
        {
            var go = new GameObject("WeldingPipelineController");
            go.AddComponent<WeldingPipelineController>();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Pipeline Controller");
        }

        [MenuItem("SMR Welding/Create Path Visualizer")]
        public static void CreatePathVisualizer()
        {
            var go = new GameObject("PathVisualizer");
            go.AddComponent<PathVisualizer>();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Path Visualizer");
        }

        [MenuItem("SMR Welding/Create Robot Visualizer")]
        public static void CreateRobotVisualizer()
        {
            var go = new GameObject("RobotVisualizer");
            go.AddComponent<RobotVisualizer>();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Robot Visualizer");
        }

        [MenuItem("SMR Welding/Create Point Cloud Visualizer")]
        public static void CreatePointCloudVisualizer()
        {
            var go = new GameObject("PointCloudVisualizer");
            go.AddComponent<PointCloudVisualizer>();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Point Cloud Visualizer");
        }

        private bool _createPipeline = true;
        private bool _createVisualizers = true;
        private bool _createUI = true;
        private bool _createLights = true;
        private bool _createCamera = true;

        private void OnGUI()
        {
            GUILayout.Label("SMR Welding Scene Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Components to Create:", EditorStyles.boldLabel);
            _createPipeline = EditorGUILayout.Toggle("Pipeline Controller", _createPipeline);
            _createVisualizers = EditorGUILayout.Toggle("Visualizers", _createVisualizers);
            _createUI = EditorGUILayout.Toggle("UI Canvas", _createUI);
            _createLights = EditorGUILayout.Toggle("Lights", _createLights);
            _createCamera = EditorGUILayout.Toggle("Camera", _createCamera);

            EditorGUILayout.Space();

            if (GUILayout.Button("Setup Scene", GUILayout.Height(40)))
            {
                SetupScene();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This will create the basic scene structure for the SMR Welding system.",
                MessageType.Info);
        }

        private void SetupScene()
        {
            // Create root object
            var root = new GameObject("SMR Welding System");
            Undo.RegisterCreatedObjectUndo(root, "Setup SMR Welding Scene");

            // Pipeline Controller
            if (_createPipeline)
            {
                var pipelineGO = new GameObject("Pipeline Controller");
                pipelineGO.transform.SetParent(root.transform);
                var pipeline = pipelineGO.AddComponent<WeldingPipelineController>();

                // Create mesh output
                var meshGO = new GameObject("Generated Mesh");
                meshGO.transform.SetParent(root.transform);
                var meshFilter = meshGO.AddComponent<MeshFilter>();
                var meshRenderer = meshGO.AddComponent<MeshRenderer>();
                meshRenderer.material = new Material(Shader.Find("Standard"));

                // Assign to pipeline via SerializedObject
                var so = new SerializedObject(pipeline);
                so.FindProperty("meshOutput").objectReferenceValue = meshFilter;
                so.FindProperty("meshRenderer").objectReferenceValue = meshRenderer;
                so.ApplyModifiedProperties();
            }

            // Visualizers
            if (_createVisualizers)
            {
                var visualizersRoot = new GameObject("Visualizers");
                visualizersRoot.transform.SetParent(root.transform);

                // Path Visualizer
                var pathGO = new GameObject("Path Visualizer");
                pathGO.transform.SetParent(visualizersRoot.transform);
                pathGO.AddComponent<PathVisualizer>();

                // Robot Visualizer
                var robotGO = new GameObject("Robot Visualizer");
                robotGO.transform.SetParent(visualizersRoot.transform);
                robotGO.AddComponent<RobotVisualizer>();

                // Point Cloud Visualizer
                var pcGO = new GameObject("Point Cloud Visualizer");
                pcGO.transform.SetParent(visualizersRoot.transform);
                pcGO.AddComponent<PointCloudVisualizer>();
            }

            // UI
            if (_createUI)
            {
                var canvasGO = new GameObject("UI Canvas");
                canvasGO.transform.SetParent(root.transform);
                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

                var uiGO = new GameObject("Welding UI");
                uiGO.transform.SetParent(canvasGO.transform);
                uiGO.AddComponent<WeldingUI>();
            }

            // Lights
            if (_createLights)
            {
                var lightGO = new GameObject("Directional Light");
                lightGO.transform.SetParent(root.transform);
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.0f;
                lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
            }

            // Camera
            if (_createCamera)
            {
                var camGO = new GameObject("Main Camera");
                camGO.transform.SetParent(root.transform);
                camGO.tag = "MainCamera";
                var cam = camGO.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Skybox;
                camGO.transform.position = new Vector3(0, 1, -2);
                camGO.transform.LookAt(Vector3.zero);
            }

            Selection.activeGameObject = root;
            Debug.Log("SMR Welding scene setup complete!");
        }
    }

    /// <summary>
    /// Custom inspector for WeldingPipelineController
    /// </summary>
    [CustomEditor(typeof(WeldingPipelineController))]
    public class WeldingPipelineControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Editor Controls", EditorStyles.boldLabel);

            var controller = (WeldingPipelineController)target;

            EditorGUI.BeginDisabledGroup(controller.IsRunning);
            
            if (GUILayout.Button("Test with Sample Data"))
            {
                controller.SendMessage("TestWithSampleData", SendMessageOptions.DontRequireReceiver);
            }

            EditorGUI.EndDisabledGroup();

            if (controller.IsRunning)
            {
                EditorGUILayout.HelpBox("Pipeline is running...", MessageType.Info);
            }

            // Show current state
            EditorGUILayout.LabelField("Current State:", controller.CurrentState.ToString());
        }
    }
}
#endif
