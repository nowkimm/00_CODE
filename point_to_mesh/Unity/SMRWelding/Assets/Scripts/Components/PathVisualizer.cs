// =============================================================================
// PathVisualizer.cs - Weld Path Visualization Component
// =============================================================================
using UnityEngine;

namespace SMRWelding.Components
{
    /// <summary>
    /// Visualizes welding path with reachability indicators
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class PathVisualizer : MonoBehaviour
    {
        [Header("Path Settings")]
        [SerializeField] private float lineWidth = 0.002f;
        [SerializeField] private Material pathMaterial;
        
        [Header("Colors")]
        [SerializeField] private Color reachableColor = Color.green;
        [SerializeField] private Color unreachableColor = Color.red;
        [SerializeField] private Color defaultColor = Color.cyan;

        [Header("Point Markers")]
        [SerializeField] private bool showPointMarkers = true;
        [SerializeField] private float markerSize = 0.005f;
        [SerializeField] private GameObject markerPrefab;

        [Header("Animation")]
        [SerializeField] private bool animatePath = false;
        [SerializeField] private float animationSpeed = 1.0f;

        private LineRenderer _lineRenderer;
        private Vector3[] _positions;
        private bool[] _reachability;
        private GameObject[] _markers;
        private float _animationProgress;

        public int PointCount => _positions?.Length ?? 0;
        public Vector3[] Positions => _positions;

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            SetupLineRenderer();
        }

        private void SetupLineRenderer()
        {
            _lineRenderer.startWidth = lineWidth;
            _lineRenderer.endWidth = lineWidth;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.numCornerVertices = 4;
            _lineRenderer.numCapVertices = 4;

            if (pathMaterial != null)
            {
                _lineRenderer.material = pathMaterial;
            }
            else
            {
                _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }
        }

        /// <summary>
        /// Set path positions without reachability data
        /// </summary>
        public void SetPath(Vector3[] positions)
        {
            SetPath(positions, null);
        }

        /// <summary>
        /// Set path positions with reachability data
        /// </summary>
        public void SetPath(Vector3[] positions, bool[] reachability)
        {
            _positions = positions;
            _reachability = reachability;
            _animationProgress = 0;

            UpdateLineRenderer();
            UpdateMarkers();
        }

        private void UpdateLineRenderer()
        {
            if (_positions == null || _positions.Length == 0)
            {
                _lineRenderer.positionCount = 0;
                return;
            }

            _lineRenderer.positionCount = _positions.Length;
            _lineRenderer.SetPositions(_positions);

            // Set colors based on reachability
            UpdateColors();
        }

        private void UpdateColors()
        {
            if (_positions == null || _positions.Length == 0) return;

            Gradient gradient = new Gradient();
            
            if (_reachability != null && _reachability.Length == _positions.Length)
            {
                // Create gradient based on reachability
                GradientColorKey[] colorKeys = new GradientColorKey[_positions.Length];
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];

                for (int i = 0; i < _positions.Length; i++)
                {
                    float t = (float)i / (_positions.Length - 1);
                    colorKeys[i] = new GradientColorKey(
                        _reachability[i] ? reachableColor : unreachableColor, t);
                }

                alphaKeys[0] = new GradientAlphaKey(1, 0);
                alphaKeys[1] = new GradientAlphaKey(1, 1);

                // Gradient has max 8 color keys, so sample if needed
                if (colorKeys.Length > 8)
                {
                    var sampledKeys = new GradientColorKey[8];
                    for (int i = 0; i < 8; i++)
                    {
                        int srcIdx = Mathf.RoundToInt(i * (colorKeys.Length - 1) / 7f);
                        sampledKeys[i] = colorKeys[srcIdx];
                    }
                    colorKeys = sampledKeys;
                }

                gradient.SetKeys(colorKeys, alphaKeys);
            }
            else
            {
                // Default solid color
                gradient.SetKeys(
                    new[] { new GradientColorKey(defaultColor, 0), new GradientColorKey(defaultColor, 1) },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }
                );
            }

            _lineRenderer.colorGradient = gradient;
        }

        private void UpdateMarkers()
        {
            ClearMarkers();

            if (!showPointMarkers || _positions == null || markerPrefab == null)
                return;

            // Create markers for key points (start, end, unreachable)
            _markers = new GameObject[_positions.Length];

            for (int i = 0; i < _positions.Length; i++)
            {
                bool isKeyPoint = i == 0 || i == _positions.Length - 1 || 
                    (_reachability != null && !_reachability[i]);

                if (isKeyPoint)
                {
                    var marker = Instantiate(markerPrefab, _positions[i], Quaternion.identity, transform);
                    marker.transform.localScale = Vector3.one * markerSize;

                    var renderer = marker.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        Color color = i == 0 ? Color.blue : 
                            (i == _positions.Length - 1 ? Color.yellow : unreachableColor);
                        renderer.material.color = color;
                    }

                    _markers[i] = marker;
                }
            }
        }

        private void ClearMarkers()
        {
            if (_markers != null)
            {
                foreach (var marker in _markers)
                {
                    if (marker != null)
                        Destroy(marker);
                }
                _markers = null;
            }
        }

        /// <summary>
        /// Clear all visualization
        /// </summary>
        public void Clear()
        {
            _positions = null;
            _reachability = null;
            _lineRenderer.positionCount = 0;
            ClearMarkers();
        }

        private void Update()
        {
            if (animatePath && _positions != null && _positions.Length > 0)
            {
                _animationProgress += Time.deltaTime * animationSpeed;
                if (_animationProgress > 1) _animationProgress = 0;

                int visibleCount = Mathf.CeilToInt(_animationProgress * _positions.Length);
                _lineRenderer.positionCount = visibleCount;
            }
        }

        /// <summary>
        /// Get position at normalized path distance (0-1)
        /// </summary>
        public Vector3 GetPositionAtDistance(float normalizedDistance)
        {
            if (_positions == null || _positions.Length == 0)
                return Vector3.zero;

            normalizedDistance = Mathf.Clamp01(normalizedDistance);
            float idx = normalizedDistance * (_positions.Length - 1);
            int i0 = Mathf.FloorToInt(idx);
            int i1 = Mathf.Min(i0 + 1, _positions.Length - 1);
            float t = idx - i0;

            return Vector3.Lerp(_positions[i0], _positions[i1], t);
        }

        /// <summary>
        /// Calculate total path length
        /// </summary>
        public float GetTotalLength()
        {
            if (_positions == null || _positions.Length < 2)
                return 0;

            float length = 0;
            for (int i = 1; i < _positions.Length; i++)
            {
                length += Vector3.Distance(_positions[i], _positions[i - 1]);
            }
            return length;
        }

        private void OnDrawGizmosSelected()
        {
            if (_positions == null || _positions.Length == 0) return;

            // Draw direction arrows
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _positions.Length - 1; i += _positions.Length / 10 + 1)
            {
                Vector3 dir = (_positions[i + 1] - _positions[i]).normalized;
                Gizmos.DrawLine(_positions[i], _positions[i] + dir * 0.02f);
            }
        }
    }
}
