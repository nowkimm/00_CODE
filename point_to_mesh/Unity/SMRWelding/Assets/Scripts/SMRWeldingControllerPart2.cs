        /// <summary>
        /// Generate weld path from mesh edge
        /// </summary>
        public void GenerateWeldPath()
        {
            if (_isProcessing || _nativeMesh == null) return;
            StartCoroutine(GenerateWeldPathAsync());
        }

        private IEnumerator GenerateWeldPathAsync()
        {
            _isProcessing = true;
            UpdateStatus("Generating weld path...");
            UpdateProgress(0);
            yield return null;

            try
            {
                var pathParams = new PathParams
                {
                    step_size = pathStepSize,
                    standoff_distance = standoffDistance,
                    weave_type = WeaveType.None,
                    weave_amplitude = weaveAmplitude,
                    weave_frequency = weaveFrequency
                };

                _weldPath?.Dispose();
                _weldPath = PathWrapper.CreateFromMeshEdge(_nativeMesh, pathParams);

                UpdateStatus($"Path created: {_weldPath.Count} points");
                UpdateProgress(30);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Path generation failed: {ex.Message}");
                _isProcessing = false;
                yield break;
            }

            yield return null;

            // Apply weave pattern
            if (weaveType != WeaveType.None)
            {
                UpdateStatus("Applying weave pattern...");
                UpdateProgress(40);
                yield return null;

                try
                {
                    _weldPath.ApplyWeave(weaveType, weaveAmplitude, weaveFrequency);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Weave application warning: {ex.Message}");
                }
            }

            // Resample path
            UpdateStatus("Resampling path...");
            UpdateProgress(50);
            yield return null;

            try
            {
                _weldPath.Resample(pathStepSize);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Resample warning: {ex.Message}");
            }

            // Smooth path
            UpdateStatus("Smoothing path...");
            UpdateProgress(60);
            yield return null;

            try
            {
                _weldPath.Smooth(5);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Smooth warning: {ex.Message}");
            }

            // Convert to joint trajectory
            if (_robot != null)
            {
                UpdateStatus("Computing joint trajectory...");
                UpdateProgress(70);
                yield return null;

                try
                {
                    var (joints, reachable) = _weldPath.ToJointTrajectory(_robot, standoffDistance);
                    _jointTrajectory = joints;
                    _pathReachability = reachable;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Joint trajectory warning: {ex.Message}");
                    _pathReachability = new bool[_weldPath.Count];
                    for (int i = 0; i < _pathReachability.Length; i++)
                        _pathReachability[i] = true;
                }
            }

            // Get path positions for visualization
            UpdateStatus("Preparing visualization...");
            UpdateProgress(90);
            yield return null;

            try
            {
                _pathPositions = _weldPath.GetPositions();
                VisualizePath();
                OnPathGenerated?.Invoke(_pathPositions, _pathReachability);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Path visualization failed: {ex.Message}");
            }

            UpdateStatus($"Path complete: {_weldPath.Count} points, {_weldPath.GetTotalLength():F3}m length");
            UpdateProgress(100);
            _isProcessing = false;
        }

        /// <summary>
        /// Generate path from explicit points
        /// </summary>
        public void GeneratePathFromPoints(Vector3[] points, Vector3[] normals)
        {
            if (_isProcessing) return;
            StartCoroutine(GeneratePathFromPointsAsync(points, normals));
        }

        private IEnumerator GeneratePathFromPointsAsync(Vector3[] points, Vector3[] normals)
        {
            _isProcessing = true;
            UpdateStatus("Creating path from points...");
            yield return null;

            try
            {
                var pathParams = new PathParams
                {
                    step_size = pathStepSize,
                    standoff_distance = standoffDistance,
                    weave_type = weaveType,
                    weave_amplitude = weaveAmplitude,
                    weave_frequency = weaveFrequency
                };

                _weldPath?.Dispose();
                _weldPath = PathWrapper.CreateFromPoints(points, normals, pathParams);

                _pathPositions = _weldPath.GetPositions();
                
                if (_robot != null)
                {
                    var (joints, reachable) = _weldPath.ToJointTrajectory(_robot, standoffDistance);
                    _jointTrajectory = joints;
                    _pathReachability = reachable;
                }

                VisualizePath();
                OnPathGenerated?.Invoke(_pathPositions, _pathReachability);
                UpdateStatus($"Path created: {_weldPath.Count} points");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Path creation failed: {ex.Message}");
            }

            _isProcessing = false;
        }

        /// <summary>
        /// Visualize path using LineRenderer
        /// </summary>
        private void VisualizePath()
        {
            if (pathRenderer == null || _pathPositions == null) return;

            pathRenderer.positionCount = _pathPositions.Length;
            pathRenderer.SetPositions(_pathPositions);

            // Color by reachability
            if (_pathReachability != null && _pathReachability.Length == _pathPositions.Length)
            {
                Gradient gradient = new Gradient();
                List<GradientColorKey> colorKeys = new List<GradientColorKey>();
                List<GradientAlphaKey> alphaKeys = new List<GradientAlphaKey>();

                for (int i = 0; i < _pathReachability.Length; i++)
                {
                    float t = (float)i / (_pathReachability.Length - 1);
                    colorKeys.Add(new GradientColorKey(
                        _pathReachability[i] ? reachableColor : unreachableColor, t));
                    alphaKeys.Add(new GradientAlphaKey(1f, t));
                }

                gradient.SetKeys(colorKeys.ToArray(), alphaKeys.ToArray());
                pathRenderer.colorGradient = gradient;
            }
        }

        /// <summary>
        /// Save generated mesh to file
        /// </summary>
        public void SaveMesh(string path)
        {
            if (_nativeMesh == null) return;

            try
            {
                if (path.EndsWith(".ply", StringComparison.OrdinalIgnoreCase))
                    _nativeMesh.SavePLY(path);
                else if (path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                    _nativeMesh.SaveOBJ(path);

                UpdateStatus($"Mesh saved: {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save mesh: {ex.Message}");
            }
        }

        /// <summary>
        /// Get robot end-effector pose at trajectory index
        /// </summary>
        public (Vector3 position, Quaternion rotation) GetRobotPoseAt(int index)
        {
            if (_robot == null || _jointTrajectory == null || index < 0 || index >= _jointTrajectory.Length)
                return (Vector3.zero, Quaternion.identity);

            return _robot.GetEndEffectorPose(_jointTrajectory[index]);
        }

        private void UpdateStatus(string status)
        {
            Debug.Log($"[SMRWelding] {status}");
            OnStatusChanged?.Invoke(status);
        }

        private void UpdateProgress(float progress)
        {
            OnProgressChanged?.Invoke(progress);
        }

        private void CleanupResources()
        {
            _weldPath?.Dispose();
            _nativeMesh?.Dispose();
            _pointCloud?.Dispose();
            _robot?.Dispose();

            _weldPath = null;
            _nativeMesh = null;
            _pointCloud = null;
            _robot = null;
        }

        /// <summary>
        /// Full pipeline: Load → Generate Mesh → Generate Path
        /// </summary>
        public void RunFullPipeline(string pointCloudPath)
        {
            StartCoroutine(RunFullPipelineAsync(pointCloudPath));
        }

        private IEnumerator RunFullPipelineAsync(string path)
        {
            yield return LoadPointCloudAsync(path);
            if (_pointCloud == null || _pointCloud.Count == 0) yield break;

            yield return GenerateMeshAsync();
            if (_nativeMesh == null) yield break;

            yield return GenerateWeldPathAsync();
        }
    }
}
