// =============================================================================
// PipelineTests.cs - Unit Tests for Welding Pipeline
// =============================================================================
using System;
using UnityEngine;
using SMRWelding.Core;
using SMRWelding.Native;

namespace SMRWelding.Tests
{
    /// <summary>
    /// Test suite for the welding pipeline
    /// </summary>
    public class PipelineTests : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private bool runOnStart = false;
        [SerializeField] private int testPointCount = 1000;

        private void Start()
        {
            if (runOnStart)
            {
                RunAllTests();
            }
        }

        [ContextMenu("Run All Tests")]
        public void RunAllTests()
        {
            Debug.Log("=== Starting Pipeline Tests ===");
            
            int passed = 0;
            int failed = 0;

            // Test 1: Point generation
            if (TestPointGeneration()) passed++; else failed++;
            
            // Test 2: Pipeline config
            if (TestPipelineConfig()) passed++; else failed++;
            
            // Test 3: Vector math
            if (TestVectorMath()) passed++; else failed++;
            
            // Test 4: Matrix operations
            if (TestMatrixOperations()) passed++; else failed++;

            Debug.Log($"=== Tests Complete: {passed} passed, {failed} failed ===");
        }

        private bool TestPointGeneration()
        {
            Debug.Log("Testing point generation...");
            try
            {
                var points = GenerateTestPoints(testPointCount);
                
                if (points == null || points.Length != testPointCount)
                {
                    Debug.LogError($"Point generation failed: expected {testPointCount}, got {points?.Length ?? 0}");
                    return false;
                }

                // Check bounds
                float maxDist = 0;
                foreach (var p in points)
                {
                    maxDist = Mathf.Max(maxDist, p.magnitude);
                }

                if (maxDist > 2f)
                {
                    Debug.LogError($"Points out of expected bounds: max distance {maxDist}");
                    return false;
                }

                Debug.Log("✓ Point generation test passed");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Point generation test failed: {e.Message}");
                return false;
            }
        }

        private bool TestPipelineConfig()
        {
            Debug.Log("Testing pipeline config...");
            try
            {
                var config = new WeldingPipeline.Config
                {
                    VoxelSize = 0.002f,
                    NormalKNN = 30,
                    PoissonDepth = 8,
                    PathStepSize = 0.005f
                };

                // Validate ranges
                if (config.VoxelSize <= 0 || config.VoxelSize > 0.1f)
                {
                    Debug.LogError($"Invalid voxel size: {config.VoxelSize}");
                    return false;
                }

                if (config.NormalKNN < 1 || config.NormalKNN > 100)
                {
                    Debug.LogError($"Invalid NormalKNN: {config.NormalKNN}");
                    return false;
                }

                Debug.Log("✓ Pipeline config test passed");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Pipeline config test failed: {e.Message}");
                return false;
            }
        }

        private bool TestVectorMath()
        {
            Debug.Log("Testing vector math...");
            try
            {
                // Test cross product
                Vector3 a = Vector3.right;
                Vector3 b = Vector3.up;
                Vector3 c = Vector3.Cross(a, b);

                if (!ApproximatelyEqual(c, Vector3.forward))
                {
                    Debug.LogError($"Cross product failed: expected forward, got {c}");
                    return false;
                }

                // Test dot product
                float dot = Vector3.Dot(a, b);
                if (Mathf.Abs(dot) > 0.001f)
                {
                    Debug.LogError($"Dot product failed: expected 0, got {dot}");
                    return false;
                }

                // Test normalization
                Vector3 v = new Vector3(3, 4, 0);
                Vector3 normalized = v.normalized;
                if (Mathf.Abs(normalized.magnitude - 1f) > 0.001f)
                {
                    Debug.LogError($"Normalization failed: magnitude {normalized.magnitude}");
                    return false;
                }

                Debug.Log("✓ Vector math test passed");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Vector math test failed: {e.Message}");
                return false;
            }
        }

        private bool TestMatrixOperations()
        {
            Debug.Log("Testing matrix operations...");
            try
            {
                // Test identity
                Matrix4x4 identity = Matrix4x4.identity;
                Vector3 testPoint = new Vector3(1, 2, 3);
                Vector3 transformed = identity.MultiplyPoint3x4(testPoint);

                if (!ApproximatelyEqual(testPoint, transformed))
                {
                    Debug.LogError($"Identity transform failed: {transformed}");
                    return false;
                }

                // Test TRS
                Vector3 pos = new Vector3(10, 0, 0);
                Quaternion rot = Quaternion.identity;
                Vector3 scale = Vector3.one;
                Matrix4x4 trs = Matrix4x4.TRS(pos, rot, scale);

                Vector3 origin = trs.MultiplyPoint3x4(Vector3.zero);
                if (!ApproximatelyEqual(origin, pos))
                {
                    Debug.LogError($"TRS transform failed: expected {pos}, got {origin}");
                    return false;
                }

                // Test inverse
                Matrix4x4 inverse = trs.inverse;
                Vector3 backToOrigin = inverse.MultiplyPoint3x4(pos);
                if (!ApproximatelyEqual(backToOrigin, Vector3.zero))
                {
                    Debug.LogError($"Inverse transform failed: {backToOrigin}");
                    return false;
                }

                Debug.Log("✓ Matrix operations test passed");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Matrix operations test failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generate test points in hemisphere shape
        /// </summary>
        public static Vector3[] GenerateTestPoints(int count)
        {
            var points = new Vector3[count];
            float radius = 0.5f;

            for (int i = 0; i < count; i++)
            {
                float u = UnityEngine.Random.value;
                float v = UnityEngine.Random.value;

                float theta = 2 * Mathf.PI * u;
                float phi = Mathf.Acos(v); // hemisphere: 0 to PI/2

                float x = radius * Mathf.Sin(phi) * Mathf.Cos(theta);
                float y = radius * Mathf.Cos(phi);
                float z = radius * Mathf.Sin(phi) * Mathf.Sin(theta);

                // Add noise
                x += UnityEngine.Random.Range(-0.005f, 0.005f);
                y += UnityEngine.Random.Range(-0.005f, 0.005f);
                z += UnityEngine.Random.Range(-0.005f, 0.005f);

                points[i] = new Vector3(x, y, z);
            }

            return points;
        }

        private bool ApproximatelyEqual(Vector3 a, Vector3 b, float epsilon = 0.001f)
        {
            return Vector3.Distance(a, b) < epsilon;
        }
    }

    /// <summary>
    /// Native wrapper tests
    /// </summary>
    public class NativeWrapperTests : MonoBehaviour
    {
        [ContextMenu("Test Native Bindings Available")]
        public void TestNativeBindingsAvailable()
        {
            Debug.Log("Testing native bindings availability...");
            
            try
            {
                // Check if DLL can be loaded
                var dllPath = System.IO.Path.Combine(
                    Application.dataPath, 
                    "Plugins/x86_64/smr_welding_native.dll"
                );

                if (System.IO.File.Exists(dllPath))
                {
                    Debug.Log($"✓ DLL found at: {dllPath}");
                }
                else
                {
                    Debug.LogWarning($"✗ DLL not found at: {dllPath}");
                    Debug.Log("Build the native plugin first using CMake.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Test failed: {e.Message}");
            }
        }

        [ContextMenu("Test Point Cloud Wrapper")]
        public void TestPointCloudWrapper()
        {
            Debug.Log("Testing PointCloudWrapper...");
            
            try
            {
                using (var pcw = new PointCloudWrapper())
                {
                    var points = PipelineTests.GenerateTestPoints(100);
                    pcw.SetPoints(points);
                    
                    int count = pcw.GetPointCount();
                    Debug.Log($"Point count: {count}");
                    
                    if (count == 100)
                    {
                        Debug.Log("✓ PointCloudWrapper test passed");
                    }
                    else
                    {
                        Debug.LogError($"✗ Expected 100 points, got {count}");
                    }
                }
            }
            catch (DllNotFoundException e)
            {
                Debug.LogWarning($"Native DLL not loaded: {e.Message}");
                Debug.Log("Build the native plugin first.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Test failed: {e.Message}");
            }
        }
    }
}
