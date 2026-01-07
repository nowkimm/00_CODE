// =============================================================================
// PointCloudVisualizer.cs - Point Cloud Visualization Component
// =============================================================================
using UnityEngine;

namespace SMRWelding.Components
{
    /// <summary>
    /// Visualizes point cloud data using particle system or mesh
    /// </summary>
    public class PointCloudVisualizer : MonoBehaviour
    {
        public enum RenderMode
        {
            ParticleSystem,
            Mesh,
            Gizmos
        }

        [Header("Render Settings")]
        [SerializeField] private RenderMode renderMode = RenderMode.ParticleSystem;
        [SerializeField] private float pointSize = 0.005f;
        [SerializeField] private Color pointColor = Color.white;
        [SerializeField] private Material pointMaterial;

        [Header("LOD Settings")]
        [SerializeField] private bool useLOD = true;
        [SerializeField] private int maxVisiblePoints = 100000;
        [SerializeField] private float lodDistance = 10f;

        [Header("Normal Visualization")]
        [SerializeField] private bool showNormals = false;
        [SerializeField] private float normalLength = 0.01f;
        [SerializeField] private Color normalColor = Color.cyan;

        private Vector3[] _points;
        private Vector3[] _normals;
        private Color[] _colors;
        private ParticleSystem _particleSystem;
        private ParticleSystem.Particle[] _particles;
        private Mesh _pointMesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;

        public int PointCount => _points?.Length ?? 0;
        public Vector3[] Points => _points;
        public Vector3[] Normals => _normals;

        private void Awake()
        {
            SetupRenderers();
        }

        private void SetupRenderers()
        {
            // Setup particle system
            _particleSystem = GetComponent<ParticleSystem>();
            if (_particleSystem == null)
            {
                _particleSystem = gameObject.AddComponent<ParticleSystem>();
                var main = _particleSystem.main;
                main.loop = false;
                main.playOnAwake = false;
                main.maxParticles = maxVisiblePoints;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = _particleSystem.emission;
                emission.enabled = false;

                var shape = _particleSystem.shape;
                shape.enabled = false;

                var renderer = GetComponent<ParticleSystemRenderer>();
                if (pointMaterial != null)
                    renderer.material = pointMaterial;
            }

            // Setup mesh renderer
            _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null)
                _meshFilter = gameObject.AddComponent<MeshFilter>();

            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();

            _meshRenderer.enabled = false;
        }

        /// <summary>
        /// Set point cloud data
        /// </summary>
        public void SetPoints(Vector3[] points, Vector3[] normals = null, Color[] colors = null)
        {
            _points = points;
            _normals = normals;
            _colors = colors;

            UpdateVisualization();
        }

        /// <summary>
        /// Clear point cloud
        /// </summary>
        public void Clear()
        {
            _points = null;
            _normals = null;
            _colors = null;

            if (_particleSystem != null)
                _particleSystem.Clear();

            if (_pointMesh != null)
            {
                Destroy(_pointMesh);
                _pointMesh = null;
            }
        }

        private void UpdateVisualization()
        {
            if (_points == null || _points.Length == 0)
            {
                Clear();
                return;
            }

            switch (renderMode)
            {
                case RenderMode.ParticleSystem:
                    UpdateParticleSystem();
                    break;
                case RenderMode.Mesh:
                    UpdatePointMesh();
                    break;
                case RenderMode.Gizmos:
                    // Gizmos are drawn in OnDrawGizmos
                    break;
            }
        }

        private void UpdateParticleSystem()
        {
            if (_particleSystem == null) return;

            int count = useLOD ? Mathf.Min(_points.Length, maxVisiblePoints) : _points.Length;
            int stride = _points.Length / count;

            if (_particles == null || _particles.Length != count)
                _particles = new ParticleSystem.Particle[count];

            for (int i = 0; i < count; i++)
            {
                int srcIdx = i * stride;
                _particles[i].position = _points[srcIdx];
                _particles[i].startSize = pointSize;
                _particles[i].startColor = _colors != null ? _colors[srcIdx] : pointColor;
                _particles[i].remainingLifetime = 1000f;
            }

            _particleSystem.SetParticles(_particles, count);
            _meshRenderer.enabled = false;
        }

        private void UpdatePointMesh()
        {
            if (_meshFilter == null) return;

            int count = useLOD ? Mathf.Min(_points.Length, maxVisiblePoints) : _points.Length;
            int stride = _points.Length / count;

            if (_pointMesh == null)
            {
                _pointMesh = new Mesh();
                _pointMesh.name = "PointCloudMesh";
            }

            Vector3[] vertices = new Vector3[count];
            int[] indices = new int[count];
            Color[] colors = new Color[count];

            for (int i = 0; i < count; i++)
            {
                int srcIdx = i * stride;
                vertices[i] = _points[srcIdx];
                indices[i] = i;
                colors[i] = _colors != null ? _colors[srcIdx] : pointColor;
            }

            _pointMesh.Clear();
            _pointMesh.vertices = vertices;
            _pointMesh.colors = colors;
            _pointMesh.SetIndices(indices, MeshTopology.Points, 0);

            _meshFilter.mesh = _pointMesh;
            _meshRenderer.enabled = true;
            _meshRenderer.material = pointMaterial;

            if (_particleSystem != null)
                _particleSystem.Clear();
        }

        /// <summary>
        /// Set render mode
        /// </summary>
        public void SetRenderMode(RenderMode mode)
        {
            renderMode = mode;
            UpdateVisualization();
        }

        /// <summary>
        /// Set point size
        /// </summary>
        public void SetPointSize(float size)
        {
            pointSize = size;
            UpdateVisualization();
        }

        /// <summary>
        /// Set point color
        /// </summary>
        public void SetPointColor(Color color)
        {
            pointColor = color;
            UpdateVisualization();
        }

        /// <summary>
        /// Get bounds of point cloud
        /// </summary>
        public Bounds GetBounds()
        {
            if (_points == null || _points.Length == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            Bounds bounds = new Bounds(_points[0], Vector3.zero);
            foreach (var p in _points)
                bounds.Encapsulate(p);

            return bounds;
        }

        /// <summary>
        /// Center camera on point cloud
        /// </summary>
        public void FocusCamera(Camera cam)
        {
            if (cam == null || _points == null) return;

            Bounds bounds = GetBounds();
            float distance = bounds.size.magnitude * 1.5f;
            
            cam.transform.position = bounds.center - cam.transform.forward * distance;
            cam.transform.LookAt(bounds.center);
        }

        private void OnDrawGizmos()
        {
            if (renderMode != RenderMode.Gizmos) return;
            if (_points == null || _points.Length == 0) return;

            Gizmos.color = pointColor;
            int stride = useLOD ? Mathf.Max(1, _points.Length / maxVisiblePoints) : 1;

            for (int i = 0; i < _points.Length; i += stride)
            {
                Gizmos.DrawSphere(_points[i], pointSize * 0.5f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!showNormals || _points == null || _normals == null) return;

            Gizmos.color = normalColor;
            int stride = Mathf.Max(1, _points.Length / 1000);

            for (int i = 0; i < _points.Length && i < _normals.Length; i += stride)
            {
                Gizmos.DrawLine(_points[i], _points[i] + _normals[i] * normalLength);
            }
        }
    }
}
