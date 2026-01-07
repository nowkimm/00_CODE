// =============================================================================
// ConfigManager.cs - Save/Load Pipeline Configuration
// =============================================================================
using System;
using System.IO;
using UnityEngine;

namespace SMRWelding.Utilities
{
    /// <summary>
    /// Manages saving and loading pipeline configurations
    /// </summary>
    public static class ConfigManager
    {
        private const string CONFIG_FOLDER = "SMRWelding/Configs";
        private const string DEFAULT_CONFIG = "default_config.json";

        /// <summary>
        /// Get the config directory path
        /// </summary>
        public static string ConfigDirectory
        {
            get
            {
                string path = Path.Combine(Application.persistentDataPath, CONFIG_FOLDER);
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            }
        }

        /// <summary>
        /// Save configuration to JSON file
        /// </summary>
        public static void SaveConfig(PipelineConfig config, string filename = null)
        {
            if (config == null)
            {
                Debug.LogError("ConfigManager: Cannot save null config");
                return;
            }

            filename = filename ?? DEFAULT_CONFIG;
            string path = Path.Combine(ConfigDirectory, filename);

            try
            {
                var wrapper = new ConfigWrapper(config);
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(path, json);
                Debug.Log($"ConfigManager: Saved config to {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"ConfigManager: Failed to save config - {e.Message}");
            }
        }

        /// <summary>
        /// Load configuration from JSON file
        /// </summary>
        public static PipelineConfig LoadConfig(string filename = null)
        {
            filename = filename ?? DEFAULT_CONFIG;
            string path = Path.Combine(ConfigDirectory, filename);

            if (!File.Exists(path))
            {
                Debug.LogWarning($"ConfigManager: Config file not found at {path}, using defaults");
                return PipelineConfig.Default;
            }

            try
            {
                string json = File.ReadAllText(path);
                var wrapper = JsonUtility.FromJson<ConfigWrapper>(json);
                Debug.Log($"ConfigManager: Loaded config from {path}");
                return wrapper.ToConfig();
            }
            catch (Exception e)
            {
                Debug.LogError($"ConfigManager: Failed to load config - {e.Message}");
                return PipelineConfig.Default;
            }
        }

        /// <summary>
        /// List all saved config files
        /// </summary>
        public static string[] ListConfigs()
        {
            if (!Directory.Exists(ConfigDirectory))
                return new string[0];

            var files = Directory.GetFiles(ConfigDirectory, "*.json");
            var names = new string[files.Length];
            
            for (int i = 0; i < files.Length; i++)
            {
                names[i] = Path.GetFileName(files[i]);
            }

            return names;
        }

        /// <summary>
        /// Delete a config file
        /// </summary>
        public static bool DeleteConfig(string filename)
        {
            string path = Path.Combine(ConfigDirectory, filename);
            
            if (File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                    Debug.Log($"ConfigManager: Deleted config {filename}");
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"ConfigManager: Failed to delete config - {e.Message}");
                }
            }
            
            return false;
        }

        /// <summary>
        /// Export config to specified path
        /// </summary>
        public static void ExportConfig(PipelineConfig config, string fullPath)
        {
            try
            {
                var wrapper = new ConfigWrapper(config);
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(fullPath, json);
                Debug.Log($"ConfigManager: Exported config to {fullPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"ConfigManager: Failed to export config - {e.Message}");
            }
        }

        /// <summary>
        /// Import config from specified path
        /// </summary>
        public static PipelineConfig ImportConfig(string fullPath)
        {
            if (!File.Exists(fullPath))
            {
                Debug.LogError($"ConfigManager: File not found - {fullPath}");
                return PipelineConfig.Default;
            }

            try
            {
                string json = File.ReadAllText(fullPath);
                var wrapper = JsonUtility.FromJson<ConfigWrapper>(json);
                Debug.Log($"ConfigManager: Imported config from {fullPath}");
                return wrapper.ToConfig();
            }
            catch (Exception e)
            {
                Debug.LogError($"ConfigManager: Failed to import config - {e.Message}");
                return PipelineConfig.Default;
            }
        }

        /// <summary>
        /// JSON-serializable wrapper for PipelineConfig
        /// </summary>
        [Serializable]
        private class ConfigWrapper
        {
            public float voxelSize;
            public int kdTreeMaxLeaf;
            public float normalEstimationRadius;
            public float poissonDepth;
            public float poissonScale;
            public float meshSimplifyRatio;
            public int robotType;
            public float pathStepSize;
            public float approachDistance;
            public int weavingPattern;
            public float weavingAmplitude;
            public float weavingFrequency;
            public string configName;
            public string configVersion;

            public ConfigWrapper() { }

            public ConfigWrapper(PipelineConfig config)
            {
                voxelSize = config.VoxelSize;
                kdTreeMaxLeaf = config.KdTreeMaxLeaf;
                normalEstimationRadius = config.NormalEstimationRadius;
                poissonDepth = config.PoissonDepth;
                poissonScale = config.PoissonScale;
                meshSimplifyRatio = config.MeshSimplifyRatio;
                robotType = config.RobotType;
                pathStepSize = config.PathStepSize;
                approachDistance = config.ApproachDistance;
                weavingPattern = config.WeavingPattern;
                weavingAmplitude = config.WeavingAmplitude;
                weavingFrequency = config.WeavingFrequency;
                configName = "SMRWelding";
                configVersion = "1.0";
            }

            public PipelineConfig ToConfig()
            {
                return new PipelineConfig
                {
                    VoxelSize = voxelSize,
                    KdTreeMaxLeaf = kdTreeMaxLeaf,
                    NormalEstimationRadius = normalEstimationRadius,
                    PoissonDepth = poissonDepth,
                    PoissonScale = poissonScale,
                    MeshSimplifyRatio = meshSimplifyRatio,
                    RobotType = robotType,
                    PathStepSize = pathStepSize,
                    ApproachDistance = approachDistance,
                    WeavingPattern = weavingPattern,
                    WeavingAmplitude = weavingAmplitude,
                    WeavingFrequency = weavingFrequency
                };
            }
        }
    }

    /// <summary>
    /// Pipeline configuration structure
    /// </summary>
    [Serializable]
    public struct PipelineConfig
    {
        // Point cloud processing
        public float VoxelSize;
        public int KdTreeMaxLeaf;
        public float NormalEstimationRadius;

        // Mesh reconstruction
        public float PoissonDepth;
        public float PoissonScale;
        public float MeshSimplifyRatio;

        // Robot
        public int RobotType;

        // Path planning
        public float PathStepSize;
        public float ApproachDistance;

        // Weaving
        public int WeavingPattern;
        public float WeavingAmplitude;
        public float WeavingFrequency;

        /// <summary>
        /// Default configuration
        /// </summary>
        public static PipelineConfig Default => new PipelineConfig
        {
            VoxelSize = 0.005f,
            KdTreeMaxLeaf = 10,
            NormalEstimationRadius = 0.02f,
            PoissonDepth = 8,
            PoissonScale = 1.1f,
            MeshSimplifyRatio = 0.5f,
            RobotType = 0, // UR5
            PathStepSize = 0.01f,
            ApproachDistance = 0.05f,
            WeavingPattern = 0, // None
            WeavingAmplitude = 0.003f,
            WeavingFrequency = 10f
        };

        /// <summary>
        /// High quality configuration (slower)
        /// </summary>
        public static PipelineConfig HighQuality => new PipelineConfig
        {
            VoxelSize = 0.002f,
            KdTreeMaxLeaf = 10,
            NormalEstimationRadius = 0.01f,
            PoissonDepth = 10,
            PoissonScale = 1.1f,
            MeshSimplifyRatio = 0.8f,
            RobotType = 0,
            PathStepSize = 0.005f,
            ApproachDistance = 0.05f,
            WeavingPattern = 1, // Zigzag
            WeavingAmplitude = 0.002f,
            WeavingFrequency = 15f
        };

        /// <summary>
        /// Fast preview configuration
        /// </summary>
        public static PipelineConfig FastPreview => new PipelineConfig
        {
            VoxelSize = 0.01f,
            KdTreeMaxLeaf = 20,
            NormalEstimationRadius = 0.03f,
            PoissonDepth = 6,
            PoissonScale = 1.2f,
            MeshSimplifyRatio = 0.3f,
            RobotType = 0,
            PathStepSize = 0.02f,
            ApproachDistance = 0.05f,
            WeavingPattern = 0,
            WeavingAmplitude = 0.003f,
            WeavingFrequency = 10f
        };

        /// <summary>
        /// Validate configuration values
        /// </summary>
        public bool IsValid()
        {
            if (VoxelSize <= 0 || VoxelSize > 0.1f) return false;
            if (KdTreeMaxLeaf < 1 || KdTreeMaxLeaf > 100) return false;
            if (NormalEstimationRadius <= 0) return false;
            if (PoissonDepth < 1 || PoissonDepth > 12) return false;
            if (MeshSimplifyRatio <= 0 || MeshSimplifyRatio > 1) return false;
            if (PathStepSize <= 0) return false;
            return true;
        }
    }
}
