using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fodinae.Assets.Scripts.Game.Managers;
using Fodinae.Assets.Scripts.World;
using MinesServer.Data;
using MinesServer.Networking.Connection;
using MinesServer.Networking.Connection.Client;
using MinesServer.Networking.Server.Packets.Connection;
using Fodinae.Assets.Scripts.Networking.Connection;
using MinesServer.Networking.Shared;

namespace Fodinae.Assets.Scripts.World
{
    /// <summary>
    /// Comprehensive test component for the terrain rendering system.
    /// Add this to a GameObject in your scene to test and verify the terrain rendering fixes.
    /// </summary>
    [ExecuteAlways]
    public class TerrainSystemTester : MonoBehaviour
    {
        [Header("Test Configuration")]
        [Tooltip("Enable automatic testing on start")]
        [SerializeField] private bool _autoTestOnStart = true;
        [Tooltip("Test interval in seconds")]
        [SerializeField] private float _testInterval = 5f;
        [Tooltip("Enable detailed logging")]
        [SerializeField] private bool _enableDetailedLogging = true;

        [Header("Test Actions")]
        [Tooltip("Enable automatic recovery attempts")]
        [SerializeField] private bool _enableAutoRecovery = true;
        [Tooltip("Enable emergency recovery")]
        [SerializeField] private bool _enableEmergencyRecovery = true;

        private WorldBackgroundRenderer _renderer;
        private TerrainRenderingTest _renderingTest;
        private TerrainInitializationTest _initTest;
        private TerrainFixVerification _verification;
        private float _lastTestTime = 0f;
        private bool _isTesting = false;
        private int _testCount = 0;

        void Start()
        {
            _renderer = FindObjectOfType<WorldBackgroundRenderer>();
            _renderingTest = FindObjectOfType<TerrainRenderingTest>();
            _initTest = FindObjectOfType<TerrainInitializationTest>();
            _verification = FindObjectOfType<TerrainFixVerification>();
            
            if (_autoTestOnStart)
            {
                StartCoroutine(RunFullSystemTest());
            }
        }

        void Update()
        {
            // Run tests periodically if auto-testing is enabled
            if (_autoTestOnStart && Time.time - _lastTestTime >= _testInterval)
            {
                if (!_isTesting)
                {
                    StartCoroutine(RunFullSystemTest());
                }
            }
        }

        /// <summary>
        /// Run a comprehensive test of the entire terrain system
        /// </summary>
        public IEnumerator RunFullSystemTest()
        {
            _isTesting = true;
            _lastTestTime = Time.time;
            _testCount++;

            Debug.Log($"=== Terrain System Test #{_testCount} Started ===");

            // Test 1: Component Discovery
            Debug.Log("Test 1: Component Discovery");
            yield return TestComponentDiscovery();

            // Test 2: System Initialization Status
            Debug.Log("Test 2: System Initialization Status");
            yield return TestInitializationStatus();

            // Test 3: MapStorage Health Check
            Debug.Log("Test 3: MapStorage Health Check");
            yield return TestMapStorageHealth();

            // Test 4: WorldBackgroundRenderer Health Check
            Debug.Log("Test 4: WorldBackgroundRenderer Health Check");
            yield return TestRendererHealth();

            // Test 5: Connection and Data Flow
            Debug.Log("Test 5: Connection and Data Flow");
            yield return TestConnectionHealth();

            // Test 6: Recovery Mechanisms
            if (_enableAutoRecovery)
            {
                Debug.Log("Test 6: Recovery Mechanisms");
                yield return TestRecoveryMechanisms();
            }

            // Test 7: Final Verification
            Debug.Log("Test 7: Final Verification");
            yield return TestFinalVerification();

            Debug.Log($"=== Terrain System Test #{_testCount} Completed ===");
            _isTesting = false;
        }

        private IEnumerator TestComponentDiscovery()
        {
            Debug.Log("  Discovering terrain system components...");
            
            bool allComponentsFound = true;
            
            if (_renderer != null)
            {
                Debug.Log("  ✓ WorldBackgroundRenderer: Found");
            }
            else
            {
                Debug.LogError("  ✗ WorldBackgroundRenderer: Not found");
                allComponentsFound = false;
            }

            if (_renderingTest != null)
            {
                Debug.Log("  ✓ TerrainRenderingTest: Found");
            }
            else
            {
                Debug.LogWarning("  ⚠ TerrainRenderingTest: Not found (testing tools unavailable)");
            }

            if (_initTest != null)
            {
                Debug.Log("  ✓ TerrainInitializationTest: Found");
            }
            else
            {
                Debug.LogWarning("  ⚠ TerrainInitializationTest: Not found (initialization tools unavailable)");
            }

            if (_verification != null)
            {
                Debug.Log("  ✓ TerrainFixVerification: Found");
            }
            else
            {
                Debug.LogWarning("  ⚠ TerrainFixVerification: Not found (verification tools unavailable)");
            }

            if (MapManager.Instance != null)
            {
                Debug.Log("  ✓ MapManager: Found");
            }
            else
            {
                Debug.LogError("  ✗ MapManager: Not found");
                allComponentsFound = false;
            }

            if (MapStorage.Instance != null)
            {
                Debug.Log("  ✓ MapStorage: Found");
            }
            else
            {
                Debug.LogError("  ✗ MapStorage: Not found");
                allComponentsFound = false;
            }

            if (ConnectionManager.Instance != null)
            {
                Debug.Log("  ✓ ConnectionManager: Found");
            }
            else
            {
                Debug.LogWarning("  ⚠ ConnectionManager: Not found (networking unavailable)");
            }

            if (allComponentsFound)
            {
                Debug.Log("  ✓ All critical components found - system appears complete");
            }
            else
            {
                Debug.LogWarning("  ⚠ Some critical components missing - system may not function properly");
            }

            yield return null;
        }

        private IEnumerator TestInitializationStatus()
        {
            Debug.Log("  Checking initialization status...");
            
            if (MapManager.Instance != null)
            {
                Debug.Log($"  ✓ MapManager: Initialized");
                Debug.Log($"    - World: {MapManager.Instance.WorldDisplayName}");
                Debug.Log($"    - Dimensions: {MapManager.Instance.WorldWidth}x{MapManager.Instance.WorldHeight}");
            }
            else
            {
                Debug.LogError("  ✗ MapManager: Not initialized");
            }

            if (MapStorage.Instance != null)
            {
                Debug.Log($"  ✓ MapStorage: {(MapStorage.Instance.IsReady ? "Ready" : "Not Ready")}");
                if (MapStorage.Instance.IsReady)
                {
                    Debug.Log($"    - World: {MapStorage.Instance.GetWorldCodeName()}");
                    Debug.Log($"    - Initialized: {MapStorage.Instance.IsInitialized()}");
                }
            }
            else
            {
                Debug.LogError("  ✗ MapStorage: Not initialized");
            }

            if (_renderer != null)
            {
                Debug.Log($"  ✓ WorldBackgroundRenderer: {( _renderer.IsProperlyConfigured() ? "Configured" : "Not Configured" )}");
                Debug.Log($"    - State: {_renderer.GetRendererState()}");
                Debug.Log($"    - Visible Chunks: {_renderer.GetVisibleChunkCount()}");
                Debug.Log($"    - Textures Loaded: {_renderer.AreTexturesLoaded()}");
                Debug.Log($"    - Atlas Applied: {_renderer.IsAtlasApplied()}");
            }
            else
            {
                Debug.LogError("  ✗ WorldBackgroundRenderer: Not initialized");
            }

            yield return null;
        }

        private IEnumerator TestMapStorageHealth()
        {
            Debug.Log("  Testing MapStorage health...");
            
            if (MapStorage.Instance == null || !MapStorage.Instance.IsReady)
            {
                Debug.LogError("  ✗ MapStorage not ready for testing");
                yield break;
            }

            // Test basic cell access
            try
            {
                var testCell = MapStorage.Instance.GetCell(0, 0);
                Debug.Log($"  ✓ Cell access test: {testCell}");
                
                if (testCell != CellType.Unloaded && testCell != CellType.Pregener)
                {
                    Debug.Log("  ✓ World data appears to be loaded");
                }
                else
                {
                    Debug.LogWarning("  ⚠ World data may not be fully loaded");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"  ✗ Cell access failed: {ex.Message}");
            }

            // Test setting a cell
            try
            {
                MapStorage.Instance.SetCell(10, 10, CellType.Unloaded);
                var afterSet = MapStorage.Instance.GetCell(10, 10);
                Debug.Log($"  ✓ Cell modification test: {afterSet}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"  ✗ Cell modification failed: {ex.Message}");
            }

            yield return null;
        }

        private IEnumerator TestRendererHealth()
        {
            Debug.Log("  Testing WorldBackgroundRenderer health...");
            
            if (_renderer == null)
            {
                Debug.LogError("  ✗ Renderer not available for testing");
                yield break;
            }

            // Test renderer configuration
            bool isConfigured = _renderer.IsProperlyConfigured();
            Debug.Log($"  ✓ Renderer configuration: {isConfigured}");
            
            if (isConfigured)
            {
                Debug.Log("  ✓ Renderer appears to be properly configured");
            }
            else
            {
                Debug.LogWarning("  ⚠ Renderer may not be properly configured");
            }

            // Test visible chunks
            int visibleChunks = _renderer.GetVisibleChunkCount();
            Debug.Log($"  ✓ Visible chunks: {visibleChunks}");
            
            if (visibleChunks > 0)
            {
                Debug.Log("  ✓ Chunks are being generated and visible");
            }
            else
            {
                Debug.LogWarning("  ⚠ No chunks visible - may indicate rendering issue");
            }

            // Test mesh generation
            var meshFilter = _renderer.GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.mesh != null)
            {
                var mesh = meshFilter.mesh;
                Debug.Log($"  ✓ Mesh vertices: {mesh.vertexCount}");
                Debug.Log($"  ✓ Mesh triangles: {mesh.triangles.Length}");
                
                if (mesh.vertexCount > 0 && mesh.triangles.Length > 0)
                {
                    Debug.Log("  ✓ Mesh appears to be properly generated");
                }
                else
                {
                    Debug.LogWarning("  ⚠ Mesh is empty - no vertices or triangles");
                }
            }
            else
            {
                Debug.LogWarning("  ⚠ Mesh not generated yet");
            }

            // Test texture application
            var meshRenderer = _renderer.GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.material != null)
            {
                var material = meshRenderer.material;
                var mainTexture = material.mainTexture;
                
                Debug.Log($"  ✓ Material: {material.name}");
                Debug.Log($"  ✓ Main texture: {(mainTexture != null ? mainTexture.name : "null")}");
                
                if (mainTexture != null)
                {
                    Debug.Log($"  ✓ Texture size: {mainTexture.width}x{mainTexture.height}");
                    Debug.Log("  ✓ Texture successfully applied to material");
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

            yield return null;
        }

        private IEnumerator TestConnectionHealth()
        {
            Debug.Log("  Testing connection and data flow...");
            
            if (ConnectionManager.Instance != null)
            {
                Debug.Log($"  ✓ Connection status: {ConnectionManager.Instance.Connection?.ConnectionStatus}");
                
                if (ConnectionManager.Instance.Connection?.ConnectionStatus == ConnectionStatus.Connected)
                {
                    Debug.Log("  ✓ Connection is active");
                }
                else
                {
                    Debug.LogWarning("  ⚠ Connection may not be active");
                }
            }
            else
            {
                Debug.LogWarning("  ⚠ ConnectionManager not available");
            }

            // Test if we have world data from the connection
            if (MapManager.Instance != null && MapManager.Instance.WorldWidth > 0 && MapManager.Instance.WorldHeight > 0)
            {
                Debug.Log("  ✓ World data received from connection");
                Debug.Log($"    - World dimensions: {MapManager.Instance.WorldWidth}x{MapManager.Instance.WorldHeight}");
                Debug.Log($"    - World name: {MapManager.Instance.WorldDisplayName}");
            }
            else
            {
                Debug.LogWarning("  ⚠ No world data received from connection");
            }

            yield return null;
        }

        private IEnumerator TestRecoveryMechanisms()
        {
            Debug.Log("  Testing recovery mechanisms...");
            
            bool recoveryNeeded = false;
            string recoveryReason = "";

            // Check if recovery is needed
            if (MapStorage.Instance == null || !MapStorage.Instance.IsReady)
            {
                recoveryNeeded = true;
                recoveryReason = "MapStorage not ready";
            }
            else if (_renderer != null && !_renderer.IsProperlyConfigured())
            {
                recoveryNeeded = true;
                recoveryReason = "Renderer not properly configured";
            }
            else if (_renderer != null && _renderer.GetVisibleChunkCount() == 0)
            {
                recoveryNeeded = true;
                recoveryReason = "No visible chunks";
            }

            if (recoveryNeeded)
            {
                Debug.LogWarning($"  ⚠ Recovery needed: {recoveryReason}");
                
                // Attempt recovery using available tools
                if (_renderingTest != null)
                {
                    Debug.Log("  Attempting recovery using TerrainRenderingTest...");
                    _renderingTest.ForceInitializationWithRecovery();
                    yield return new WaitForSeconds(2.0f);
                }
                else if (_initTest != null)
                {
                    Debug.Log("  Attempting recovery using TerrainInitializationTest...");
                    _initTest.ForceSystemReinitialize();
                    yield return new WaitForSeconds(2.0f);
                }
                else if (_verification != null)
                {
                    Debug.Log("  Attempting recovery using TerrainFixVerification...");
                    _verification.RunComprehensiveVerification();
                    yield return new WaitForSeconds(2.0f);
                }
                else
                {
                    Debug.Log("  Attempting manual recovery...");
                    if (_renderer != null)
                    {
                        _renderer.ForceReinitialize();
                        yield return new WaitForSeconds(2.0f);
                    }
                }
                
                // Check if recovery was successful
                if (_renderer != null && _renderer.IsProperlyConfigured() && _renderer.GetVisibleChunkCount() > 0)
                {
                    Debug.Log("  ✓ Recovery successful");
                }
                else
                {
                    Debug.LogWarning("  ⚠ Recovery may have failed - check logs for details");
                    
                    // Try emergency recovery if enabled
                    if (_enableEmergencyRecovery)
                    {
                        Debug.Log("  Attempting emergency recovery...");
                        yield return RunEmergencyRecovery();
                    }
                }
            }
            else
            {
                Debug.Log("  ✓ No recovery needed - system appears healthy");
            }

            yield return null;
        }

        private IEnumerator RunEmergencyRecovery()
        {
            Debug.Log("  === Emergency Recovery ===");
            
            // Wait a bit for any pending operations
            yield return new WaitForSeconds(2.0f);
            
            // Force MapStorage disposal and re-creation
            if (MapStorage.Instance != null)
            {
                MapStorage.Instance.Dispose();
                Debug.Log("  MapStorage disposed for emergency recovery");
            }
            
            // Try to create a minimal test world if we have MapManager
            if (MapManager.Instance != null)
            {
                Debug.Log("  Creating minimal test world for emergency recovery");
                MapStorage.Instance.InitWorld("emergency_test", 32, 32);
                
                yield return new WaitForSeconds(1.0f);
                
                if (MapStorage.Instance.IsReady)
                {
                    Debug.Log("  ✓ Emergency recovery successful - minimal world created");
                    if (_renderer != null)
                    {
                        _renderer.ForceInitialization();
                    }
                }
                else
                {
                    Debug.LogError("  ✗ Emergency recovery failed - system may need restart");
                }
            }
            else
            {
                Debug.LogWarning("  ⚠ MapManager not available for emergency recovery");
            }
        }

        private IEnumerator TestFinalVerification()
        {
            Debug.Log("  Running final verification...");
            
            // Run verification using available tools
            if (_verification != null)
            {
                _verification.GetVerificationSummary();
                yield return new WaitForSeconds(1.0f);
            }
            else if (_renderingTest != null)
            {
                _renderingTest.QuickDiagnostic();
                yield return new WaitForSeconds(1.0f);
            }
            else if (_initTest != null)
            {
                _initTest.GetDetailedStatus();
                yield return new WaitForSeconds(1.0f);
            }
            else
            {
                // Manual verification
                Debug.Log("=== Manual Verification ===");
                Debug.Log($"MapStorage Ready: {MapStorage.Instance?.IsReady ?? false}");
                Debug.Log($"MapManager Available: {MapManager.Instance != null}");
                Debug.Log($"Renderer Configured: {_renderer?.IsProperlyConfigured() ?? false}");
                Debug.Log($"Visible Chunks: {_renderer?.GetVisibleChunkCount() ?? 0}");
                Debug.Log("=== Verification Complete ===");
            }

            yield return null;
        }

        /// <summary>
        /// Run a quick system status check
        /// </summary>
        public void QuickSystemCheck()
        {
            Debug.Log("=== Quick Terrain System Check ===");
            Debug.Log($"MapStorage Ready: {MapStorage.Instance?.IsReady ?? false}");
            Debug.Log($"MapManager Available: {MapManager.Instance != null}");
            Debug.Log($"Renderer Configured: {_renderer?.IsProperlyConfigured() ?? false}");
            Debug.Log($"Visible Chunks: {_renderer?.GetVisibleChunkCount() ?? 0}");
            Debug.Log($"Textures Loaded: {_renderer?.AreTexturesLoaded() ?? false}");
            Debug.Log($"Atlas Applied: {_renderer?.IsAtlasApplied() ?? false}");
            Debug.Log("================================");
        }

        /// <summary>
        /// Force a complete system reset and re-initialization
        /// </summary>
        public void ForceSystemReset()
        {
            Debug.Log("=== Forcing System Reset ===");
            
            // Reset MapStorage
            if (MapStorage.Instance != null)
            {
                MapStorage.Instance.Dispose();
                Debug.Log("MapStorage disposed");
            }

            // Reset renderer
            if (_renderer != null)
            {
                _renderer.ForceReinitialize();
                Debug.Log("Renderer reinitialized");
            }

            // Wait and test again
            StartCoroutine(DelayedSystemCheck());
        }

        /// <summary>
        /// Force initialization with detailed error reporting and recovery
        /// </summary>
        public void ForceInitializationWithRecovery()
        {
            Debug.Log("=== Force Initialization with Recovery ===");
            
            // Check MapManager state
            if (MapManager.Instance == null)
            {
                Debug.LogError("MapManager not found - cannot force initialization");
                return;
            }

            Debug.Log($"MapManager state: World={MapManager.Instance.WorldDisplayName}, Width={MapManager.Instance.WorldWidth}, Height={MapManager.Instance.WorldHeight}");
            
            // Check if MapStorage is ready
            if (MapStorage.Instance != null && MapStorage.Instance.IsReady)
            {
                Debug.Log("MapStorage is already ready, forcing renderer initialization");
                _renderer?.ForceInitialization();
                return;
            }

            // Try to re-initialize MapStorage if we have world data
            if (MapManager.Instance.WorldWidth > 0 && MapManager.Instance.WorldHeight > 0)
            {
                Debug.Log($"Attempting to re-initialize MapStorage with world dimensions {MapManager.Instance.WorldWidth}x{MapManager.Instance.WorldHeight}");
                MapStorage.Instance.InitWorld(MapManager.Instance.WorldCodeName, MapManager.Instance.WorldWidth, MapManager.Instance.WorldHeight);
                
                // Wait a moment then check if it worked
                StartCoroutine(CheckInitializationResult());
            }
            else
            {
                Debug.LogWarning("No world data available to re-initialize MapStorage");
            }
        }

        /// <summary>
        /// Check initialization result and provide detailed feedback
        /// </summary>
        private IEnumerator CheckInitializationResult()
        {
            yield return new WaitForSeconds(1.0f);
            
            Debug.Log("=== Checking Initialization Result ===");
            Debug.Log($"MapStorage ready: {MapStorage.Instance?.IsReady ?? false}");
            Debug.Log($"Renderer properly configured: {_renderer?.IsProperlyConfigured() ?? false}");
            Debug.Log($"Visible chunks: {_renderer?.GetVisibleChunkCount() ?? 0}");
            
            if (MapStorage.Instance?.IsReady ?? false)
            {
                Debug.Log("MapStorage initialization successful, forcing renderer update");
                _renderer?.ForceInitialization();
            }
            else
            {
                Debug.LogError("MapStorage initialization failed - check logs for specific errors");
                
                // Try emergency recovery
                if (_enableEmergencyRecovery)
                {
                    StartCoroutine(RunEmergencyRecovery());
                }
            }
        }

        private IEnumerator DelayedSystemCheck()
        {
            yield return new WaitForSeconds(2.0f);
            QuickSystemCheck();
        }

        private void OnValidate()
        {
            // Ensure test interval is reasonable
            if (_testInterval < 1f) _testInterval = 1f;
            if (_testInterval > 60f) _testInterval = 60f;
        }
    }
}