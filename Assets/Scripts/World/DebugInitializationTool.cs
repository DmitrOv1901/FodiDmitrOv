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
    /// Debug tool for manual world initialization and terrain rendering testing.
    /// Add this component to any GameObject to get manual control over the terrain system.
    /// </summary>
    [ExecuteAlways]
    public class DebugInitializationTool : MonoBehaviour
    {
        [Header("Debug Configuration")]
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool _enableDebugLogging = true;
        
        [Header("Manual Initialization")]
        [Tooltip("Enable manual initialization controls")]
        [SerializeField] private bool _enableManualControls = true;
        
        [Header("Test World Configuration")]
        [Tooltip("Test world name")]
        [SerializeField] private string _testWorldName = "debug_world";
        [Tooltip("Test world width")]
        [SerializeField] private int _testWorldWidth = 100;
        [Tooltip("Test world height")]
        [SerializeField] private int _testWorldHeight = 100;

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
        [Tooltip("Test packet flow")]
        [SerializeField] private bool _testPacketFlow = false;

        private WorldBackgroundRenderer _renderer;
        private TerrainSystemTester _tester;
        private TerrainInitializationTool _initTool;
        private PacketHandler _packetHandler;
        private ConnectionManager _connectionManager;
        private MapManager _mapManager;
        private MapStorage _mapStorage;

        void Start()
        {
            FindComponents();
            LogSystemStatus();
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
            _initTool = FindObjectOfType<TerrainInitializationTool>();
            _packetHandler = FindObjectOfType<PacketHandler>();
            _connectionManager = FindObjectOfType<ConnectionManager>();
            _mapManager = FindObjectOfType<MapManager>();
            _mapStorage = MapStorage.Instance;
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

            // Test packet flow
            if (_testPacketFlow)
            {
                _testPacketFlow = false;
                TestPacketFlow();
            }
        }

        /// <summary>
        /// Force MapStorage initialization with current MapManager data
        /// </summary>
        public void ForceMapStorageInitialization()
        {
            if (!_enableDebugLogging) return;
            Debug.Log("=== Debug: Force MapStorage Initialization ===");
            
            if (_mapManager == null)
            {
                Debug.LogError("Debug: MapManager not found - cannot initialize MapStorage");
                return;
            }

            if (string.IsNullOrEmpty(_mapManager.WorldCodeName))
            {
                Debug.LogError("Debug: MapManager has no world name - cannot initialize MapStorage");
                return;
            }

            if (_mapManager.WorldWidth <= 0 || _mapManager.WorldHeight <= 0)
            {
                Debug.LogError("Debug: MapManager has invalid world dimensions - cannot initialize MapStorage");
                return;
            }

            Debug.Log($"Debug: Initializing MapStorage with world '{_mapManager.WorldCodeName}' dimensions {_mapManager.WorldWidth}x{_mapManager.WorldHeight}");
            
            try
            {
                MapStorage.Instance.InitWorld(
                    _mapManager.WorldCodeName,
                    _mapManager.WorldWidth,
                    _mapManager.WorldHeight
                );

                if (MapStorage.Instance.IsReady)
                {
                    Debug.Log("Debug: ✓ MapStorage initialization successful!");
                }
                else
                {
                    Debug.LogError("Debug: ✗ MapStorage initialization failed - check logs for details");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Debug: ✗ MapStorage initialization threw exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Force WorldBackgroundRenderer initialization
        /// </summary>
        public void ForceRendererInitialization()
        {
            if (!_enableDebugLogging) return;
            Debug.Log("=== Debug: Force Renderer Initialization ===");
            
            if (_renderer == null)
            {
                Debug.LogError("Debug: WorldBackgroundRenderer not found - cannot force initialization");
                return;
            }

            _renderer.ForceInitialization();
            Debug.Log("Debug: Renderer initialization triggered");
        }

        /// <summary>
        /// Create a test world for debugging
        /// </summary>
        public void CreateTestWorld()
        {
            if (!_enableDebugLogging) return;
            Debug.Log("=== Debug: Creating Test World ===");
            Debug.Log($"Debug: Test world: {_testWorldName}, dimensions: {_testWorldWidth}x{_testWorldHeight}");
            
            // Dispose existing MapStorage
            if (MapStorage.Instance != null)
            {
                MapStorage.Instance.Dispose();
                Debug.Log("Debug: Disposed existing MapStorage");
            }

            try
            {
                MapStorage.Instance.InitWorld(_testWorldName, _testWorldWidth, _testWorldHeight);
                
                if (MapStorage.Instance.IsReady)
                {
                    Debug.Log("Debug: ✓ Test world created successfully!");
                    
                    // Try to initialize renderer if available
                    if (_renderer != null)
                    {
                        _renderer.ForceInitialization();
                        Debug.Log("Debug: Triggered renderer initialization for test world");
                    }
                }
                else
                {
                    Debug.LogError("Debug: ✗ Test world creation failed - check logs for details");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Debug: ✗ Test world creation threw exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Emergency recovery mechanism
        /// </summary>
        public void EmergencyRecovery()
        {
            if (!_enableDebugLogging) return;
            Debug.Log("=== Debug: Emergency Recovery ===");
            
            // Dispose MapStorage
            if (MapStorage.Instance != null)
            {
                MapStorage.Instance.Dispose();
                Debug.Log("Debug: Disposed MapStorage");
            }

            // Create minimal test world
            try
            {
                Debug.Log("Debug: Creating minimal test world for recovery");
                MapStorage.Instance.InitWorld("emergency_recovery", 32, 32);
                
                if (MapStorage.Instance.IsReady)
                {
                    Debug.Log("Debug: ✓ Emergency recovery successful!");
                    
                    // Try to initialize renderer
                    if (_renderer != null)
                    {
                        _renderer.ForceInitialization();
                        Debug.Log("Debug: Triggered renderer initialization for recovery");
                    }
                }
                else
                {
                    Debug.LogError("Debug: ✗ Emergency recovery failed - MapStorage still not ready");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Debug: ✗ Emergency recovery threw exception: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset the entire terrain system
        /// </summary>
        public void ResetSystem()
        {
            if (!_enableDebugLogging) return;
            Debug.Log("=== Debug: System Reset ===");
            
            // Reset MapStorage
            if (MapStorage.Instance != null)
            {
                MapStorage.Instance.Dispose();
                Debug.Log("Debug: Disposed MapStorage");
            }

            // Reset renderer
            if (_renderer != null)
            {
                _renderer.ForceReinitialize();
                Debug.Log("Debug: Reset WorldBackgroundRenderer");
            }

            // Reset test components if available
            if (_tester != null)
            {
                StartCoroutine(_tester.RunFullSystemTest());
                Debug.Log("Debug: Triggered TerrainSystemTester");
            }

            if (_initTool != null)
            {
                _initTool.ResetSystem();
                Debug.Log("Debug: Triggered TerrainInitializationTool reset");
            }

            Debug.Log("Debug: System reset completed");
        }

        /// <summary>
        /// Test the complete packet flow from DummyConnection to MapManager
        /// </summary>
        public void TestPacketFlow()
        {
            if (!_enableDebugLogging) return;
            Debug.Log("=== Debug: Testing Packet Flow ===");
            
            // Check if we have a connection
            if (_connectionManager == null)
            {
                Debug.LogError("Debug: ConnectionManager not found");
                return;
            }

            if (_connectionManager.Connection == null)
            {
                Debug.LogError("Debug: No active connection found");
                return;
            }

            // Check if we have a packet handler
            if (_packetHandler == null)
            {
                Debug.LogError("Debug: PacketHandler not found");
                return;
            }

            // Create a test WorldInitPacket
            var cellConfigs = CreateTestCellConfigurations();
            var testPacket = new WorldInitPacket(
                "debug_test",
                "Debug Test World",
                (ushort)_testWorldWidth,
                (ushort)_testWorldHeight,
                cellConfigs,
                new byte[][] {
                    new byte[] { 37, 38, 106 }
                }
            );

            Debug.Log($"Debug: Created test WorldInitPacket: {testPacket.DisplayName} ({testPacket.CodeName}) [{testPacket.Width}x{testPacket.Height}]");

            // Simulate packet processing
            try
            {
                Debug.Log("Debug: Processing WorldInitPacket through PacketHandler...");
                _packetHandler.HandleWorldInitPacket(testPacket);
                
                Debug.Log("Debug: Checking MapStorage status...");
                if (MapStorage.Instance.IsReady)
                {
                    Debug.Log("Debug: ✓ MapStorage is ready after packet processing");
                }
                else
                {
                    Debug.LogError("Debug: ✗ MapStorage is not ready after packet processing");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Debug: ✗ Packet flow test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Create test cell configurations for debugging
        /// </summary>
        private CellConfigurationPacket[] CreateTestCellConfigurations()
        {
            var configs = new CellConfigurationPacket[256];
            
            // Initialize all to default values
            for (int i = 0; i < 256; i++)
            {
                configs[i] = new CellConfigurationPacket
                {
                    Animation = CellAnimationType.None,
                    AnimationSpeed = 0,
                    Color = unchecked((int)0xFF808080), // Default gray
                    FrameOffset = 0,
                    Properties = 0
                };
            }
            
            // Configure specific cell types
            configs[(int)CellType.Empty] = new CellConfigurationPacket
            {
                Animation = CellAnimationType.None,
                AnimationSpeed = 0,
                Color = unchecked((int)0xFF808080), // Gray
                FrameOffset = 0,
                Properties = 0
            };
            
            configs[(int)CellType.Road] = new CellConfigurationPacket
            {
                Animation = CellAnimationType.None,
                AnimationSpeed = 0,
                Color = unchecked((int)0xFFCCCCCC), // Light gray
                FrameOffset = 0,
                Properties = 0
            };
            
            configs[(int)CellType.Boulder1] = new CellConfigurationPacket
            {
                Animation = CellAnimationType.None,
                AnimationSpeed = 0,
                Color = unchecked((int)0xFF000000), // Black
                FrameOffset = 0,
                Properties = 0
            };
            
            return configs;
        }

        /// <summary>
        /// Log current system status for debugging
        /// </summary>
        public void LogSystemStatus()
        {
            if (!_enableDebugLogging) return;
            
            Debug.Log("=== Debug: System Status ===");
            Debug.Log($"Debug: MapManager: {_mapManager != null}");
            Debug.Log($"Debug: MapStorage: {_mapStorage != null}");
            Debug.Log($"Debug: WorldBackgroundRenderer: {_renderer != null}");
            Debug.Log($"Debug: PacketHandler: {_packetHandler != null}");
            Debug.Log($"Debug: ConnectionManager: {_connectionManager != null}");
            Debug.Log($"Debug: TerrainSystemTester: {_tester != null}");
            Debug.Log($"Debug: TerrainInitializationTool: {_initTool != null}");
            
            if (_mapManager != null)
            {
                Debug.Log($"Debug: MapManager World: {_mapManager.WorldDisplayName} ({_mapManager.WorldCodeName}) [{_mapManager.WorldWidth}x{_mapManager.WorldHeight}]");
            }
            
            if (_mapStorage != null)
            {
                Debug.Log($"Debug: MapStorage Ready: {_mapStorage.IsReady}");
                Debug.Log($"Debug: MapStorage World: {_mapStorage.GetWorldCodeName()}");
            }
            
            if (_renderer != null)
            {
                Debug.Log($"Debug: Renderer Configured: {_renderer.IsProperlyConfigured()}");
                Debug.Log($"Debug: Renderer State: {_renderer.GetRendererState()}");
                Debug.Log($"Debug: Visible Chunks: {_renderer.GetVisibleChunkCount()}");
            }
            
            if (_packetHandler != null)
            {
                Debug.Log($"Debug: PacketHandler Stats: {_packetHandler.GetStatistics()}");
            }
            
            Debug.Log("=== Debug: Status Complete ===");
        }

        /// <summary>
        /// Run comprehensive system test
        /// </summary>
        public void RunComprehensiveTest()
        {
            if (!_enableDebugLogging) return;
            Debug.Log("=== Debug: Running Comprehensive Test ===");
            
            if (_tester != null)
            {
                StartCoroutine(_tester.RunFullSystemTest());
            }
            else if (_initTool != null)
            {
                _initTool.RunComprehensiveTest();
            }
            else
            {
                Debug.LogWarning("Debug: No test components found - running manual checks");
                ManualSystemCheck();
            }
        }

        /// <summary>
        /// Manual system status check
        /// </summary>
        public void ManualSystemCheck()
        {
            if (!_enableDebugLogging) return;
            Debug.Log("=== Debug: Manual System Check ===");
            Debug.Log($"Debug: MapStorage Ready: {MapStorage.Instance?.IsReady ?? false}");
            Debug.Log($"Debug: MapManager Available: {MapManager.Instance != null}");
            Debug.Log($"Debug: Renderer Configured: {_renderer?.IsProperlyConfigured() ?? false}");
            Debug.Log($"Debug: Visible Chunks: {_renderer?.GetVisibleChunkCount() ?? 0}");
            Debug.Log($"Debug: Textures Loaded: {_renderer?.AreTexturesLoaded() ?? false}");
            Debug.Log($"Debug: Atlas Applied: {_renderer?.IsAtlasApplied() ?? false}");
            Debug.Log("=== Debug: Check Complete ===");
        }

        /// <summary>
        /// Debug current system status
        /// </summary>
        public void DebugStatus()
        {
            if (_enableDebugLogging)
            {
                Debug.Log("=== Debug: Initialization Tool Debug ===");
                Debug.Log($"Debug: Manual Controls Enabled: {_enableManualControls}");
                Debug.Log($"Debug: Test World: {_testWorldName} ({_testWorldWidth}x{_testWorldHeight})");
                Debug.Log($"Debug: MapStorage Ready: {MapStorage.Instance?.IsReady ?? false}");
                Debug.Log($"Debug: MapManager Available: {MapManager.Instance != null}");
                Debug.Log($"Debug: Renderer Found: {_renderer != null}");
                Debug.Log($"Debug: Renderer State: {_renderer?.GetRendererState() ?? "N/A"}");
                Debug.Log($"Debug: Visible Chunks: {_renderer?.GetVisibleChunkCount() ?? 0}");
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
            if (_renderer != null && _renderer.GetRendererState() == "ReadyForRendering")
            {
                _renderer.OnDrawGizmosSelected();
            }
        }
    }
}