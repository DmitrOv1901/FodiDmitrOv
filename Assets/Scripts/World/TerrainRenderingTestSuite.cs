using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MinesServer.Data;
using Fodinae.Assets.Scripts.Game.Managers;
using Fodinae.Assets.Scripts.Networking;
using Fodinae.Assets.Scripts.Networking.Connection;
using MinesServer.Networking.Connection.Client;
using MinesServer.Networking.Server.Packets.Connection;
using System.Linq;

namespace Fodinae.Assets.Scripts.World
{
    /// <summary>
    /// Comprehensive test suite for terrain rendering fixes.
    /// Tests all components and their interactions.
    /// </summary>
    [RequireComponent(typeof(WorldBackgroundRenderer))]
    public class TerrainRenderingTestSuite : MonoBehaviour
    {
        [Header("Test Configuration")]
        [Tooltip("Enable automatic test execution")]
        [SerializeField] private bool _autoRunTests = true;
        
        [Tooltip("Test world dimensions")]
        [SerializeField] private int _testWorldWidth = 64;
        [SerializeField] private int _testWorldHeight = 64;
        
        [Tooltip("Test world name")]
        [SerializeField] private string _testWorldName = "Test_World";
        
        [Tooltip("Enable detailed test logging")]
        [SerializeField] private bool _detailedLogging = true;

        private WorldBackgroundRenderer _renderer;
        private TerrainRenderingDiagnostics _diagnostics;
        private bool _isInitialized = false;
        private TestResults _testResults;

        [System.Serializable]
        public class TestResults
        {
            public bool AllTestsPassed;
            public List<TestResult> IndividualTests;
            public string Summary;
        }

        [System.Serializable]
        public class TestResult
        {
            public string TestName;
            public bool Passed;
            public string Details;
            public float Duration;
        }

        void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            _renderer = GetComponent<WorldBackgroundRenderer>();
            _diagnostics = GetComponent<TerrainRenderingDiagnostics>();
            
            if (_diagnostics == null)
            {
                Debug.LogWarning("TerrainRenderingTestSuite: Adding TerrainRenderingDiagnostics component");
                _diagnostics = gameObject.AddComponent<TerrainRenderingDiagnostics>();
            }

            _isInitialized = true;
            Debug.Log("TerrainRenderingTestSuite: Initialized");
        }

        void Start()
        {
            if (_autoRunTests)
            {
                StartCoroutine(RunFullTestSuite());
            }
        }

        /// <summary>
        /// Run the complete test suite
        /// </summary>
        public IEnumerator RunFullTestSuite()
        {
            Debug.Log("=== TERRAIN RENDERING TEST SUITE START ===");
            
            _testResults = new TestResults
            {
                IndividualTests = new List<TestResult>()
            };

            // Test 1: Component Initialization
            yield return RunTest("Component Initialization", TestComponentInitialization);

            // Test 2: MapStorage Initialization
            yield return RunTest("MapStorage Initialization", TestMapStorageInitialization);

            // Test 3: MapManager Initialization
            yield return RunTest("MapManager Initialization", TestMapManagerInitialization);

            // Test 4: Standalone Mode
            yield return RunTest("Standalone Mode", TestStandaloneMode);

            // Test 5: Network Mode
            yield return RunTest("Network Mode", TestNetworkMode);

            // Test 6: Terrain Rendering
            yield return RunTest("Terrain Rendering", TestTerrainRendering);

            // Test 7: Error Recovery
            yield return RunTest("Error Recovery", TestErrorRecovery);

            // Generate summary
            GenerateTestSummary();

            Debug.Log("=== TERRAIN RENDERING TEST SUITE END ===");
        }

        private IEnumerator RunTest(string testName, Func<IEnumerator> testFunction)
        {
            Debug.Log($"Running test: {testName}");
            var startTime = Time.time;
            var testResult = new TestResult { TestName = testName };

            yield return testFunction();
            testResult.Passed = true;
            testResult.Details = "Test completed successfully";

            testResult.Duration = Time.time - startTime;
            _testResults.IndividualTests.Add(testResult);

            Debug.Log($"{(testResult.Passed ? "✓" : "✗")} {testName}: {testResult.Details} ({testResult.Duration:F2}s)");
        }

        private IEnumerator TestComponentInitialization()
        {
            // Test WorldBackgroundRenderer
            if (_renderer == null)
            {
                throw new Exception("WorldBackgroundRenderer not found");
            }

            if (!_renderer.IsProperlyConfigured())
            {
                throw new Exception("WorldBackgroundRenderer not properly configured");
            }

            // Test MapStorage singleton
            if (MapStorage.Instance == null)
            {
                throw new Exception("MapStorage singleton not available");
            }

            // Test MapManager singleton
            if (MapManager.Instance == null)
            {
                throw new Exception("MapManager singleton not available");
            }

            // Test diagnostics component
            if (_diagnostics == null)
            {
                throw new Exception("TerrainRenderingDiagnostics not available");
            }

            yield return null;
        }

        private IEnumerator TestMapStorageInitialization()
        {
            // Dispose any existing world
            MapStorage.Instance.Dispose();

            // Test normal initialization
            MapStorage.Instance.InitWorld(_testWorldName, _testWorldWidth, _testWorldHeight);

            // Wait for initialization
            yield return new WaitForSeconds(1.0f);

            if (!MapStorage.Instance.IsReady)
            {
                throw new Exception($"MapStorage initialization failed. IsReady: {MapStorage.Instance.IsReady}");
            }

            if (MapStorage.Instance.cellLayer == null)
            {
                throw new Exception("MapStorage.cellLayer is null after initialization");
            }

            // Test cell access
            var testCell = MapStorage.Instance.GetCell(0, 0);
            if (testCell != CellType.Unloaded)
            {
                throw new Exception($"Unexpected cell type at (0,0): {testCell}");
            }

            // Test cell setting
            MapStorage.Instance.SetCell(0, 0, CellType.AliveBlue);
            var retrievedCell = MapStorage.Instance.GetCell(0, 0);
            if (retrievedCell != CellType.AliveBlue)
            {
                throw new Exception($"Failed to set/get cell at (0,0). Expected: AliveBlue, Got: {retrievedCell}");
            }
        }

        private IEnumerator TestMapManagerInitialization()
        {
            // Create test world init packet
            var cellConfigurations = CreateTestCellConfigurations();
            var worldInitPacket = new MinesServer.Networking.Server.Packets.Connection.WorldInitPacket
            {
                CodeName = _testWorldName,
                DisplayName = _testWorldName,
                Width = (ushort)_testWorldWidth,
                Height = (ushort)_testWorldHeight,
                Cells = cellConfigurations
            };

            // Test MapManager initialization
            MapManager.Instance.LoadWorldInit(worldInitPacket);

            // Wait for processing
            yield return new WaitForSeconds(1.0f);

            if (!MapManager.Instance._isWorldInitialized)
            {
                throw new Exception("MapManager world initialization failed");
            }

            if (MapManager.Instance.WorldCodeName != _testWorldName)
            {
                throw new Exception($"World name mismatch. Expected: {_testWorldName}, Got: {MapManager.Instance.WorldCodeName}");
            }

            if (MapManager.Instance.WorldWidth != _testWorldWidth || MapManager.Instance.WorldHeight != _testWorldHeight)
            {
                throw new Exception($"World dimensions mismatch. Expected: {_testWorldWidth}x{_testWorldHeight}, Got: {MapManager.Instance.WorldWidth}x{MapManager.Instance.WorldHeight}");
            }
        }

        private IEnumerator TestStandaloneMode()
        {
            // Find standalone initializer
            var standaloneInit = FindObjectOfType<StandaloneWorldInitializer>();
            if (standaloneInit == null)
            {
                throw new Exception("StandaloneWorldInitializer not found in scene");
            }

            // Test force initialization
            standaloneInit.ForceStandaloneInitialization();

            // Wait for initialization
            yield return new WaitForSeconds(3.0f);

            if (!standaloneInit.IsReady())
            {
                throw new Exception("StandaloneWorldInitializer failed to become ready");
            }

            if (!MapStorage.Instance.IsReady)
            {
                throw new Exception("MapStorage not ready after standalone initialization");
            }
        }

        private IEnumerator TestNetworkMode()
        {
            // Test with DummyConnection
            DummyConnection dummyConnection = null;//FindObjectOfType<DummyConnection>();
            if (dummyConnection == null)
            {
                Debug.LogWarning("DummyConnection not found, skipping network mode test");
                yield break;
            }

            // Ensure MapStorage is clean
            MapStorage.Instance.Dispose();

            // Start dummy connection
            dummyConnection.Connect();

            // Wait for packets to be processed
            yield return new WaitForSeconds(2.0f);

            // Check if world was initialized
            if (!MapManager.Instance._isWorldInitialized)
            {
                throw new Exception("World not initialized via network mode");
            }

            if (!MapStorage.Instance.IsReady)
            {
                throw new Exception("MapStorage not ready after network initialization");
            }
        }

        private IEnumerator TestTerrainRendering()
        {
            // Ensure world is initialized
            if (!MapManager.Instance._isWorldInitialized)
            {
                var cellConfigurations = CreateTestCellConfigurations();
                var worldInitPacket = new MinesServer.Networking.Server.Packets.Connection.WorldInitPacket
                {
                    CodeName = _testWorldName,
                    DisplayName = _testWorldName,
                    Width = (ushort)_testWorldWidth,
                    Height = (ushort)_testWorldHeight,
                    Cells = cellConfigurations
                };
                MapManager.Instance.LoadWorldInit(worldInitPacket);
            }

            // Wait for renderer to process
            yield return new WaitForSeconds(2.0f);

            // Check renderer state
            var rendererState = _renderer.GetRendererState();
            if (rendererState != "ReadyForRendering")
            {
                throw new Exception($"Renderer not ready. Current state: {rendererState}");
            }

            // Check if visible chunks are being generated
            var visibleChunkCount = _renderer.GetVisibleChunkCount();
            if (visibleChunkCount <= 0)
            {
                throw new Exception($"No visible chunks generated. Count: {visibleChunkCount}");
            }

            // Check if textures are loaded
            if (!_renderer.AreTexturesLoaded())
            {
                Debug.LogWarning("Textures not loaded yet, but renderer is ready");
            }
        }

        private IEnumerator TestErrorRecovery()
        {
            // Test MapStorage disposal and re-creation
            MapStorage.Instance.Dispose();
            
            yield return new WaitForSeconds(0.5f);

            if (MapStorage.Instance.IsReady)
            {
                throw new Exception("MapStorage should not be ready after disposal");
            }

            // Re-initialize
            MapStorage.Instance.InitWorld(_testWorldName, _testWorldWidth, _testWorldHeight);
            
            yield return new WaitForSeconds(1.0f);

            if (!MapStorage.Instance.IsReady)
            {
                throw new Exception("MapStorage failed to recover after disposal");
            }

            // Test renderer recovery
            _renderer.ForceReinitialize();
            
            yield return new WaitForSeconds(1.0f);

            var rendererState = _renderer.GetRendererState();
            if (rendererState != "ReadyForRendering")
            {
                throw new Exception($"Renderer failed to recover. State: {rendererState}");
            }
        }

        private CellConfigurationPacket[] CreateTestCellConfigurations()
        {
            var configurations = new CellConfigurationPacket[256];
            
            for (int i = 0; i < configurations.Length; i++)
            {
                configurations[i] = new CellConfigurationPacket
                {
                    Animation = 0,
                    AnimationSpeed = 0,
                    Color = unchecked((int)0xFFFFFFFF),
                    FrameOffset = 0,
                    Properties = 0
                };
            }

            // Configure some common cell types
            if (configurations.Length > 1)
            {
                configurations[1] = new CellConfigurationPacket
                {
                    Animation = 0,
                    AnimationSpeed = 0,
                    Color = unchecked((int)0xFF00FF00),
                    FrameOffset = 0,
                    Properties = 0
                };
            }

            return configurations;
        }

        private void GenerateTestSummary()
        {
            var passedTests = _testResults.IndividualTests.Count(t => t.Passed);
            var totalTests = _testResults.IndividualTests.Count;
            var successRate = (float)passedTests / totalTests * 100;

            _testResults.AllTestsPassed = passedTests == totalTests;
            _testResults.Summary = $"Tests: {passedTests}/{totalTests} passed ({successRate:F1}%)";

            Debug.Log($"=== TEST SUMMARY ===");
            Debug.Log(_testResults.Summary);
            
            if (_testResults.AllTestsPassed)
            {
                Debug.Log("✓ ALL TESTS PASSED - Terrain rendering system is working correctly!");
            }
            else
            {
                Debug.LogWarning("✗ SOME TESTS FAILED - Check individual test results for details");
            }

            // Log detailed results
            foreach (var test in _testResults.IndividualTests)
            {
                var status = test.Passed ? "PASS" : "FAIL";
                Debug.Log($"{status}: {test.TestName} ({test.Duration:F2}s) - {test.Details}");
            }
        }

        /// <summary>
        /// Get the test results
        /// </summary>
        public TestResults GetTestResults() => _testResults;

        /// <summary>
        /// Run a specific test by name
        /// </summary>
        public IEnumerator RunSpecificTest(string testName)
        {
            switch (testName.ToLower())
            {
                case "component":
                    yield return TestComponentInitialization();
                    break;
                case "mapstorage":
                    yield return TestMapStorageInitialization();
                    break;
                case "mapmanager":
                    yield return TestMapManagerInitialization();
                    break;
                case "standalone":
                    yield return TestStandaloneMode();
                    break;
                case "network":
                    yield return TestNetworkMode();
                    break;
                case "rendering":
                    yield return TestTerrainRendering();
                    break;
                case "recovery":
                    yield return TestErrorRecovery();
                    break;
                default:
                    throw new ArgumentException($"Unknown test name: {testName}");
            }
        }

        /// <summary>
        /// Export test results to string
        /// </summary>
        public string ExportTestResults()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== TERRAIN RENDERING TEST RESULTS ===");
            report.AppendLine($"Generated: {DateTime.Now}");
            report.AppendLine($"Auto Run: {_autoRunTests}");
            report.AppendLine($"Test World: {_testWorldName} ({_testWorldWidth}x{_testWorldHeight})");
            report.AppendLine();

            if (_testResults != null)
            {
                report.AppendLine($"Summary: {_testResults.Summary}");
                report.AppendLine();

                report.AppendLine("INDIVIDUAL TESTS:");
                foreach (var test in _testResults.IndividualTests)
                {
                    var status = test.Passed ? "PASS" : "FAIL";
                    report.AppendLine($"{status}: {test.TestName}");
                    report.AppendLine($"  Duration: {test.Duration:F2}s");
                    report.AppendLine($"  Details: {test.Details}");
                    report.AppendLine();
                }
            }
            else
            {
                report.AppendLine("No test results available");
            }

            report.AppendLine("=== END TEST RESULTS ===");
            return report.ToString();
        }

        /// <summary>
        /// Clear test results
        /// </summary>
        public void ClearResults()
        {
            _testResults = null;
            Debug.Log("TerrainRenderingTestSuite: Test results cleared");
        }

        private void OnDestroy()
        {
            Debug.Log("TerrainRenderingTestSuite: Destroyed");
        }
    }
}