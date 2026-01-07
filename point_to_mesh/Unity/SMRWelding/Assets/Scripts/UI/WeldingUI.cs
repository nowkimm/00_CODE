// =============================================================================
// WeldingUI.cs - Main UI Controller for Welding System
// =============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SMRWelding.UI
{
    using Components;
    using Native;

    /// <summary>
    /// Main UI controller for the welding pipeline
    /// </summary>
    public class WeldingUI : MonoBehaviour
    {
        [Header("Pipeline Controller")]
        [SerializeField] private WeldingPipelineController pipelineController;

        [Header("File Selection")]
        [SerializeField] private TMP_InputField filePathInput;
        [SerializeField] private Button browseButton;
        [SerializeField] private Button loadButton;

        [Header("Pipeline Controls")]
        [SerializeField] private Button runPipelineButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Settings Panel")]
        [SerializeField] private TMP_Dropdown robotTypeDropdown;
        [SerializeField] private Slider voxelSizeSlider;
        [SerializeField] private TextMeshProUGUI voxelSizeText;
        [SerializeField] private Slider poissonDepthSlider;
        [SerializeField] private TextMeshProUGUI poissonDepthText;
        [SerializeField] private TMP_Dropdown weavePatternDropdown;

        [Header("Visualization Controls")]
        [SerializeField] private Toggle showMeshToggle;
        [SerializeField] private Toggle showPathToggle;
        [SerializeField] private Toggle showRobotToggle;
        [SerializeField] private Button playTrajectoryButton;
        [SerializeField] private Button stopTrajectoryButton;

        [Header("Info Panel")]
        [SerializeField] private TextMeshProUGUI meshInfoText;
        [SerializeField] private TextMeshProUGUI pathInfoText;
        [SerializeField] private TextMeshProUGUI trajectoryInfoText;

        [Header("Visualization References")]
        [SerializeField] private MeshRenderer meshVisualization;
        [SerializeField] private PathVisualizer pathVisualizer;
        [SerializeField] private RobotVisualizer robotVisualizer;

        private void Awake()
        {
            SetupUI();
            SetupEventHandlers();
        }

        private void SetupUI()
        {
            // Setup robot type dropdown
            if (robotTypeDropdown != null)
            {
                robotTypeDropdown.ClearOptions();
                robotTypeDropdown.AddOptions(new System.Collections.Generic.List<string>
                {
                    "UR5", "UR10", "KUKA KR6 R700", "Doosan M1013", "Custom"
                });
            }

            // Setup weave pattern dropdown
            if (weavePatternDropdown != null)
            {
                weavePatternDropdown.ClearOptions();
                weavePatternDropdown.AddOptions(new System.Collections.Generic.List<string>
                {
                    "None", "Zigzag", "Circular", "Triangle", "Figure 8"
                });
            }

            // Initialize sliders
            UpdateSliderTexts();
            UpdateStatus("Ready");
        }

        private void SetupEventHandlers()
        {
            // File buttons
            browseButton?.onClick.AddListener(OnBrowseClicked);
            loadButton?.onClick.AddListener(OnLoadClicked);

            // Pipeline buttons
            runPipelineButton?.onClick.AddListener(OnRunPipelineClicked);
            resetButton?.onClick.AddListener(OnResetClicked);

            // Visualization toggles
            showMeshToggle?.onValueChanged.AddListener(OnShowMeshChanged);
            showPathToggle?.onValueChanged.AddListener(OnShowPathChanged);
            showRobotToggle?.onValueChanged.AddListener(OnShowRobotChanged);

            // Trajectory buttons
            playTrajectoryButton?.onClick.AddListener(OnPlayTrajectoryClicked);
            stopTrajectoryButton?.onClick.AddListener(OnStopTrajectoryClicked);

            // Sliders
            voxelSizeSlider?.onValueChanged.AddListener(_ => UpdateSliderTexts());
            poissonDepthSlider?.onValueChanged.AddListener(_ => UpdateSliderTexts());

            // Pipeline events
            if (pipelineController != null)
            {
                pipelineController.OnStatusChanged.AddListener(UpdateStatus);
                pipelineController.OnProgressChanged.AddListener(UpdateProgress);
                pipelineController.OnPipelineComplete.AddListener(OnPipelineComplete);
                pipelineController.OnError.AddListener(OnPipelineError);
            }
        }

        private void OnBrowseClicked()
        {
            // In standalone build, use file browser
            // For now, just show a message
            Debug.Log("Browse for point cloud file");
            
            #if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel(
                "Select Point Cloud", "", "ply,pcd");
            if (!string.IsNullOrEmpty(path) && filePathInput != null)
            {
                filePathInput.text = path;
            }
            #endif
        }

        private void OnLoadClicked()
        {
            if (filePathInput == null || string.IsNullOrEmpty(filePathInput.text))
            {
                UpdateStatus("Please select a file");
                return;
            }

            string path = filePathInput.text;
            if (!System.IO.File.Exists(path))
            {
                UpdateStatus("File not found");
                return;
            }

            pipelineController?.RunFromFile(path);
        }

        private void OnRunPipelineClicked()
        {
            if (pipelineController == null) return;

            if (!string.IsNullOrEmpty(filePathInput?.text))
            {
                pipelineController.RunFromFile(filePathInput.text);
            }
            else
            {
                UpdateStatus("No file selected");
            }
        }

        private void OnResetClicked()
        {
            pipelineController?.Reset();
            UpdateInfoPanels();
        }

        private void OnShowMeshChanged(bool show)
        {
            if (meshVisualization != null)
                meshVisualization.enabled = show;
        }

        private void OnShowPathChanged(bool show)
        {
            if (pathVisualizer != null)
                pathVisualizer.gameObject.SetActive(show);
        }

        private void OnShowRobotChanged(bool show)
        {
            if (robotVisualizer != null)
                robotVisualizer.gameObject.SetActive(show);
        }

        private void OnPlayTrajectoryClicked()
        {
            if (robotVisualizer == null || pipelineController == null) return;

            var trajectory = pipelineController.JointTrajectory;
            if (trajectory != null && trajectory.Length > 0)
            {
                robotVisualizer.PlayTrajectory(trajectory, () =>
                {
                    UpdateStatus("Trajectory playback complete");
                });
                UpdateStatus("Playing trajectory...");
            }
            else
            {
                UpdateStatus("No trajectory available");
            }
        }

        private void OnStopTrajectoryClicked()
        {
            robotVisualizer?.StopAnimation();
            UpdateStatus("Trajectory stopped");
        }

        private void UpdateSliderTexts()
        {
            if (voxelSizeSlider != null && voxelSizeText != null)
            {
                voxelSizeText.text = $"Voxel: {voxelSizeSlider.value:F3}m";
            }

            if (poissonDepthSlider != null && poissonDepthText != null)
            {
                poissonDepthText.text = $"Depth: {(int)poissonDepthSlider.value}";
            }
        }

        private void UpdateStatus(string status)
        {
            if (statusText != null)
                statusText.text = status;
        }

        private void UpdateProgress(float progress)
        {
            if (progressSlider != null)
                progressSlider.value = progress;
        }

        private void OnPipelineComplete()
        {
            UpdateStatus("Pipeline complete!");
            UpdateInfoPanels();
            
            // Enable visualization
            if (showMeshToggle != null) showMeshToggle.isOn = true;
            if (showPathToggle != null) showPathToggle.isOn = true;
        }

        private void OnPipelineError(string error)
        {
            UpdateStatus($"Error: {error}");
            if (progressSlider != null)
                progressSlider.value = 0;
        }

        private void UpdateInfoPanels()
        {
            if (pipelineController == null) return;

            // Mesh info
            if (meshInfoText != null)
            {
                var mesh = pipelineController.GeneratedMesh;
                if (mesh != null)
                {
                    meshInfoText.text = $"Vertices: {mesh.vertexCount:N0}\n" +
                                       $"Triangles: {mesh.triangles.Length / 3:N0}";
                }
                else
                {
                    meshInfoText.text = "No mesh";
                }
            }

            // Path info
            if (pathInfoText != null)
            {
                var path = pipelineController.PathPositions;
                if (path != null && path.Length > 0)
                {
                    float length = pathVisualizer?.GetTotalLength() ?? 0;
                    pathInfoText.text = $"Points: {path.Length:N0}\n" +
                                       $"Length: {length:F3}m";
                }
                else
                {
                    pathInfoText.text = "No path";
                }
            }

            // Trajectory info
            if (trajectoryInfoText != null)
            {
                var trajectory = pipelineController.JointTrajectory;
                if (trajectory != null && trajectory.Length > 0)
                {
                    var reachability = pipelineController.GetType()
                        .GetProperty("Reachability")?.GetValue(pipelineController) as bool[];
                    
                    int reachable = 0;
                    if (reachability != null)
                    {
                        foreach (bool r in reachability)
                            if (r) reachable++;
                    }

                    trajectoryInfoText.text = $"Points: {trajectory.Length:N0}\n" +
                        $"Reachable: {reachable}/{trajectory.Length}";
                }
                else
                {
                    trajectoryInfoText.text = "No trajectory";
                }
            }
        }

        /// <summary>
        /// Get current settings from UI
        /// </summary>
        public WeldingPipeline.Config GetConfigFromUI()
        {
            return new WeldingPipeline.Config
            {
                VoxelSize = voxelSizeSlider?.value ?? 0.002f,
                PoissonDepth = (int)(poissonDepthSlider?.value ?? 8),
                RobotType = (RobotType)(robotTypeDropdown?.value ?? 0),
                WeavePattern = (WeaveType)(weavePatternDropdown?.value ?? 0)
            };
        }
    }
}
