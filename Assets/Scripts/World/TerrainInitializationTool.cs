using System.Collections;
using UnityEngine;
using Fodinae.Assets.Scripts.Game.Managers;
using Fodinae.Assets.Scripts.World;
using MinesServer.Data;
using MinesServer.Networking.Server.Packets.Connection;

namespace Fodinae.Assets.Scripts.World
{
    /// <summary>
    /// Manual initialization tool for terrain rendering system.
    /// Add this component to any GameObject to get manual control over terrain initialization.
    /// </summary>
    [ExecuteAlways]
    public class TerrainInitializationTool : MonoBehaviour
    {
        [Header("Manual Initialization")]
        [Tooltip("Enable manual initialization controls")]
        [SerializeField] private bool _enableManualControls = true;
        
        [Header("Test World Configuration")]
        [Tooltip("Test world name")]
        [SerializeField] private string _testWorldName = "test_world";
        [Tooltip("Test world width")]
        [SerializeField] private int _testWorldWidth = 64;
        [Tooltip("Test world height")]
        [SerializeField] private int _testWorldHeight = 64;

        [Header("Debug Information")]
        [Tooltip("Show detailed debug information")]
        [SerializeField] private bool _showDebugInfo = true;

        [Header("Manual Controls")]
        [Tooltip("Force MapStorage initialization")]
        [SerializeField] private bool _forceMapStorageInit = false;
        [Tooltip("Force WorldBackgroundRenderer initialization")]
        [SerializeField] private bool _forceRendererInit = false;
        [Tooltip("Create test world")]
        [SerializeField] private bool _createTestWorld = false;
        [Tooltip("Emergency recovery")]
        [SerializeField] private bool _emergencyRecovery = false;
        [Tooltip("Reset system")]
        [SerializeField] private bool _resetSystem = false;

        private WorldBackgroundRenderer _renderer;
        private TerrainSystemTester _tester;
        private TerrainInitializationTest _initTest;
        private TerrainRenderingTest _renderingTest;
        private TerrainFixVerification _verification;

        void Start()
        {
            FindComponents();
        }

        void Update()
        {
            // Handle manual controls
            if (_enableManualControls)
            {
                HandleManualControls();
            }
        }

        void OnValidate()
        {
            // Ensure test world dimensions are valid
            if (_testWorldWidth < 1) _testWorldWidth = 1;
            if (_testWorldWidth > 10000) _testWorldWidth = 10000;
            if (_testWorldHeight < 1) _testWorldHeight = 1;
            if (_testWorldHeight > 10000) _testWorldHeight = 10000;
        }

        private void FindComponents()
        {
            _renderer = FindObjectOfType<WorldBackgroundRenderer>();
            _tester = FindObjectOfType<TerrainSystemTester>();
            _initTest = FindObjectOfType<TerrainInitializationTest>();
            _renderingTest = FindObjectOfType<TerrainRenderingTest>();
            _verification = FindObjectOfType<TerrainFixVerification>();
        }

        private void HandleManualControls()
        {
            // Force MapStorage initialization
            if (_forceMapStorageInit)
            {
                _forceMapStorageInit = false;
                ForceMapStorageInitialization();
            }

            // Force WorldBackgroundRenderer initialization
            if (_forceRendererInit)
            {
                _forceRendererInit = false;
                ForceRendererInitialization();
            }

            // Create test world
            if (_createTestWorld)
            {
                _createTestWorld = false;
                CreateTestWorld();
            }

            // Emergency recovery
            if (_emergencyRecovery)
            {
                _emergencyRecovery = false;
                EmergencyRecovery();
            }

            // Reset system
            if (_resetSystem)
            {
                _resetSystem = false;
                ResetSystem();
            }
        }

        /// <summary>
        /// Force MapStorage initialization with current MapManager data
        /// </summary>
        public void ForceMapStorageInitialization()
        {
            Debug.Log("=== Manual MapStorage Initialization ===");
            
            if (MapManager.Instance == null)
            {
                Debug.LogError("MapManager not found - cannot initialize MapStorage");
                return;
            }

            if (string.IsNullOrEmpty(MapManager.Instance.WorldCodeName))
            {
                Debug.LogError("MapManager has no world name - cannot initialize MapStorage");
                return;
            }

            if (MapManager.Instance.WorldWidth <= 0 || MapManager.Instance.WorldHeight <= 0)
            {
                Debug.LogError("MapManager has invalid world dimensions - cannot initialize MapStorage");
                return;
            }

            Debug.Log($"Initializing MapStorage with world '{MapManager.Instance.WorldCodeName}' dimensions {MapManager.Instance.WorldWidth}x{MapManager.Instance.WorldHeight}");
            
            try
            {
                MapStorage.Instance.InitWorld(
                    MapManager.Instance.WorldCodeName,
                    MapManager.Instance.WorldWidth,
                    MapManager.Instance.WorldHeight
                );

                if (MapStorage.Instance.IsReady)
                {
                    Debug.Log("✓ MapStorage initialization successful!");
                }
                else
                {
                    Debug.LogError("✗ MapStorage initialization failed - check logs for details");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"✗ MapStorage initialization threw exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Force WorldBackgroundRenderer initialization
        /// </summary>
        public void ForceRendererInitialization()
        {
            Debug.Log("=== Manual Renderer Initialization ===");
            
            if (_renderer == null)
            {
                Debug.LogError("WorldBackgroundRenderer not found - cannot force initialization");
                return;
            }

            _renderer.ForceInitialization();
            Debug.Log("Renderer initialization triggered");
        }

        /// <summary>
        /// Create a test world for debugging
        /// </summary>
        public void CreateTestWorld()
        {
            Debug.Log("=== Creating Test World ===");
            Debug.Log($"Test world: {_testWorldName}, dimensions: {_testWorldWidth}x{_testWorldHeight}");
            
            // Dispose existing MapStorage
            if (MapStorage.Instance != null)
            {
                MapStorage.Instance.Dispose();
                Debug.Log("Disposed existing MapStorage");
            }

            try
            {
                MapStorage.Instance.InitWorld(_testWorldName, _testWorldWidth, _testWorldHeight);
                
                if (MapStorage.Instance.IsReady)
                {
                    Debug.Log("✓ Test world created successfully!");
                    
                    // Try to initialize renderer if available
                    if (_renderer != null)
                    {
                        _renderer.ForceInitialization();
                        Debug.Log("Triggered renderer initialization for test world");
                    }
                }
                else
                {
                    Debug.LogError("✗ Test world creation failed - check logs for details");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"✗ Test world creation threw exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Emergency recovery mechanism
        /// </summary>
        public void EmergencyRecovery()
        {
            Debug.Log("=== Emergency Recovery ===");
            
            // Dispose MapStorage
            if (MapStorage.Instance != null)
            {
                MapStorage.Instance.Dispose();
                Debug.Log("Disposed MapStorage");
            }

            // Create minimal test world
            try
            {
                Debug.Log("Creating minimal test world for recovery");
                MapStorage.Instance.InitWorld("emergency_recovery", 32, 32);
                
                if (MapStorage.Instance.IsReady)
                {
                    Debug.Log("✓ Emergency recovery successful!");
                    
                    // Try to initialize renderer
                    if (_renderer != null)
                    {
                        _renderer.ForceInitialization();
                        Debug.Log("Triggered renderer initialization for recovery");
                    }
                }
                else
                {
                    Debug.LogError("✗ Emergency recovery failed - MapStorage still not ready");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"✗ Emergency recovery threw exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset the entire terrain system
        /// </summary>
        public void ResetSystem()
        {
            Debug.Log("=== System Reset ===");
            
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

            // Reset test components if available
            if (_initTest != null)
            {
                _initTest.ForceSystemReinitialize();
                Debug.Log("Reset TerrainInitializationTest");
            }

            if (_renderingTest != null)
            {
                _renderingTest.ForceSystemReset();
                Debug.Log("Reset TerrainRenderingTest");
            }

            Debug.Log("System reset completed");
        }

        /// <summary>
        /// Run comprehensive system test
        /// </summary>
        public void RunComprehensiveTest()
        {
            Debug.Log("=== Comprehensive System Test ===");
            
            if (_tester != null)
            {
                StartCoroutine(_tester.RunFullSystemTest());
            }
            else if (_renderingTest != null)
            {
                StartCoroutine(_renderingTest.RunComprehensiveTest());
            }
            else
            {
                Debug.LogWarning("No test components found - running manual checks");
                ManualSystemCheck();
            }
        }

        /// <summary>
        /// Manual system status check
        /// </summary>
        public void ManualSystemCheck()
        {
            Debug.Log("=== Manual System Check ===");
            Debug.Log($"MapStorage Ready: {MapStorage.Instance?.IsReady ?? false}");
            Debug.Log($"MapManager Available: {MapManager.Instance != null}");
            Debug.Log($"Renderer Configured: {_renderer?.IsProperlyConfigured() ?? false}");
            Debug.Log($"Visible Chunks: {_renderer?.GetVisibleChunkCount() ?? 0}");
            Debug.Log($"Textures Loaded: {_renderer?.AreTexturesLoaded() ?? false}");
            Debug.Log($"Atlas Applied: {_renderer?.IsAtlasApplied() ?? false}");
            Debug.Log("=== Check Complete ===");
        }

        /// <summary>
        /// Debug current system status
        /// </summary>
        public void DebugStatus()
        {
            if (_showDebugInfo)
            {
                Debug.Log("=== Terrain Initialization Tool Debug ===");
                Debug.Log($"Manual Controls Enabled: {_enableManualControls}");
                Debug.Log($"Test World: {_testWorldName} ({_testWorldWidth}x{_testWorldHeight})");
                Debug.Log($"MapStorage Ready: {MapStorage.Instance?.IsReady ?? false}");
                Debug.Log($"MapManager Available: {MapManager.Instance != null}");
                Debug.Log($"Renderer Found: {_renderer != null}");
                Debug.Log($"Renderer State: {_renderer?.GetRendererState() ?? "N/A"}");
                Debug.Log($"Visible Chunks: {_renderer?.GetVisibleChunkCount() ?? 0}");
                Debug.Log("========================================");
            }
        }

        private void OnEnable()
        {
            FindComponents();
        }

        private void OnDisable()
        {
            // Clean up any ongoing operations
            StopAllCoroutines();
        }

        private void OnDrawGizmos()
        {
            // Draw debug gizmos if renderer is available and debug mode is enabled
            if (_renderer != null && _showDebugInfo)
            {
                _renderer.OnDrawGizmosSelected();
            }
        }
    }
}