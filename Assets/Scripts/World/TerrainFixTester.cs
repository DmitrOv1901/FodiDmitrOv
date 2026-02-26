using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fodinae.Assets.Scripts.Game.Managers;
using Fodinae.Assets.Scripts.World;
using MinesServer.Data;
using MinesServer.Networking.Server.Packets.Connection;

namespace Fodinae.Assets.Scripts.World
{
    /// <summary>
    /// Standalone terrain fix tester that can be run in the Unity Editor
    /// to verify that the terrain rendering fixes are working correctly.
    /// </summary>
    public class TerrainFixTester : MonoBehaviour
    {
        [Header("Test Configuration")]
        [Tooltip("Enable automatic testing on start")]
        [SerializeField] private bool _autoTestOnStart = true;
        [Tooltip("Enable detailed logging")]
        [SerializeField] private bool _enableDetailedLogging = true;

        [Header("Test Actions")]
        [Tooltip("Force system reinitialization on start")]
        [SerializeField] private bool _forceReinitializeOnStart = false;
        [Tooltip("Create test world if no world data available")]
        [SerializeField] private bool _createTestWorldIfMissing = true;

        private WorldBackgroundRenderer _renderer;
        private TerrainDiagnosticRunner _diagnosticRunner;

        void Start()
        {
            if (_autoTestOnStart)
            {
                StartCoroutine(RunTerrainFixTest());
            }
        }

        /// <summary>
        /// Run comprehensive terrain fix test
        /// </summary>
        public IEnumerator RunTerrainFixTest()
        {
            Debug.Log("=== TERRAIN FIX TESTER STARTED ===");
            Debug.Log("Testing terrain rendering fixes...");

            // Find components
            _renderer = FindObjectOfType<WorldBackgroundRenderer>();
            _diagnosticRunner = FindObjectOfType<TerrainDiagnosticRunner>();

            if (_renderer == null)
            {
                Debug.LogError("❌ WorldBackgroundRenderer not found in scene");
                yield break;
            }

            Debug.Log("✅ Found WorldBackgroundRenderer");

            if (_diagnosticRunner == null)
            {
                Debug.LogWarning("⚠ TerrainDiagnosticRunner not found - adding component");
                _diagnosticRunner = _renderer.gameObject.AddComponent<TerrainDiagnosticRunner>();
            }

            // Force reinitialization if requested
            if (_forceReinitializeOnStart)
            {
                Debug.Log("🔄 Forcing system reinitialization...");
                _diagnosticRunner.ForceSystemReinitialize();
                yield return new WaitForSeconds(2f);
            }

            // Check if we have world data
            bool hasWorldData = CheckWorldData();

            if (!hasWorldData && _createTestWorldIfMissing)
            {
                Debug.Log("🌍 Creating test world since no world data available...");
                CreateTestWorld();
                yield return new WaitForSeconds(1f);
            }

            // Run diagnostics
            Debug.Log("🔍 Running terrain diagnostics...");
            yield return StartCoroutine(_diagnosticRunner.RunDiagnostics());

            // Final verification
            Debug.Log("✅ Terrain fix test completed");
            Debug.Log("Check the diagnostic output above for any issues");
        }

        private bool CheckWorldData()
        {
            if (MapStorage.Instance == null || !MapStorage.Instance.IsReady)
            {
                return false;
            }

            try
            {
                // Test a few cells
                var cell1 = MapStorage.Instance.GetCell(0, 0);
                var cell2 = MapStorage.Instance.GetCell(10, 10);
                
                return cell1 != CellType.Unloaded && cell1 != CellType.Pregener;
            }
            catch
            {
                return false;
            }
        }

        private void CreateTestWorld()
        {
            Debug.Log("Creating test world with dimensions 64x64...");

            // Create a minimal test world
            if (MapManager.Instance != null)
            {
                // Create a test world init packet
                var testPacket = new WorldInitPacket
                {
                    CodeName = "test_world",
                    DisplayName = "Test World",
                    Width = 64,
                    Height = 64,
                    Cells = new CellConfigurationPacket[256]
                }; // Default configurations

                // Initialize the world
                MapManager.Instance.LoadWorldInit(testPacket);
                
                Debug.Log("✅ Test world created successfully");
            }
            else
            {
                Debug.LogError("❌ MapManager not available to create test world");
            }
        }

        /// <summary>
        /// Manual test method that can be called from the Unity Editor
        /// </summary>
        public void RunManualTest()
        {
            StartCoroutine(RunTerrainFixTest());
        }

        /// <summary>
        /// Force system reinitialization
        /// </summary>
        public void ForceReinitialize()
        {
            if (_diagnosticRunner != null)
            {
                _diagnosticRunner.ForceSystemReinitialize();
            }
            else
            {
                Debug.LogWarning("No diagnostic runner available for reinitialization");
            }
        }

        /// <summary>
        /// Get current system status
        /// </summary>
        public string GetSystemStatus()
        {
            var status = new System.Text.StringBuilder();
            status.AppendLine("=== TERRAIN SYSTEM STATUS ===");

            if (MapManager.Instance != null)
            {
                status.AppendLine($"MapManager: {MapManager.Instance.WorldDisplayName} ({MapManager.Instance.WorldCodeName})");
                status.AppendLine($"Dimensions: {MapManager.Instance.WorldWidth}x{MapManager.Instance.WorldHeight}");
            }
            else
            {
                status.AppendLine("MapManager: Not available");
            }

            if (MapStorage.Instance != null)
            {
                status.AppendLine($"MapStorage: Ready={MapStorage.Instance.IsReady}, Initialized={MapStorage.Instance.IsInitialized()}");
                status.AppendLine($"World: {MapStorage.Instance.GetWorldCodeName()}");
                status.AppendLine($"CellLayer: {(MapStorage.Instance.cellLayer != null ? "Available" : "NULL")}");
            }
            else
            {
                status.AppendLine("MapStorage: Not available");
            }

            if (_renderer != null)
            {
                status.AppendLine($"Renderer: Configured={_renderer.IsProperlyConfigured()}, Chunks={_renderer.GetVisibleChunkCount()}");
                status.AppendLine($"Textures: Loaded={_renderer.AreTexturesLoaded()}, Atlas={_renderer.IsAtlasApplied()}");
            }
            else
            {
                status.AppendLine("Renderer: Not available");
            }

            return status.ToString();
        }

        private void OnValidate()
        {
            if (_forceReinitializeOnStart && !_autoTestOnStart)
            {
                Debug.LogWarning("ForceReinitializeOnStart is enabled but AutoTestOnStart is disabled");
            }
        }
    }
}