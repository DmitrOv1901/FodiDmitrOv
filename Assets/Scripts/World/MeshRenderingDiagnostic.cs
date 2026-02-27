using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fodinae.Assets.Scripts.Game.Managers;
using Fodinae.Assets.Scripts.World;
using MinesServer.Data;

namespace Fodinae.Assets.Scripts.World
{
    /// <summary>
    /// Diagnostic script specifically for mesh rendering issues.
    /// This script checks if the mesh is being generated and rendered properly.
    /// </summary>
    [RequireComponent(typeof(WorldBackgroundRenderer))]
    public class MeshRenderingDiagnostic : MonoBehaviour
    {
        [Header("Diagnostic Settings")]
        [Tooltip("Enable automatic mesh diagnostics")]
        [SerializeField] private bool _autoRunDiagnostics = true;
        [Tooltip("Diagnostic interval in seconds")]
        [SerializeField] private float _diagnosticInterval = 2f;

        private WorldBackgroundRenderer _renderer;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private float _lastDiagnosticTime = 0f;

        void Start()
        {
            _renderer = GetComponent<WorldBackgroundRenderer>();
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            
            if (_autoRunDiagnostics)
            {
                StartCoroutine(RunMeshDiagnostics());
            }
        }

        void Update()
        {
            // Run diagnostics periodically
            if (_autoRunDiagnostics && Time.time - _lastDiagnosticTime >= _diagnosticInterval)
            {
                RunMeshStateCheck();
                _lastDiagnosticTime = Time.time;
            }
        }

        /// <summary>
        /// Run comprehensive mesh rendering diagnostics
        /// </summary>
        public IEnumerator RunMeshDiagnostics()
        {
            Debug.Log("=== MESH RENDERING DIAGNOSTIC STARTED ===");

            yield return new WaitForSeconds(1f);

            // Test 1: Check components
            Debug.Log("\n--- Test 1: Component Status ---");
            bool componentsOk = CheckComponents();

            // Test 2: Check mesh state
            Debug.Log("\n--- Test 2: Mesh State ---");
            bool meshOk = CheckMeshState();

            // Test 3: Check renderer state
            Debug.Log("\n--- Test 3: Renderer State ---");
            bool rendererOk = CheckRendererState();

            // Test 4: Check world data
            Debug.Log("\n--- Test 4: World Data ---");
            bool worldDataOk = CheckWorldData();

            // Test 5: Force mesh regeneration
            Debug.Log("\n--- Test 5: Force Mesh Regeneration ---");
            bool regenerationOk = ForceMeshRegeneration();

            // Summary
            Debug.Log("\n=== MESH DIAGNOSTIC SUMMARY ===");
            Debug.Log($"Components: {(componentsOk ? "✓ OK" : "✗ FAILED")}");
            Debug.Log($"Mesh State: {(meshOk ? "✓ OK" : "✗ FAILED")}");
            Debug.Log($"Renderer: {(rendererOk ? "✓ OK" : "✗ FAILED")}");
            Debug.Log($"World Data: {(worldDataOk ? "✓ OK" : "✗ FAILED")}");
            Debug.Log($"Regeneration: {(regenerationOk ? "✓ OK" : "✗ FAILED")}");

            if (!componentsOk || !meshOk || !rendererOk || !worldDataOk || !regenerationOk)
            {
                Debug.LogError("\n=== MESH RENDERING ISSUES FOUND ===");
                
                if (!componentsOk)
                {
                    Debug.LogError("❌ Missing required components (MeshFilter, MeshRenderer, WorldBackgroundRenderer)");
                }
                
                if (!meshOk)
                {
                    Debug.LogError("❌ Mesh is null or empty (no vertices/triangles)");
                }
                
                if (!rendererOk)
                {
                    Debug.LogError("❌ Renderer issues (disabled, no material, or not visible)");
                }
                
                if (!worldDataOk)
                {
                    Debug.LogError("❌ No world data available for mesh generation");
                }
                
                if (!regenerationOk)
                {
                    Debug.LogError("❌ Mesh regeneration failed");
                }
            }
            else
            {
                Debug.Log("\n✅ All mesh rendering components appear healthy");
                Debug.Log("If mesh still not visible, check:");
                Debug.Log("- Camera position and render distance");
                Debug.Log("- Layer settings and camera culling");
                Debug.Log("- Material transparency or shader issues");
            }

            Debug.Log("=== MESH RENDERING DIAGNOSTIC COMPLETED ===");
        }

        private bool CheckComponents()
        {
            bool allComponentsPresent = true;

            if (_renderer == null)
            {
                Debug.LogError("❌ WorldBackgroundRenderer component not found");
                allComponentsPresent = false;
            }
            else
            {
                Debug.Log("✅ WorldBackgroundRenderer: Found");
            }

            if (_meshFilter == null)
            {
                Debug.LogError("❌ MeshFilter component not found");
                allComponentsPresent = false;
            }
            else
            {
                Debug.Log("✅ MeshFilter: Found");
            }

            if (_meshRenderer == null)
            {
                Debug.LogError("❌ MeshRenderer component not found");
                allComponentsPresent = false;
            }
            else
            {
                Debug.Log("✅ MeshRenderer: Found");
            }

            return allComponentsPresent;
        }

        private bool CheckMeshState()
        {
            if (_meshFilter == null)
            {
                Debug.LogError("❌ Cannot check mesh - MeshFilter is null");
                return false;
            }

            _mesh = _meshFilter.mesh;

            if (_mesh == null)
            {
                Debug.LogError("❌ Mesh is null");
                return false;
            }

            Debug.Log($"✅ Mesh: Found ({_mesh.name})");
            Debug.Log($"   Vertices: {_mesh.vertexCount}");
            Debug.Log($"   Triangles: {_mesh.triangles.Length}");
            Debug.Log($"   UVs: {_mesh.uv.Length}");
            Debug.Log($"   Bounds: {_mesh.bounds}");

            bool hasGeometry = _mesh.vertexCount > 0 && _mesh.triangles.Length > 0;

            if (!hasGeometry)
            {
                Debug.LogWarning("⚠ Mesh has no geometry (0 vertices or triangles)");
                Debug.LogWarning("   This means mesh generation is not working");
            }
            else
            {
                Debug.Log("✅ Mesh has geometry");
            }

            return hasGeometry;
        }

        private bool CheckRendererState()
        {
            if (_meshRenderer == null)
            {
                Debug.LogError("❌ Cannot check renderer - MeshRenderer is null");
                return false;
            }

            Debug.Log($"✅ MeshRenderer: Enabled={_meshRenderer.enabled}");
            Debug.Log($"   Material: {(_meshRenderer.material != null ? _meshRenderer.material.name : "NULL")}");
            Debug.Log($"   Sorting Order: {_meshRenderer.sortingOrder}");
            Debug.Log($"   Shadow Casting: {_meshRenderer.shadowCastingMode}");
            Debug.Log($"   Receive Shadows: {_meshRenderer.receiveShadows}");

            bool rendererEnabled = _meshRenderer.enabled;
            bool hasMaterial = _meshRenderer.material != null;

            if (!rendererEnabled)
            {
                Debug.LogWarning("⚠ MeshRenderer is disabled");
            }

            if (!hasMaterial)
            {
                Debug.LogWarning("⚠ MeshRenderer has no material");
            }

            return rendererEnabled && hasMaterial;
        }

        private bool CheckWorldData()
        {
            if (MapStorage.Instance == null || !MapStorage.Instance.IsReady)
            {
                Debug.LogError("❌ MapStorage not ready - no world data for mesh generation");
                return false;
            }

            Debug.Log("✅ MapStorage: Ready");

            try
            {
                // Test accessing a few cells
                var testCell1 = MapStorage.Instance.GetCell(0, 0);
                var testCell2 = MapStorage.Instance.GetCell(10, 10);
                
                Debug.Log($"   Test cell (0,0): {testCell1}");
                Debug.Log($"   Test cell (10,10): {testCell2}");

                bool hasValidData = testCell1 != CellType.Unloaded && testCell1 != CellType.Pregener;
                
                if (hasValidData)
                {
                    Debug.Log("✅ World data contains valid cell types");
                    return true;
                }
                else
                {
                    Debug.LogWarning("⚠ World data contains only Unloaded/Pregener cells");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Error accessing world data: {ex.Message}");
                return false;
            }
        }

        private bool ForceMeshRegeneration()
        {
            Debug.Log("🔄 Attempting to force mesh regeneration...");

            if (_renderer == null)
            {
                Debug.LogError("❌ Cannot force regeneration - WorldBackgroundRenderer not found");
                return false;
            }

            try
            {
                // Force reinitialization
                _renderer.ForceInitialization();
                
                // Wait a moment for regeneration
                Debug.Log("   Waiting for mesh regeneration...");
                
                // Check if mesh has geometry after regeneration
                if (_meshFilter != null && _meshFilter.mesh != null)
                {
                    var mesh = _meshFilter.mesh;
                    bool hasGeometry = mesh.vertexCount > 0 && mesh.triangles.Length > 0;
                    
                    Debug.Log($"   After regeneration - Vertices: {mesh.vertexCount}, Triangles: {mesh.triangles.Length}");
                    
                    if (hasGeometry)
                    {
                        Debug.Log("✅ Mesh regeneration successful");
                        return true;
                    }
                    else
                    {
                        Debug.LogError("❌ Mesh regeneration failed - still no geometry");
                        return false;
                    }
                }
                else
                {
                    Debug.LogError("❌ Mesh still null after regeneration attempt");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Error during mesh regeneration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check current mesh state immediately
        /// </summary>
        public void RunMeshStateCheck()
        {
            Debug.Log("=== MESH STATE CHECK ===");

            if (_meshFilter != null && _meshFilter.mesh != null)
            {
                var mesh = _meshFilter.mesh;
                Debug.Log($"Mesh vertices: {mesh.vertexCount}");
                Debug.Log($"Mesh triangles: {mesh.triangles.Length}");
                Debug.Log($"Mesh UVs: {mesh.uv.Length}");
                
                if (mesh.vertexCount == 0 || mesh.triangles.Length == 0)
                {
                    Debug.LogWarning("⚠ Mesh has no geometry - mesh generation may have failed");
                }
                else
                {
                    Debug.Log("✅ Mesh has geometry");
                }
            }
            else
            {
                Debug.LogError("❌ Mesh is null or MeshFilter is missing");
            }

            if (_meshRenderer != null)
            {
                Debug.Log($"Renderer enabled: {_meshRenderer.enabled}");
                Debug.Log($"Material assigned: {_meshRenderer.material != null}");
            }
        }

        /// <summary>
        /// Force immediate mesh regeneration
        /// </summary>
        public void ForceRegeneration()
        {
            Debug.Log("=== FORCING MESH REGENERATION ===");
            
            if (_renderer != null)
            {
                _renderer.ForceInitialization();
                Debug.Log("✅ Force initialization called");
            }
            else
            {
                Debug.LogError("❌ WorldBackgroundRenderer not found");
            }
        }

        /// <summary>
        /// Get detailed mesh information
        /// </summary>
        public string GetMeshInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== MESH DIAGNOSTIC INFO ===");

            if (_meshFilter != null && _meshFilter.mesh != null)
            {
                var mesh = _meshFilter.mesh;
                info.AppendLine($"Mesh Name: {mesh.name}");
                info.AppendLine($"Vertices: {mesh.vertexCount}");
                info.AppendLine($"Triangles: {mesh.triangles.Length}");
                info.AppendLine($"UVs: {mesh.uv.Length}");
                info.AppendLine($"Bounds: {mesh.bounds}");
            }
            else
            {
                info.AppendLine("Mesh: NULL");
            }

            if (_meshRenderer != null)
            {
                info.AppendLine($"Renderer Enabled: {_meshRenderer.enabled}");
                info.AppendLine($"Material: {(_meshRenderer.material != null ? _meshRenderer.material.name : "NULL")}");
                info.AppendLine($"Sorting Order: {_meshRenderer.sortingOrder}");
            }

            return info.ToString();
        }

        private void OnValidate()
        {
            if (_diagnosticInterval < 0.5f) _diagnosticInterval = 0.5f;
            if (_diagnosticInterval > 30f) _diagnosticInterval = 30f;
        }
    }
}