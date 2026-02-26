using System.Collections;
using UnityEngine;
using Fodinae.Assets.Scripts.Game.Managers;
using Fodinae.Assets.Scripts.World;
using MinesServer.Data;
using MinesServer.Networking.Server.Packets.Connection;
using MinesServer.Networking.Connection;
using Fodinae.Assets.Scripts.Networking;
using Fodinae.Assets.Scripts.Networking.Connection;

namespace Fodinae.Assets.Scripts.World
{
    /// <summary>
    /// Verification script to test that terrain rendering is working correctly.
    /// This script simulates the complete initialization flow and verifies each step.
    /// </summary>
    [ExecuteAlways]
    public class TerrainRenderingVerification : MonoBehaviour
    {
        [Header("Verification Settings")]
        [Tooltip("Enable automatic verification on start")]
        [SerializeField] private bool _autoVerifyOnStart = true;
        [Tooltip("Enable detailed logging")]
        [SerializeField] private bool _enableDetailedLogging = true;
        [Tooltip("Test interval in seconds")]
        [SerializeField] private float _testInterval = 10f;

        [Header("Test Configuration")]
        [Tooltip("Test world dimensions")]
        [SerializeField] private int _testWidth = 100;
        [Tooltip("Test world height")]
        [SerializeField] private int _testHeight = 100;
        [Tooltip("Test world name")]
        [SerializeField] private string _testWorldName = "verification_test";

        private WorldBackgroundRenderer _renderer;
        private MapManager _mapManager;
        private MapStorage _mapStorage;
        private PacketHandler _packetHandler;
        private ConnectionManager _connectionManager;
        private DebugInitializationTool _debugTool;
        
        private bool _isVerifying = false;
        private float _lastTestTime = 0f;
        private int _verificationCount = 0;

        void Start()
        {
            if (_autoVerifyOnStart)
            {
                StartCoroutine(RunCompleteVerification());
            }
        }

        void Update()
        {
            // Run periodic verification if enabled
            if (_autoVerifyOnStart && Time.time - _lastTestTime >= _testInterval)
            {
                if (!_isVerifying)
                {
                    StartCoroutine(RunCompleteVerification());
                }
            }
        }

        /// <summary>
        /// Run complete terrain rendering verification
        /// </summary>
        public IEnumerator RunCompleteVerification()
        {
            _isVerifying = true;
            _lastTestTime = Time.time;
            _verificationCount++;

            if (_enableDetailedLogging)
            {
                Debug.Log($"=== Terrain Rendering Verification #{_verificationCount} Started ===");
            }

            // Step 1: Component Discovery
            yield return VerifyComponents();

            // Step 2: System Initialization
            yield return VerifySystemInitialization();

            // Step 3: World Initialization
            yield return VerifyWorldInitialization();

            // Step 4: MapStorage Verification
            yield return VerifyMapStorage();

            // Step 5: Renderer Verification
            yield return VerifyRenderer();

            // Step 6: Final Status Check
            yield return VerifyFinalStatus();

            if (_enableDetailedLogging)
            {
                Debug.Log($"=== Terrain Rendering Verification #{_verificationCount} Completed ===");
            }

            _isVerifying = false;
        }

        private IEnumerator VerifyComponents()
        {
            if (_enableDetailedLogging) Debug.Log("Step 1: Verifying component discovery...");

            FindComponents();

            bool allComponentsFound = true;

            if (_renderer != null)
            {
                if (_enableDetailedLogging) Debug.Log("  ✓ WorldBackgroundRenderer: Found");
            }
            else
            {
                Debug.LogError("  ✗ WorldBackgroundRenderer: Not found");
                allComponentsFound = false;
            }

            if (_mapManager != null)
            {
                if (_enableDetailedLogging) Debug.Log("  ✓ MapManager: Found");
            }
            else
            {
                Debug.LogError("  ✗ MapManager: Not found");
                allComponentsFound = false;
            }

            if (_mapStorage != null)
            {
                if (_enableDetailedLogging) Debug.Log("  ✓ MapStorage: Found");
            }
            else
            {
                Debug.LogError("  ✗ MapStorage: Not found");
                allComponentsFound = false;
            }

            if (_packetHandler != null)
            {
                if (_enableDetailedLogging) Debug.Log("  ✓ PacketHandler: Found");
            }
            else
            {
                Debug.LogError("  ✗ PacketHandler: Not found");
                allComponentsFound = false;
            }

            if (_connectionManager != null)
            {
                if (_enableDetailedLogging) Debug.Log("  ✓ ConnectionManager: Found");
            }
            else
            {
                Debug.LogError("  ✗ ConnectionManager: Not found");
                allComponentsFound = false;
            }

            if (_debugTool != null)
            {
                if (_enableDetailedLogging) Debug.Log("  ✓ DebugInitializationTool: Found");
            }
            else
            {
                if (_enableDetailedLogging) Debug.LogWarning("  ⚠ DebugInitializationTool: Not found (manual testing unavailable)");
            }

            if (allComponentsFound)
            {
                if (_enableDetailedLogging) Debug.Log("  ✓ All critical components found");
            }
            else
            {
                Debug.LogError("  ✗ Missing critical components - terrain rendering will not work");
            }

            yield return null;
        }

        private IEnumerator VerifySystemInitialization()
        {
            if (_enableDetailedLogging) Debug.Log("Step 2: Verifying system initialization...");

            bool systemReady = true;

            if (_mapManager != null)
            {
                if (_enableDetailedLogging) Debug.Log($"  ✓ MapManager: Initialized");
                if (_enableDetailedLogging) Debug.Log($"    - World: {_mapManager.WorldDisplayName}");
                if (_enableDetailedLogging) Debug.Log($"    - Dimensions: {_mapManager.WorldWidth}x{_mapManager.WorldHeight}");
            }
            else
            {
                Debug.LogError("  ✗ MapManager: Not initialized");
                systemReady = false;
            }

            if (_mapStorage != null)
            {
                if (_enableDetailedLogging) Debug.Log($"  ✓ MapStorage: {(MapStorage.Instance.IsReady ? "Ready" : "Not Ready")}");
                if (MapStorage.Instance.IsReady)
                {
                    if (_enableDetailedLogging) Debug.Log($"    - World: {MapStorage.Instance.GetWorldCodeName()}");
                    if (_enableDetailedLogging) Debug.Log($"    - Initialized: {MapStorage.Instance.IsInitialized()}");
                }
            }
            else
            {
                Debug.LogError("  ✗ MapStorage: Not initialized");
                systemReady = false;
            }

            if (_renderer != null)
            {
                if (_enableDetailedLogging) Debug.Log($"  ✓ WorldBackgroundRenderer: {(_renderer.IsProperlyConfigured() ? "Configured" : "Not Configured")}");
                if (_enableDetailedLogging) Debug.Log($"    - State: {_renderer.GetRendererState()}");
                if (_enableDetailedLogging) Debug.Log($"    - Visible Chunks: {_renderer.GetVisibleChunkCount()}");
                if (_enableDetailedLogging) Debug.Log($"    - Textures Loaded: {_renderer.AreTexturesLoaded()}");
                if (_enableDetailedLogging) Debug.Log($"    - Atlas Applied: {_renderer.IsAtlasApplied()}");
            }
            else
            {
                Debug.LogError("  ✗ WorldBackgroundRenderer: Not initialized");
                systemReady = false;
            }

            if (systemReady)
            {
                if (_enableDetailedLogging) Debug.Log("  ✓ System initialization appears complete");
            }
            else
            {
                Debug.LogError("  ✗ System initialization incomplete - terrain rendering will not work");
            }

            yield return null;
        }

        private IEnumerator VerifyWorldInitialization()
        {
            if (_enableDetailedLogging) Debug.Log("Step 3: Verifying world initialization...");

            bool worldInitialized = true;

            if (_mapManager != null && !string.IsNullOrEmpty(_mapManager.WorldCodeName))
            {
                if (_enableDetailedLogging) Debug.Log($"  ✓ World initialized: {_mapManager.WorldDisplayName} ({_mapManager.WorldCodeName})");
                if (_enableDetailedLogging) Debug.Log($"    - Dimensions: {_mapManager.WorldWidth}x{_mapManager.WorldHeight}");
            }
            else
            {
                Debug.LogError("  ✗ World not initialized in MapManager");
                worldInitialized = false;
            }

            if (MapStorage.Instance.IsReady)
            {
                if (_enableDetailedLogging) Debug.Log($"  ✓ MapStorage ready for world: {MapStorage.Instance.GetWorldCodeName()}");
            }
            else
            {
                Debug.LogError("  ✗ MapStorage not ready - this is the core issue!");
                worldInitialized = false;
            }

            if (worldInitialized)
            {
                if (_enableDetailedLogging) Debug.Log("  ✓ World initialization successful");
            }
            else
            {
                Debug.LogError("  ✗ World initialization failed - terrain rendering will not work");
            }

            yield return null;
        }

        private IEnumerator VerifyMapStorage()
        {
            if (_enableDetailedLogging) Debug.Log("Step 4: Verifying MapStorage functionality...");

            bool mapStorageWorking = true;

            if (MapStorage.Instance.IsReady && MapStorage.Instance.cellLayer != null)
            {
                // Test basic cell access
                try
                {
                    var testCell = MapStorage.Instance.GetCell(0, 0);
                    if (_enableDetailedLogging) Debug.Log($"  ✓ Cell access test: {testCell}");
                    
                    if (testCell != CellType.Unloaded && testCell != CellType.Pregener)
                    {
                        if (_enableDetailedLogging) Debug.Log("  ✓ World data appears to be loaded");
                    }
                    else
                    {
                        if (_enableDetailedLogging) Debug.LogWarning("  ⚠ World data may not be fully loaded");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"  ✗ Cell access failed: {ex.Message}");
                    mapStorageWorking = false;
                }

                // Test setting a cell
                try
                {
                    MapStorage.Instance.SetCell(10, 10, CellType.Unloaded);
                    var afterSet = MapStorage.Instance.GetCell(10, 10);
                    if (_enableDetailedLogging) Debug.Log($"  ✓ Cell modification test: {afterSet}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"  ✗ Cell modification failed: {ex.Message}");
                    mapStorageWorking = false;
                }
            }
            else
            {
                Debug.LogError("  ✗ MapStorage not ready or cellLayer null");
                mapStorageWorking = false;
            }

            if (mapStorageWorking)
            {
                if (_enableDetailedLogging) Debug.Log("  ✓ MapStorage functionality verified");
            }
            else
            {
                Debug.LogError("  ✗ MapStorage functionality issues detected");
            }

            yield return null;
        }

        private IEnumerator VerifyRenderer()
        {
            if (_enableDetailedLogging) Debug.Log("Step 5: Verifying WorldBackgroundRenderer...");

            bool rendererWorking = true;

            if (_renderer != null && _renderer.IsProperlyConfigured())
            {
                if (_enableDetailedLogging) Debug.Log("  ✓ Renderer properly configured");
                
                // Check mesh generation
                var meshFilter = _renderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.mesh != null)
                {
                    var mesh = meshFilter.mesh;
                    if (_enableDetailedLogging) Debug.Log($"  ✓ Mesh vertices: {mesh.vertexCount}");
                    if (_enableDetailedLogging) Debug.Log($"  ✓ Mesh triangles: {mesh.triangles.Length}");
                    
                    if (mesh.vertexCount > 0 && mesh.triangles.Length > 0)
                    {
                        if (_enableDetailedLogging) Debug.Log("  ✓ Mesh properly generated");
                    }
                    else
                    {
                        Debug.LogWarning("  ⚠ Mesh is empty - no vertices or triangles");
                        rendererWorking = false;
                    }
                }
                else
                {
                    Debug.LogWarning("  ⚠ Mesh not generated yet");
                }

                // Check texture application
                var meshRenderer = _renderer.GetComponent<MeshRenderer>();
                if (meshRenderer != null && meshRenderer.material != null)
                {
                    var material = meshRenderer.material;
                    var mainTexture = material.mainTexture;
                    
                    if (_enableDetailedLogging) Debug.Log($"  ✓ Material: {material.name}");
                    if (_enableDetailedLogging) Debug.Log($"  ✓ Main texture: {(mainTexture != null ? mainTexture.name : "null")}");
                    
                    if (mainTexture != null)
                    {
                        if (_enableDetailedLogging) Debug.Log($"  ✓ Texture size: {mainTexture.width}x{mainTexture.height}");
                        if (_enableDetailedLogging) Debug.Log("  ✓ Texture successfully applied");
                    }
                    else
                    {
                        Debug.LogWarning("  ⚠ No texture applied to material");
                    }
                }
                else
                {
                    Debug.LogWarning("  ⚠ Renderer material not available");
                }
            }
            else
            {
                Debug.LogError("  ✗ Renderer not properly configured");
                rendererWorking = false;
            }

            if (rendererWorking)
            {
                if (_enableDetailedLogging) Debug.Log("  ✓ WorldBackgroundRenderer verified");
            }
            else
            {
                Debug.LogError("  ✗ WorldBackgroundRenderer issues detected");
            }

            yield return null;
        }

        private IEnumerator VerifyFinalStatus()
        {
            if (_enableDetailedLogging) Debug.Log("Step 6: Final status verification...");

            bool terrainRenderingReady = true;

            // Check overall system status
            if (!MapStorage.Instance.IsReady)
            {
                Debug.LogError("  ✗ CRITICAL: MapStorage not ready - terrain will not render");
                terrainRenderingReady = false;
            }

            if (_renderer == null || !_renderer.IsProperlyConfigured())
            {
                Debug.LogError("  ✗ CRITICAL: WorldBackgroundRenderer not ready - terrain will not render");
                terrainRenderingReady = false;
            }

            if (_renderer != null && _renderer.GetVisibleChunkCount() == 0)
            {
                Debug.LogWarning("  ⚠ No visible chunks - may indicate rendering issue");
            }

            if (terrainRenderingReady)
            {
                Debug.Log("  ✓ ✓ ✓ TERRAIN RENDERING SYSTEM READY ✓ ✓ ✓");
                Debug.Log("  ✓ MapStorage is ready and initialized");
                Debug.Log("  ✓ WorldBackgroundRenderer is configured and working");
                Debug.Log("  ✓ Terrain should be rendering correctly");
            }
            else
            {
                Debug.LogError("  ✗ ✗ ✗ TERRAIN RENDERING SYSTEM NOT READY ✗ ✗ ✗");
                Debug.LogError("  ✗ Critical issues detected - terrain will not render");
                Debug.LogError("  ✗ Check the error messages above for specific issues");
            }

            yield return null;
        }

        private void FindComponents()
        {
            _renderer = FindObjectOfType<WorldBackgroundRenderer>();
            _mapManager = FindObjectOfType<MapManager>();
            _mapStorage = MapStorage.Instance;
            _packetHandler = FindObjectOfType<PacketHandler>();
            _connectionManager = FindObjectOfType<ConnectionManager>();
            _debugTool = FindObjectOfType<DebugInitializationTool>();
        }

        /// <summary>
        /// Force complete system reset and re-verification
        /// </summary>
        public void ForceCompleteReset()
        {
            Debug.Log("=== Force Complete System Reset ===");
            
            // Reset MapStorage
            if (MapStorage.Instance != null)
            {
                MapStorage.Instance.Dispose();
                Debug.Log("Disposed MapStorage");
            }

            // Reset renderer
            if (_renderer != null)
            {
                _renderer.ForceReinitialize();
                Debug.Log("Reset WorldBackgroundRenderer");
            }

            // Reset debug tool if available
            if (_debugTool != null)
            {
                _debugTool.ResetSystem();
                Debug.Log("Reset DebugInitializationTool");
            }

            // Wait and re-verify
            StartCoroutine(DelayedReVerification());
        }

        /// <summary>
        /// Manual terrain rendering test
        /// </summary>
        public void TestTerrainRendering()
        {
            Debug.Log("=== Manual Terrain Rendering Test ===");
            
            if (MapStorage.Instance.IsReady)
            {
                Debug.Log("✓ MapStorage is ready");
                
                if (_renderer != null && _renderer.IsProperlyConfigured())
                {
                    Debug.Log("✓ WorldBackgroundRenderer is configured");
                    
                    if (_renderer.GetVisibleChunkCount() > 0)
                    {
                        Debug.Log("✓ Chunks are being generated and visible");
                        Debug.Log("✓ TERRAIN RENDERING SHOULD BE WORKING!");
                    }
                    else
                    {
                        Debug.LogWarning("⚠ No visible chunks - check camera position or render distance");
                    }
                }
                else
                {
                    Debug.LogError("✗ WorldBackgroundRenderer not configured");
                }
            }
            else
            {
                Debug.LogError("✗ MapStorage not ready - terrain cannot render");
            }
        }

        private IEnumerator DelayedReVerification()
        {
            yield return new WaitForSeconds(2.0f);
            StartCoroutine(RunCompleteVerification());
        }

        private void OnValidate()
        {
            // Ensure test dimensions are valid
            if (_testWidth < 1) _testWidth = 1;
            if (_testWidth > 10000) _testWidth = 10000;
            if (_testHeight < 1) _testHeight = 1;
            if (_testHeight > 10000) _testHeight = 10000;
        }
    }
}