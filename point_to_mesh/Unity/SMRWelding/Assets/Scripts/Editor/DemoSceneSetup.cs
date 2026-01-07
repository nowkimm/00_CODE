// =============================================================================
// DemoSceneSetup.cs - Create Demo Scene with All Components
// =============================================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using SMRWelding.Components;

namespace SMRWelding.Editor
{
    /// <summary>
    /// Editor utilities for creating demo scenes
    /// </summary>
    public static class DemoSceneSetup
    {
        [MenuItem("SMR Welding/Create Demo Scene", false, 100)]
        public static void CreateDemoScene()
        {
            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Root object
            var root = new GameObject("SMR Welding Demo");
            
            // Add QuickStartDemo
            var demo = root.AddComponent<QuickStartDemo>();

            // Create mesh output
            var meshGO = new GameObject("Generated Mesh");
            meshGO.transform.SetParent(root.transform);
            var meshFilter = meshGO.AddComponent<MeshFilter>();
            var meshRenderer = meshGO.AddComponent<MeshRenderer>();
            meshRenderer.material = CreateDefaultMaterial();

            // Create visualizers
            var visualizers = new GameObject("Visualizers");
            visualizers.transform.SetParent(root.transform);

            var pathVis = new GameObject("Path Visualizer");
            pathVis.transform.SetParent(visualizers.transform);
            pathVis.AddComponent<PathVisualizer>();

            var pointVis = new GameObject("Point Cloud Visualizer");
            pointVis.transform.SetParent(visualizers.transform);
            pointVis.AddComponent<PointCloudVisualizer>();

            var robotVis = new GameObject("Robot Visualizer");
            robotVis.transform.SetParent(visualizers.transform);
            robotVis.AddComponent<RobotVisualizer>();

            // Setup camera
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = new Vector3(0, 1.5f, -2f);
                cam.transform.rotation = Quaternion.Euler(30, 0, 0);
                cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f);
            }

            // Setup lighting
            var lights = Object.FindObjectsOfType<Light>();
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    light.transform.rotation = Quaternion.Euler(50, -30, 0);
                    light.intensity = 1.2f;
                }
            }

            // Add ambient light
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.35f);

            // Select root
            Selection.activeGameObject = root;

            // Save scene
            string scenePath = "Assets/Scenes/SMRWeldingDemo.unity";
            
            // Create Scenes folder if needed
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"Created demo scene at: {scenePath}");

            EditorUtility.DisplayDialog("Demo Scene Created",
                "SMR Welding demo scene has been created.\n\n" +
                "Press Play to run the demo automatically, or use the context menu on QuickStartDemo component.",
                "OK");
        }

        [MenuItem("SMR Welding/Create Quick Test Object", false, 101)]
        public static void CreateQuickTestObject()
        {
            var go = new GameObject("SMR Quick Test");
            go.AddComponent<QuickStartDemo>();
            
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Quick Test");
            
            Debug.Log("Created QuickStartDemo object. Press Play and it will run automatically.");
        }

        private static Material CreateDefaultMaterial()
        {
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(0.6f, 0.65f, 0.7f);
            material.SetFloat("_Metallic", 0.3f);
            material.SetFloat("_Glossiness", 0.5f);
            return material;
        }

        [MenuItem("SMR Welding/Documentation/Open README", false, 200)]
        public static void OpenReadme()
        {
            string readmePath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../../README.md"));
            
            if (System.IO.File.Exists(readmePath))
            {
                System.Diagnostics.Process.Start(readmePath);
            }
            else
            {
                Debug.LogWarning($"README not found at: {readmePath}");
            }
        }

        [MenuItem("SMR Welding/Documentation/Open Project Plan", false, 201)]
        public static void OpenProjectPlan()
        {
            string planPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../../project_plan.md"));
            
            if (System.IO.File.Exists(planPath))
            {
                System.Diagnostics.Process.Start(planPath);
            }
            else
            {
                Debug.LogWarning($"Project plan not found at: {planPath}");
            }
        }

        [MenuItem("SMR Welding/Check Native DLL", false, 300)]
        public static void CheckNativeDLL()
        {
            string dllPath = System.IO.Path.Combine(
                Application.dataPath, "Plugins/x86_64/smr_welding_native.dll");
            
            bool exists = System.IO.File.Exists(dllPath);
            
            string message = exists 
                ? $"Native DLL found at:\n{dllPath}\n\nYou can use the full pipeline with native processing."
                : $"Native DLL not found at:\n{dllPath}\n\n" +
                  "The system will use Simulation Mode.\n\n" +
                  "To build the native DLL:\n" +
                  "1. Install Visual Studio 2022 with C++ support\n" +
                  "2. Install CMake\n" +
                  "3. Install vcpkg and run:\n" +
                  "   vcpkg install open3d:x64-windows eigen3:x64-windows\n" +
                  "4. Run build_native.ps1 or build_native.bat";

            EditorUtility.DisplayDialog(
                exists ? "Native DLL Status: OK" : "Native DLL Status: Not Found",
                message,
                "OK");
        }
    }
}
#endif
