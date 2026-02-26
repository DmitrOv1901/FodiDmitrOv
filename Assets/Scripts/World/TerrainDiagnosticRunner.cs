using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fodinae.Assets.Scripts.Game.Managers;
using Fodinae.Assets.Scripts.World;
using MinesServer.Data;

namespace Fodinae.Assets.Scripts.World
{
    /// <summary>
    /// Diagnostic runner that automatically tests the terrain rendering system
    /// and provides immediate feedback about what's wrong.
    /// Add this to any GameObject to run diagnostics.
    /// </summary>
    [RequireComponent(typeof(WorldBackgroundRenderer))]
    public class TerrainDiagnosticRunner : MonoBehaviour
    {
        [Header("Diagnostic Settings")]
        [Tooltip("Enable automatic diagnostics on start")]
        [SerializeField] private bool _autoRunOnStart = true;
        [Tooltip("Delay before running diagnostics (seconds)")]
        [SerializeField] private float _startDelay = 2f;
        [Tooltip("Enable detailed console logging")]
        [SerializeField] private bool _enableDetailedLogging = true;

        private WorldBackgroundRenderer _renderer;
        private bool _diagnosticsCompleted = false;

        void Start()
        {
            _renderer = GetComponent<WorldBackgroundRenderer>();
            
            if (_autoRunOnStart)
            {
                StartCoroutine(RunDiagnostics());
            }
        }

        /// <summary>
        /// Run comprehensive diagnostics and provide immediate feedback
        /// </summary>
        public IEnumerator RunDiagnostics()
        {
            if (_diagnosticsCompleted)
            {
                Debug.Log("=== Terrain Diagnostics Already Completed ===");
                yield break;
            }

            Debug.Log("=== TERRAIN DIAGNOSTIC RUNNER STARTED ===");
            Debug.Log("This will identify why terrain is rendering white...");

            yield return new WaitForSeconds(_startDelay);

            // Test 1: Check MapManager
            Debug.Log("\n--- Test 1: MapManager Status ---");
            bool mapManagerOk = TestMapManager();
            
            // Test 2: Check MapStorage
            Debug.Log("\n--- Test 2: MapStorage Status ---");
            bool mapStorageOk = TestMapStorage();
            
            // Test 3: Check WorldBackgroundRenderer
            Debug.Log("\n--- Test 3: WorldBackgroundRenderer Status ---");
            bool rendererOk = TestRenderer();
            
            // Test 4: Check World Data Access
            Debug.Log("\n--- Test 4: World Data Access ---");
            bool worldDataOk = TestWorldDataAccess();
            
            // Test 5: Check Texture System
            Debug.Log("\n--- Test 5: Texture System ---");
            bool textureSystemOk = TestTextureSystem();

            // Summary and Recommendations
            Debug.Log("\n=== DIAGNOSTIC SUMMARY ===");
            Debug.Log($"MapManager: {(mapManagerOk ? "✓ OK" : "✗ FAILED")}");
            Debug.Log($"MapStorage: {(mapStorageOk ? "✓ OK" : "✗ FAILED")}");
            Debug.Log($"Renderer: {(rendererOk ? "✓ OK" : "✗ FAILED")}");
            Debug.Log($"World Data: {(worldDataOk ? "✓ OK" : "✗ FAILED")}");
            Debug.Log($"Texture System: {(textureSystemOk ? "✓ OK" : "✗ FAILED")}");

            if (!mapManagerOk || !mapStorageOk || !rendererOk || !worldDataOk || !textureSystemOk)
            {
                Debug.LogError("\n=== ISSUES FOUND - TERRAIN WILL RENDER WHITE ===");
                
                if (!mapManagerOk)
                {
                    Debug.LogError("❌ MapManager not available - world data cannot be loaded");
                    Debug.LogError("   Solution: Ensure MapManager component exists in scene");
                }
                
                if (!mapStorageOk)
                {
                    Debug.LogError("❌ MapStorage not ready - world cells cannot be accessed");
                    Debug.LogError("   Solution: Check MapStorage initialization, verify world dimensions");
                }
                
                if (!rendererOk)
                {
                    Debug.LogError("❌ WorldBackgroundRenderer not configured - mesh/texture not applied");
                    Debug.LogError("   Solution: Check renderer initialization, verify material setup");
                }
                
                if (!worldDataOk)
                {
                    Debug.LogError("❌ World data not accessible - no cell data to render");
                    Debug.LogError("   Solution: Ensure world is properly initialized with valid data");
                }
                
                if (!textureSystemOk)
                {
                    Debug.LogError("❌ Texture system failed - no textures to display");
                    Debug.LogError("   Solution: Check texture loading, verify fallback textures work");
                }
                
                Debug.LogError("\n=== IMMEDIATE ACTIONS ===");
                Debug.LogError("1. Check Unity Console for specific error messages");
                Debug.LogError("2. Verify MapManager has received world data from server");
                Debug.LogError("3. Run ForceSystemReinitialize() if needed");
                Debug.LogError("4. Check file permissions for world data storage");
            }
            else
            {
                Debug.Log("\n✅ All systems appear healthy - terrain should render correctly");
                Debug.Log("If terrain is still white, check:");
                Debug.Log("- Camera position and render distance");
                Debug.Log("- Lighting and material settings");
                Debug.Log("- Texture loading from server");
            }

            _diagnosticsCompleted = true;
            Debug.Log("=== TERRAIN DIAGNOSTIC RUNNER COMPLETED ===");
        }

        private bool TestMapManager()
        {
            if (MapManager.Instance == null)
            {
                Debug.LogError("❌ MapManager: Not found in scene");
                return false;
            }

            Debug.Log($"✅ MapManager: Found ({MapManager.Instance.WorldDisplayName})");
            Debug.Log($"   World Code: {MapManager.Instance.WorldCodeName}");
            Debug.Log($"   Dimensions: {MapManager.Instance.WorldWidth}x{MapManager.Instance.WorldHeight}");
            Debug.Log($"   IsStandaloneMode: {MapManager.Instance.IsStandaloneMode}");

            if (string.IsNullOrEmpty(MapManager.Instance.WorldCodeName))
            {
                Debug.LogWarning("⚠ MapManager: No world code name set (may be waiting for server data)");
                return false;
            }

            if (MapManager.Instance.WorldWidth <= 0 || MapManager.Instance.WorldHeight <= 0)
            {
                Debug.LogWarning("⚠ MapManager: Invalid world dimensions (may be waiting for server data)");
                return false;
            }

            return true;
        }

        private bool TestMapStorage()
        {
            if (MapStorage.Instance == null)
            {
                Debug.LogError("❌ MapStorage: Not available");
                return false;
            }

            Debug.Log($"✅ MapStorage: Available");
            Debug.Log($"   IsReady: {MapStorage.Instance.IsReady}");
            Debug.Log($"   IsInitialized: {MapStorage.Instance.IsInitialized()}");
            Debug.Log($"   World Code: {MapStorage.Instance.GetWorldCodeName()}");

            if (!MapStorage.Instance.IsReady)
            {
                Debug.LogError("❌ MapStorage: Not ready - world data not loaded");
                
                if (MapStorage.Instance.cellLayer == null)
                {
                    Debug.LogError("   Root cause: cellLayer is null - WorldLayer creation failed");
                    Debug.LogError("   This is CRITICAL - terrain rendering cannot work without cellLayer");
                }
                else
                {
                    Debug.LogError("   Root cause: MapStorage not properly initialized");
                }
                return false;
            }

            if (MapStorage.Instance.cellLayer == null)
            {
                Debug.LogError("❌ MapStorage: cellLayer is null despite being ready");
                return false;
            }

            Debug.Log($"   CellLayer: {MapStorage.Instance.cellLayer.WidthChunks}x{MapStorage.Instance.cellLayer.HeightChunks} chunks");
            Debug.Log($"   ChunkSize: {MapStorage.Instance.cellLayer.ChunkSize}");

            return true;
        }

        private bool TestRenderer()
        {
            if (_renderer == null)
            {
                Debug.LogError("❌ WorldBackgroundRenderer: Component not found");
                return false;
            }

            Debug.Log($"✅ WorldBackgroundRenderer: Found");
            Debug.Log($"   IsProperlyConfigured: {_renderer.IsProperlyConfigured()}");
            Debug.Log($"   VisibleChunks: {_renderer.GetVisibleChunkCount()}");
            Debug.Log($"   TexturesLoaded: {_renderer.AreTexturesLoaded()}");
            Debug.Log($"   AtlasApplied: {_renderer.IsAtlasApplied()}");
            Debug.Log($"   RendererState: {_renderer.GetRendererState()}");

            if (!_renderer.IsProperlyConfigured())
            {
                Debug.LogWarning("⚠ WorldBackgroundRenderer: Not properly configured");
                Debug.LogWarning("   This may be normal during initialization");
            }

            if (_renderer.GetVisibleChunkCount() == 0)
            {
                Debug.LogWarning("⚠ WorldBackgroundRenderer: No visible chunks");
                Debug.LogWarning("   Check camera position and render distance");
            }

            return true;
        }

        private bool TestWorldDataAccess()
        {
            if (MapStorage.Instance == null || !MapStorage.Instance.IsReady)
            {
                Debug.LogError("❌ Cannot test world data - MapStorage not ready");
                return false;
            }

            Debug.Log("✅ Testing world data access...");

            try
            {
                // Test accessing a few cells
                var testCell1 = MapStorage.Instance.GetCell(0, 0);
                var testCell2 = MapStorage.Instance.GetCell(10, 10);
                var testCell3 = MapStorage.Instance.GetCell(100, 100);

                Debug.Log($"   Cell (0,0): {testCell1}");
                Debug.Log($"   Cell (10,10): {testCell2}");
                Debug.Log($"   Cell (100,100): {testCell3}");

                // Check if we have valid data
                bool hasValidData = testCell1 != CellType.Unloaded && testCell1 != CellType.Pregener;
                
                if (hasValidData)
                {
                    Debug.Log("✅ World data accessible and contains valid cell types");
                    return true;
                }
                else
                {
                    Debug.LogWarning("⚠ World data contains only Unloaded/Pregener cells");
                    Debug.LogWarning("   This may indicate world data not fully loaded yet");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Error accessing world data: {ex.Message}");
                return false;
            }
        }

        private bool TestTextureSystem()
        {
            Debug.Log("✅ Testing texture system...");

            try
            {
                // Test WorldTextureManager
                if (WorldTextureManager.Instance == null)
                {
                    Debug.LogError("❌ WorldTextureManager: Not available");
                    return false;
                }

                Debug.Log("✅ WorldTextureManager: Available");

                // Test atlas access
                var atlases = WorldTextureManager.Instance.GetAllAtlases();
                Debug.Log($"   Atlas count: {atlases.Count}");

                if (atlases.Count > 0)
                {
                    var atlas = atlases[0];
                    Debug.Log($"   Atlas size: {atlas.Size}x{atlas.Size}");
                    Debug.Log($"   Cell size: {atlas.CellSize}");
                    
                    // Try to get atlas texture
                    var atlasTexture = atlas.GetAtlasTexture().GetAwaiter().GetResult();
                    if (atlasTexture != null)
                    {
                        Debug.Log($"✅ Atlas texture available: {atlasTexture.width}x{atlasTexture.height}");
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning("⚠ Atlas texture not available yet");
                    }
                }
                else
                {
                    Debug.LogWarning("⚠ No atlases available yet");
                }

                return false; // Texture system not ready
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"❌ Error testing texture system: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Force complete system reinitialization
        /// </summary>
        public void ForceSystemReinitialize()
        {
            Debug.Log("=== FORCING SYSTEM REINITIALIZATION ===");
            
            // Reset MapStorage
            if (MapStorage.Instance != null)
            {
                MapStorage.Instance.Dispose();
                Debug.Log("✅ MapStorage disposed");
            }

            // Force renderer reinitialization
            if (_renderer != null)
            {
                _renderer.ForceReinitialize();
                Debug.Log("✅ Renderer reinitialized");
            }

            // Wait and test again
            StartCoroutine(DelayedReTest());
        }

        private IEnumerator DelayedReTest()
        {
            yield return new WaitForSeconds(2f);
            Debug.Log("=== RUNNING DIAGNOSTICS AFTER REINITIALIZATION ===");
            StartCoroutine(RunDiagnostics());
        }

        /// <summary>
        /// Get detailed diagnostic information for debugging
        /// </summary>
        public string GetDetailedDiagnosticInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== DETAILED TERRAIN DIAGNOSTIC INFO ===");

            if (MapManager.Instance != null)
            {
                info.AppendLine($"MapManager: {MapManager.Instance.WorldDisplayName} ({MapManager.Instance.WorldCodeName})");
                info.AppendLine($"Dimensions: {MapManager.Instance.WorldWidth}x{MapManager.Instance.WorldHeight}");
            }
            else
            {
                info.AppendLine("MapManager: Not available");
            }

            if (MapStorage.Instance != null)
            {
                info.AppendLine($"MapStorage: Ready={MapStorage.Instance.IsReady}, Initialized={MapStorage.Instance.IsInitialized()}");
                info.AppendLine($"World: {MapStorage.Instance.GetWorldCodeName()}");
                info.AppendLine($"CellLayer: {(MapStorage.Instance.cellLayer != null ? "Available" : "NULL - CRITICAL!")}");
            }
            else
            {
                info.AppendLine("MapStorage: Not available");
            }

            if (_renderer != null)
            {
                info.AppendLine($"Renderer: Configured={_renderer.IsProperlyConfigured()}, Chunks={_renderer.GetVisibleChunkCount()}");
                info.AppendLine($"Textures: Loaded={_renderer.AreTexturesLoaded()}, Atlas={_renderer.IsAtlasApplied()}");
            }
            else
            {
                info.AppendLine("Renderer: Not available");
            }

            return info.ToString();
        }

        private void OnValidate()
        {
            if (_startDelay < 0f) _startDelay = 0f;
            if (_startDelay > 30f) _startDelay = 30f;
        }
    }
}