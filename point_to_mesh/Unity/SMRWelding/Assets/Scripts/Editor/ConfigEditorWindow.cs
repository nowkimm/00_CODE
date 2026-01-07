// =============================================================================
// ConfigEditorWindow.cs - Editor Window for Managing Configurations
// =============================================================================
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using SMRWelding.Utilities;

namespace SMRWelding.Editor
{
    /// <summary>
    /// Editor window for managing pipeline configurations
    /// </summary>
    public class ConfigEditorWindow : EditorWindow
    {
        private PipelineConfig currentConfig;
        private string[] savedConfigs;
        private int selectedConfigIndex = 0;
        private string newConfigName = "my_config";
        private Vector2 scrollPosition;

        [MenuItem("SMR Welding/Configuration Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<ConfigEditorWindow>("Config Manager");
            window.minSize = new Vector2(400, 500);
        }

        private void OnEnable()
        {
            currentConfig = PipelineConfig.Default;
            RefreshConfigList();
        }

        private void RefreshConfigList()
        {
            savedConfigs = ConfigManager.ListConfigs();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Pipeline Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Presets
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Default"))
                currentConfig = PipelineConfig.Default;
            if (GUILayout.Button("High Quality"))
                currentConfig = PipelineConfig.HighQuality;
            if (GUILayout.Button("Fast Preview"))
                currentConfig = PipelineConfig.FastPreview;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Point Cloud Settings
            EditorGUILayout.LabelField("Point Cloud Processing", EditorStyles.boldLabel);
            currentConfig.VoxelSize = EditorGUILayout.Slider("Voxel Size", currentConfig.VoxelSize, 0.001f, 0.05f);
            currentConfig.KdTreeMaxLeaf = EditorGUILayout.IntSlider("KD-Tree Max Leaf", currentConfig.KdTreeMaxLeaf, 1, 50);
            currentConfig.NormalEstimationRadius = EditorGUILayout.Slider("Normal Radius", currentConfig.NormalEstimationRadius, 0.005f, 0.1f);

            EditorGUILayout.Space(10);

            // Mesh Settings
            EditorGUILayout.LabelField("Mesh Reconstruction", EditorStyles.boldLabel);
            currentConfig.PoissonDepth = EditorGUILayout.Slider("Poisson Depth", currentConfig.PoissonDepth, 4, 12);
            currentConfig.PoissonScale = EditorGUILayout.Slider("Poisson Scale", currentConfig.PoissonScale, 1.0f, 1.5f);
            currentConfig.MeshSimplifyRatio = EditorGUILayout.Slider("Simplify Ratio", currentConfig.MeshSimplifyRatio, 0.1f, 1.0f);

            EditorGUILayout.Space(10);

            // Robot Settings
            EditorGUILayout.LabelField("Robot Settings", EditorStyles.boldLabel);
            string[] robotTypes = { "UR5", "UR10", "KUKA KR6-R700", "Doosan M1013" };
            currentConfig.RobotType = EditorGUILayout.Popup("Robot Type", currentConfig.RobotType, robotTypes);

            EditorGUILayout.Space(10);

            // Path Settings
            EditorGUILayout.LabelField("Path Planning", EditorStyles.boldLabel);
            currentConfig.PathStepSize = EditorGUILayout.Slider("Step Size", currentConfig.PathStepSize, 0.001f, 0.05f);
            currentConfig.ApproachDistance = EditorGUILayout.Slider("Approach Distance", currentConfig.ApproachDistance, 0.01f, 0.2f);

            EditorGUILayout.Space(10);

            // Weaving Settings
            EditorGUILayout.LabelField("Weaving Pattern", EditorStyles.boldLabel);
            string[] weavingPatterns = { "None", "Zigzag", "Circular", "Triangle", "Figure-8" };
            currentConfig.WeavingPattern = EditorGUILayout.Popup("Pattern", currentConfig.WeavingPattern, weavingPatterns);
            
            if (currentConfig.WeavingPattern > 0)
            {
                currentConfig.WeavingAmplitude = EditorGUILayout.Slider("Amplitude (m)", currentConfig.WeavingAmplitude, 0.001f, 0.01f);
                currentConfig.WeavingFrequency = EditorGUILayout.Slider("Frequency (Hz)", currentConfig.WeavingFrequency, 1f, 30f);
            }

            EditorGUILayout.Space(15);

            // Validation
            bool isValid = currentConfig.IsValid();
            if (!isValid)
            {
                EditorGUILayout.HelpBox("Configuration has invalid values!", MessageType.Warning);
            }

            // Save/Load Section
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Save / Load", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            newConfigName = EditorGUILayout.TextField("Config Name", newConfigName);
            if (GUILayout.Button("Save", GUILayout.Width(60)))
            {
                string filename = newConfigName.EndsWith(".json") ? newConfigName : newConfigName + ".json";
                ConfigManager.SaveConfig(currentConfig, filename);
                RefreshConfigList();
            }
            EditorGUILayout.EndHorizontal();

            // Saved configs list
            if (savedConfigs != null && savedConfigs.Length > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();
                selectedConfigIndex = EditorGUILayout.Popup("Saved Configs", selectedConfigIndex, savedConfigs);
                if (GUILayout.Button("Load", GUILayout.Width(60)))
                {
                    currentConfig = ConfigManager.LoadConfig(savedConfigs[selectedConfigIndex]);
                }
                if (GUILayout.Button("Delete", GUILayout.Width(60)))
                {
                    if (EditorUtility.DisplayDialog("Delete Config",
                        $"Delete '{savedConfigs[selectedConfigIndex]}'?", "Yes", "No"))
                    {
                        ConfigManager.DeleteConfig(savedConfigs[selectedConfigIndex]);
                        RefreshConfigList();
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            // Import/Export
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export to File..."))
            {
                string path = EditorUtility.SaveFilePanel("Export Config", "", "welding_config", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    ConfigManager.ExportConfig(currentConfig, path);
                }
            }
            if (GUILayout.Button("Import from File..."))
            {
                string path = EditorUtility.OpenFilePanel("Import Config", "", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    currentConfig = ConfigManager.ImportConfig(path);
                }
            }
            EditorGUILayout.EndHorizontal();

            // Apply to Scene
            EditorGUILayout.Space(15);
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
            if (GUILayout.Button("Apply to Scene Controller", GUILayout.Height(30)))
            {
                ApplyToSceneController();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Configuration Tips:\n" +
                "• Lower voxel size = more detail, slower processing\n" +
                "• Higher Poisson depth = smoother mesh, more memory\n" +
                "• Simplify ratio < 0.5 for faster rendering\n" +
                "• Use weaving for better weld penetration",
                MessageType.Info);

            EditorGUILayout.EndScrollView();
        }

        private void ApplyToSceneController()
        {
            var controller = FindObjectOfType<Components.WeldingPipelineController>();
            if (controller != null)
            {
                // Apply config via serialized fields
                var so = new SerializedObject(controller);
                
                so.FindProperty("voxelSize").floatValue = currentConfig.VoxelSize;
                so.FindProperty("normalRadius").floatValue = currentConfig.NormalEstimationRadius;
                so.FindProperty("poissonDepth").intValue = (int)currentConfig.PoissonDepth;
                so.FindProperty("simplifyRatio").floatValue = currentConfig.MeshSimplifyRatio;
                so.FindProperty("pathStepSize").floatValue = currentConfig.PathStepSize;
                so.FindProperty("approachDistance").floatValue = currentConfig.ApproachDistance;
                
                so.ApplyModifiedProperties();
                
                Debug.Log("Applied configuration to WeldingPipelineController");
                EditorUtility.DisplayDialog("Success", "Configuration applied to scene controller", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Not Found", 
                    "No WeldingPipelineController found in scene.\n\n" +
                    "Use SMR Welding > Setup Welding Scene to create one.", "OK");
            }
        }
    }
}
#endif
